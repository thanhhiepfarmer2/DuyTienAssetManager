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
                throw new ExternalStorageAuthenticationException(
                    $"Google rejected the refresh token request with HTTP {(int)response.StatusCode}.");
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
