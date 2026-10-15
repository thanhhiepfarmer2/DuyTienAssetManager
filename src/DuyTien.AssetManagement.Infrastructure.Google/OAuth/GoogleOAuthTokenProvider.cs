using System.Text.Json;
using System.Text.Json.Serialization;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;

namespace DuyTien.AssetManagement.Infrastructure.Google.OAuth;

public sealed class GoogleOAuthTokenProvider(
    GoogleWorkspaceOptions options,
    HttpClient httpClient) : IGoogleAccessTokenProvider, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? accessToken;
    private DateTimeOffset expiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (HasUsableToken())
        {
            return accessToken!;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (HasUsableToken())
            {
                return accessToken!;
            }

            var missing = options.GetMissingOAuthRuntimeSettings();
            if (missing.Count > 0)
            {
                throw new ExternalStorageAuthenticationException(
                    $"Google OAuth configuration is incomplete: {string.Join(", ", missing)}.");
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.OAuthClientId!,
                ["client_secret"] = options.OAuthClientSecret!,
                ["refresh_token"] = options.OAuthRefreshToken!,
                ["grant_type"] = "refresh_token"
            });

            using var response = await httpClient.PostAsync(
                options.OAuthTokenEndpoint,
                content,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var code = await ReadFailureCodeAsync(response, cancellationToken).ConfigureAwait(false);
                throw new ExternalStorageAuthenticationException(
                    $"Google rejected the refresh token request with HTTP {(int)response.StatusCode}.")
                { StatusCode = (int)response.StatusCode, FailureCode = code };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var token = await JsonSerializer.DeserializeAsync<TokenResponse>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new ExternalStorageAuthenticationException(
                    "Google returned a token response without an access token.");
            }

            GoogleOAuthScopes.EnsureExactGrant(token.Scope);
            accessToken = token.AccessToken;
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn));
            return accessToken;
        }
        catch (ExternalStorageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new ExternalStorageAuthenticationException(
                "Could not obtain a Google access token.",
                exception);
        }
        finally
        {
            gate.Release();
        }
    }

    // Inspect only the standard error code, bounded in memory. Never retain/log response text,
    // error_description, URLs, credentials or provider-supplied exception messages.
    private static async Task<string> ReadFailureCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var bytes = new byte[4097]; var used = 0;
            while (used < bytes.Length)
            {
                var count = await stream.ReadAsync(bytes.AsMemory(used), ct).ConfigureAwait(false);
                if (count == 0) break;
                used += count;
            }
            if (used > 4096) return "unclassified";
            using var document = JsonDocument.Parse(bytes.AsMemory(0, used));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.String)
                return "unclassified";
            return error.GetString() switch
            {
                "invalid_grant" => "invalid_grant",
                "invalid_client" => "invalid_client",
                "unauthorized_client" => "unauthorized_client",
                "invalid_request" => "invalid_request",
                "temporarily_unavailable" => "temporarily_unavailable",
                "access_denied" => "access_denied",
                _ => "unclassified"
            };
        }
        catch (Exception e) when (e is JsonException or IOException or HttpRequestException)
        { return "unclassified"; }
    }

    public void Invalidate()
    {
        accessToken = null;
        expiresAt = default;
    }

    public void Dispose() => gate.Dispose();

    private bool HasUsableToken() =>
        !string.IsNullOrWhiteSpace(accessToken) &&
        expiresAt > DateTimeOffset.UtcNow.AddMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);
}
