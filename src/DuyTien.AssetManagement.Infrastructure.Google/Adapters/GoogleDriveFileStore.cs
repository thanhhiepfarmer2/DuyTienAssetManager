using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;

namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleDriveFileStore(
    GoogleWorkspaceOptions options,
    GoogleApiRequestExecutor api) : IAssetFileStore
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    public async Task<StorageReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        var missingSettings = options.GetMissingDriveSettings();
        if (missingSettings.Count > 0)
        {
            return Readiness(live: false, missingSettings);
        }

        try
        {
            var folder = await GetFileMetadataAsync(options.DriveFolderId!, cancellationToken)
                .ConfigureAwait(false);
            var live = string.Equals(folder.Id, options.DriveFolderId, StringComparison.Ordinal) &&
                string.Equals(folder.MimeType, FolderMimeType, StringComparison.Ordinal) &&
                !folder.Trashed &&
                folder.Capabilities?.CanAddChildren == true;
            return Readiness(live, missingSettings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ExternalStorageException)
        {
            return Readiness(live: false, missingSettings);
        }
    }

    public async Task<StoredFile> UploadAsync(
        FileUploadRequest request,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ValidateUpload(request);

        var existing = await FindByOperationAsync(request.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (!string.Equals(existing.FileName, request.FileName, StringComparison.Ordinal) ||
                !string.Equals(existing.ContentType, request.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                throw new ExternalStorageException(
                    "Drive operation id already exists with different file metadata.");
            }

            return existing with { Reconciled = true };
        }

        var bytes = content.ToArray();
        var metadata = new
        {
            name = request.FileName,
            parents = new[] { options.DriveFolderId! },
            appProperties = new Dictionary<string, string>
            {
                ["task"] = "TASK-004",
                ["operationId"] = request.OperationId,
                ["sha256"] = request.Sha256
            }
        };
        var relative = "upload/drive/v3/files?uploadType=multipart&" +
            "fields=id,name,mimeType,size,md5Checksum,parents,appProperties,trashed";

        try
        {
            using var response = await api.SendAsync(
                () => CreateMultipartRequest(BuildDriveUri(relative), metadata, request.ContentType, bytes),
                GoogleRequestSemantics.NonIdempotentWrite,
                cancellationToken).ConfigureAwait(false);
            var file = await ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
            return ToStoredFile(file, request.OperationId, reconciled: false);
        }
        catch (ExternalStorageTransientException exception) when (exception.CommitOutcomeUnknown)
        {
            var reconciled = await FindByOperationAsync(request.OperationId, cancellationToken)
                .ConfigureAwait(false);
            if (reconciled is not null)
            {
                return reconciled with { Reconciled = true };
            }

            throw;
        }
    }

    public async Task<StoredFile?> FindByOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("Operation id is required.", nameof(operationId));
        }

        var escapedFolderId = EscapeDriveQuery(options.DriveFolderId!);
        var escapedOperationId = EscapeDriveQuery(operationId);
        var query = $"'{escapedFolderId}' in parents and trashed = false and " +
            $"appProperties has {{ key='operationId' and value='{escapedOperationId}' }}";
        var relative = "drive/v3/files?spaces=drive&pageSize=2&" +
            $"q={Uri.EscapeDataString(query)}&" +
            "fields=files(id,name,mimeType,size,md5Checksum,parents,appProperties,trashed)";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildDriveUri(relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<FileListResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        var files = result?.Files ?? [];
        if (files.Length > 1)
        {
            throw new ExternalStorageException(
                "More than one Drive file has the same TASK-004 operation id; manual reconciliation is required.");
        }

        return files.Length == 0
            ? null
            : ToStoredFile(files[0], operationId, reconciled: true);
    }

    public async Task<byte[]> DownloadAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new ArgumentException("File id is required.", nameof(fileId));
        }

        var relative = $"drive/v3/files/{Uri.EscapeDataString(fileId)}?alt=media";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildDriveUri(relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<DriveFileResponse> GetFileMetadataAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        var relative = $"drive/v3/files/{Uri.EscapeDataString(fileId)}?" +
            "fields=id,name,mimeType,parents,trashed,capabilities(canAddChildren,canDownload)";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildDriveUri(relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        return await ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateMultipartRequest(
        Uri uri,
        object metadata,
        string contentType,
        byte[] bytes)
    {
        var boundary = $"task004_{Guid.NewGuid():N}";
        var multipart = new MultipartContent("related", boundary);
        multipart.Add(JsonContent.Create(metadata, options: JsonOptions));
        var media = new ByteArrayContent(bytes);
        media.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        multipart.Add(media);
        return new HttpRequestMessage(HttpMethod.Post, uri) { Content = multipart };
    }

    private static async Task<DriveFileResponse> ReadFileAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<DriveFileResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false) ??
            throw new ExternalStorageException("Google Drive returned an empty file response.");
    }

    private static StoredFile ToStoredFile(
        DriveFileResponse file,
        string operationId,
        bool reconciled) =>
        new(
            file.Id,
            file.Name,
            file.MimeType,
            ParseSize(file.Size),
            file.Md5Checksum,
            operationId,
            reconciled);

    private static long? ParseSize(string? value) =>
        long.TryParse(value, out var parsed) ? parsed : null;

    private static string EscapeDriveQuery(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);

    private static void ValidateUpload(FileUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OperationId) ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            string.IsNullOrWhiteSpace(request.ContentType) ||
            string.IsNullOrWhiteSpace(request.Sha256))
        {
            throw new ArgumentException("Upload operation id, name, content type and SHA-256 are required.");
        }

        if (request.FileName.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException("File name must not contain a path separator.");
        }
    }

    private Uri BuildDriveUri(string relative) => new(new Uri(options.DriveApiBaseUrl), relative);

    private void EnsureConfigured()
    {
        var missing = options.GetMissingDriveSettings();
        if (missing.Count > 0)
        {
            throw new ExternalStorageException(
                $"Google Drive configuration is incomplete: {string.Join(", ", missing)}.");
        }
    }

    private static StorageReadiness Readiness(
        bool live,
        IReadOnlyCollection<string> missingSettings) =>
        new(
            "Google Drive My Drive via company operator OAuth",
            ConfigurationPresent: missingSettings.Count == 0,
            LiveConnectionVerified: live,
            MissingSettings: missingSettings);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record FileListResponse(DriveFileResponse[] Files);
    private sealed record DriveFileResponse(
        string Id,
        string Name,
        string MimeType,
        string? Size,
        string? Md5Checksum,
        string[]? Parents,
        Dictionary<string, string>? AppProperties,
        bool Trashed,
        DriveCapabilities? Capabilities);
    private sealed record DriveCapabilities(bool CanAddChildren, bool CanDownload);
}
