using System.Net;
using System.Text;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleOAuthTokenProviderTests
{
    [Fact]
    public async Task RefreshTokenResponse_ParsesGoogleSnakeCaseAndCachesAccessToken()
    {
        var handler = new TokenHandler();
        using var client = new HttpClient(handler);
        var provider = new GoogleOAuthTokenProvider(
            new GoogleWorkspaceOptions
            {
                OAuthClientId = "client-id",
                OAuthClientSecret = "client-secret",
                OAuthRefreshToken = "refresh-token",
                SpreadsheetId = "sheet-id",
                OAuthTokenEndpoint = "https://example.test/token"
            },
            client);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("access-token", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain("refresh-token", handler.LastResponseBody ?? string.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://www.googleapis.com/auth/drive")]
    [InlineData("https://www.googleapis.com/auth/spreadsheets")]
    [InlineData("https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/drive")]
    public async Task RefreshTokenResponse_RejectsMissingOrBroadScopes(string grantedScopes)
    {
        var handler = new TokenHandler(grantedScopes);
        using var client = new HttpClient(handler);
        using var provider = new GoogleOAuthTokenProvider(
            CompleteOptions(),
            client);

        var exception = await Assert.ThrowsAsync<ExternalStorageAuthenticationException>(
            () => provider.GetAccessTokenAsync());

        Assert.Contains("exactly drive.file", exception.Message, StringComparison.Ordinal);
    }

    private static GoogleWorkspaceOptions CompleteOptions() => new()
    {
        OAuthClientId = "client-id",
        OAuthClientSecret = "client-secret",
        OAuthRefreshToken = "refresh-token",
        OAuthTokenEndpoint = "https://example.test/token"
    };

    private sealed class TokenHandler(
        string grantedScopes = GoogleOAuthScopes.DriveFile) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string? LastResponseBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastResponseBody = $$"""
                {"access_token":"access-token","expires_in":3600,"token_type":"Bearer","scope":"{{grantedScopes}}"}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(LastResponseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
