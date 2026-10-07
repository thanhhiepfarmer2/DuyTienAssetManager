using System.Text.Json;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;

namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleDriveAccessVerifier(
    GoogleWorkspaceOptions options,
    GoogleApiRequestExecutor api)
{
    public async Task EnsureAccessibleAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new ArgumentException("File id is required.", nameof(fileId));
        }

        var relative = $"drive/v3/files/{Uri.EscapeDataString(fileId)}?fields=id,trashed";
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(new Uri(options.DriveApiBaseUrl), relative)),
            GoogleRequestSemantics.SafeRead,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var file = await JsonSerializer.DeserializeAsync<DriveFileResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        if (file is null ||
            !string.Equals(file.Id, fileId, StringComparison.Ordinal) ||
            file.Trashed)
        {
            throw new ExternalStorageException(
                "Drive did not return the expected accessible, non-trashed file.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record DriveFileResponse(string Id, bool Trashed);
}
