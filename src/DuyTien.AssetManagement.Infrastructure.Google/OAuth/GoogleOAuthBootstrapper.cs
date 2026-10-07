using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;

namespace DuyTien.AssetManagement.Infrastructure.Google.OAuth;

public sealed class GoogleOAuthBootstrapper(
    GoogleWorkspaceOptions options,
    HttpClient httpClient)
{
    public const string RedirectUri = "http://127.0.0.1:53682/authorize/";
    private const string SpreadsheetMimeType = "application/vnd.google-apps.spreadsheet";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    public async Task<string> AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        var result = await AuthorizeCoreAsync(
            pickerResourceKind: null,
            cancellationToken).ConfigureAwait(false);
        return result.RefreshToken;
    }

    public Task<GoogleOAuthAuthorizationResult> AuthorizeAndPickAsync(
        GooglePickerResourceKind resourceKind,
        CancellationToken cancellationToken = default) =>
        AuthorizeCoreAsync(resourceKind, cancellationToken);

    private async Task<GoogleOAuthAuthorizationResult> AuthorizeCoreAsync(
        GooglePickerResourceKind? pickerResourceKind,
        CancellationToken cancellationToken)
    {
        var missing = options.GetMissingOAuthBootstrapSettings();
        if (missing.Count > 0)
        {
            throw new ExternalStorageAuthenticationException(
                $"OAuth bootstrap configuration is incomplete: {string.Join(", ", missing)}.");
        }

        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var codeVerifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var codeChallenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        var authorizationUri = BuildAuthorizationUri(
            state,
            codeChallenge,
            pickerResourceKind);

        using var listener = new TcpListener(IPAddress.Loopback, 53682);
        listener.Start(backlog: 1);
        OpenSystemBrowser(authorizationUri);

        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        var callback = await ReadCallbackAsync(client, cancellationToken).ConfigureAwait(false);
        await WriteBrowserResponseAsync(client, callback.Error is null, cancellationToken)
            .ConfigureAwait(false);

        if (callback.Error is not null)
        {
            throw new ExternalStorageAuthenticationException(
                $"Google OAuth authorization did not complete: {callback.Error}.");
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state),
                Encoding.UTF8.GetBytes(callback.State ?? string.Empty)))
        {
            throw new ExternalStorageAuthenticationException("OAuth state validation failed.");
        }

        if (string.IsNullOrWhiteSpace(callback.Code))
        {
            throw new ExternalStorageAuthenticationException("OAuth callback did not contain a code.");
        }

        var pickedFileId = ValidatePickerResult(callback, pickerResourceKind);
        var refreshToken = await ExchangeCodeAsync(
            callback.Code,
            codeVerifier,
            cancellationToken).ConfigureAwait(false);
        return new GoogleOAuthAuthorizationResult(refreshToken, pickedFileId);
    }

    internal Uri BuildAuthorizationUri(
        string state,
        string codeChallenge,
        GooglePickerResourceKind? pickerResourceKind = null)
    {
        var values = new Dictionary<string, string>
        {
            ["client_id"] = options.OAuthClientId!,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', GoogleOAuthScopes.Required),
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        if (pickerResourceKind is not null)
        {
            values["trigger_onepick"] = "true";
            values["allow_multiple"] = "false";
            values["mimetypes"] = pickerResourceKind is GooglePickerResourceKind.Spreadsheet or GooglePickerResourceKind.SystemSpreadsheet
                ? SpreadsheetMimeType
                : FolderMimeType;
            if (pickerResourceKind == GooglePickerResourceKind.Folder)
            {
                values["allow_folder_selection"] = "true";
            }
        }

        var query = string.Join('&', values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{options.OAuthAuthorizationEndpoint}?{query}");
    }

    private async Task<string> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.OAuthClientId!,
            ["client_secret"] = options.OAuthClientSecret!,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri
        });
        using var response = await httpClient.PostAsync(
            options.OAuthTokenEndpoint,
            content,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalStorageAuthenticationException(
                $"Google rejected the authorization code with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var token = await JsonSerializer.DeserializeAsync<TokenResponse>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken).ConfigureAwait(false);
        if (token is null || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new ExternalStorageAuthenticationException(
                "Google did not return a refresh token. Revoke the prior grant and retry consent.");
        }

        GoogleOAuthScopes.EnsureExactGrant(token.Scope);
        return token.RefreshToken;
    }

    private static string? ValidatePickerResult(
        OAuthCallback callback,
        GooglePickerResourceKind? pickerResourceKind)
    {
        if (pickerResourceKind is null)
        {
            return null;
        }

        var pickedIds = (callback.PickedFileIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pickedIds.Length != 1 || string.IsNullOrWhiteSpace(pickedIds[0]))
        {
            throw new ExternalStorageAuthenticationException(
                "Google Picker must return exactly one selected resource.");
        }

        return pickedIds[0];
    }

    private static async Task<OAuthCallback> ReadCallbackAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return new OAuthCallback(null, null, "empty_callback", null);
        }

        string? line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        while (!string.IsNullOrEmpty(line));

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return new OAuthCallback(null, null, "invalid_callback", null);
        }

        var uri = new Uri(new Uri(RedirectUri), parts[1]);
        if (!uri.AbsolutePath.Equals("/authorize/", StringComparison.Ordinal))
        {
            return new OAuthCallback(null, null, "invalid_callback_path", null);
        }

        var query = ParseQuery(uri.Query);
        query.TryGetValue("code", out var code);
        query.TryGetValue("state", out var callbackState);
        query.TryGetValue("error", out var error);
        query.TryGetValue("picked_file_ids", out var pickedFileIds);
        return new OAuthCallback(code, callbackState, error, pickedFileIds);
    }

    private static async Task WriteBrowserResponseAsync(
        TcpClient client,
        bool success,
        CancellationToken cancellationToken)
    {
        var body = success
            ? "<!doctype html><meta charset=\"utf-8\"><title>Đã cấp quyền</title><p>Đã nhận phản hồi OAuth. Có thể đóng tab này.</p>"
            : "<!doctype html><meta charset=\"utf-8\"><title>Không hoàn tất</title><p>Không hoàn tất cấp quyền OAuth. Quay lại cửa sổ lệnh để xem trạng thái.</p>";
        var bytes = Encoding.UTF8.GetBytes(body);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await using var stream = client.GetStream();
        await stream.WriteAsync(headers, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split('=', 2))
            .ToDictionary(
                item => Uri.UnescapeDataString(item[0].Replace('+', ' ')),
                item => item.Length > 1
                    ? Uri.UnescapeDataString(item[1].Replace('+', ' '))
                    : string.Empty,
                StringComparer.Ordinal);

    private static void OpenSystemBrowser(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            throw new ExternalStorageAuthenticationException(
                "Could not open the system browser for Google OAuth.",
                exception);
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record OAuthCallback(
        string? Code,
        string? State,
        string? Error,
        string? PickedFileIds);
    private sealed record TokenResponse(
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);
}
