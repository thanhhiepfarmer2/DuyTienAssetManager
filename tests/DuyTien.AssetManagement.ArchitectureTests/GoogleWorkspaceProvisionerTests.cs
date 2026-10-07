using System.Net;
using System.Text;
using DuyTien.AssetManagement.Infrastructure.Google;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleWorkspaceProvisionerTests
{
    [Fact]
    public async Task Provisioning_UsesExplicitCreateRequestsAndReturnsCreatedIds()
    {
        var handler = new ProvisioningHandler();
        var options = new GoogleWorkspaceOptions
        {
            OAuthClientId = "client",
            OAuthClientSecret = "secret",
            OAuthRefreshToken = "refresh",
            SheetsApiBaseUrl = "https://example.test/v4/",
            DriveApiBaseUrl = "https://example.test/"
        };
        using var client = new HttpClient(handler);
        var executor = new GoogleApiRequestExecutor(
            options,
            new StaticTokenProvider(),
            client,
            (_, _) => Task.CompletedTask);
        var provisioner = new GoogleWorkspaceProvisioner(options, executor);

        var spreadsheetId = await provisioner.CreateSpreadsheetAsync("DEV Sheet");
        var folderId = await provisioner.CreateFolderAsync("DEV Files");

        Assert.Equal("created-sheet", spreadsheetId);
        Assert.Equal("created-folder", folderId);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("DEV Sheet", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains(
            "application/vnd.google-apps.folder",
            handler.RequestBodies[1],
            StringComparison.Ordinal);
    }

    private sealed class ProvisioningHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = request.RequestUri!.AbsolutePath.Contains("/spreadsheets", StringComparison.Ordinal)
                ? """{"spreadsheetId":"created-sheet"}"""
                : """{"id":"created-folder"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StaticTokenProvider : IGoogleAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("test-access-token");

        public void Invalidate()
        {
        }
    }
}
