using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;

namespace DuyTien.AssetManagement.Infrastructure.Google;

public sealed class GoogleWorkspaceProvisioner(
    GoogleWorkspaceOptions options,
    GoogleApiRequestExecutor api)
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    public async Task<string> CreateSpreadsheetAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        ValidateName(title, nameof(title));
        EnsureOAuthConfigured();

        var uri = new Uri(
            new Uri(options.SheetsApiBaseUrl),
            "spreadsheets?fields=spreadsheetId");
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(new { properties = new { title } })
            },
            GoogleRequestSemantics.NonIdempotentWrite,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<SpreadsheetResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(result?.SpreadsheetId)
            ? throw new ExternalStorageException("Sheets did not return the provisioned spreadsheet id.")
            : result.SpreadsheetId;
    }

    public async Task<string> CreateFolderAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name, nameof(name));
        EnsureOAuthConfigured();

        var uri = new Uri(
            new Uri(options.DriveApiBaseUrl),
            "drive/v3/files?fields=id");
        using var response = await api.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(new { name, mimeType = FolderMimeType })
            },
            GoogleRequestSemantics.NonIdempotentWrite,
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<DriveFileResponse>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(result?.Id)
            ? throw new ExternalStorageException("Drive did not return the provisioned folder id.")
            : result.Id;
    }

    private void EnsureOAuthConfigured()
    {
        var missing = options.GetMissingOAuthRuntimeSettings();
        if (missing.Count > 0)
        {
            throw new ExternalStorageException(
                $"Google OAuth configuration is incomplete: {string.Join(", ", missing)}.");
        }
    }

    private static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120)
        {
            throw new ArgumentException("Resource name is required and may contain at most 120 characters.", parameterName);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record SpreadsheetResponse(string SpreadsheetId);
    private sealed record DriveFileResponse(string Id);
}
