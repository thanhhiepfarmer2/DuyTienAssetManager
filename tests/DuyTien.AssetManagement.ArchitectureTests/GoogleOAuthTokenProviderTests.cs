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

    [Theory]
    [InlineData("invalid_grant", "invalid_grant")]
    [InlineData("invalid_client", "invalid_client")]
    [InlineData("access_denied", "access_denied")]
    [InlineData("secret-body-must-not-leak", "unclassified")]
    public async Task Refresh_failure_reports_only_allowlisted_code_and_status(string error, string expected)
    {
        using var client = new HttpClient(new FailureHandler("{\"error\":\"" + error + "\",\"error_description\":\"secret-body-must-not-leak\"}"));
        using var provider = new GoogleOAuthTokenProvider(CompleteOptions(), client);
        var failure = await Assert.ThrowsAsync<ExternalStorageAuthenticationException>(() => provider.GetAccessTokenAsync());
        Assert.Equal(400, failure.StatusCode); Assert.Equal(expected, failure.FailureCode);
        Assert.DoesNotContain("secret-body", failure.ToString());
        Assert.DoesNotContain("refresh-token", failure.ToString());
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"error\":42}")]
    public async Task Invalid_error_body_is_not_retained_or_exposed(string body)
    {
        using var client = new HttpClient(new FailureHandler(body));
        using var provider = new GoogleOAuthTokenProvider(CompleteOptions(), client);
        var failure = await Assert.ThrowsAsync<ExternalStorageAuthenticationException>(() => provider.GetAccessTokenAsync());
        Assert.Equal(400, failure.StatusCode); Assert.Equal("unclassified", failure.FailureCode);
    }

    [Fact]
    public async Task Oversize_error_body_is_bounded_and_cannot_supply_a_code()
    {
        using var client = new HttpClient(new FailureHandler("{\"error\":\"invalid_grant\",\"error_description\":\"" + new string('x', 5000) + "\"}"));
        using var provider = new GoogleOAuthTokenProvider(CompleteOptions(), client);
        var failure = await Assert.ThrowsAsync<ExternalStorageAuthenticationException>(() => provider.GetAccessTokenAsync());
        Assert.Equal("unclassified", failure.FailureCode);
    }

    private sealed class FailureHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
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
