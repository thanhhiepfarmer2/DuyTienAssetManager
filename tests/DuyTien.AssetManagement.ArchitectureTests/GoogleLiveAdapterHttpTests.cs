using System.Net;
using System.Text;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleAdapterHttpTests
{
    [Fact]
    public async Task Readiness_PerformsAuthenticatedSheetsAndDriveRequests()
    {
        var handler = new DelegateHandler(request =>
        {
            Assert.Equal("Bearer test-access-token", request.Headers.Authorization?.ToString());
            if (request.RequestUri!.AbsolutePath.Contains("/spreadsheets/", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """
                    {"spreadsheetId":"sheet-dev","properties":{"title":"dev"},"sheets":[]}
                    """);
            }

            if (request.RequestUri.AbsolutePath.Contains("/drive/v3/files/folder-dev", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """
                    {
                      "id":"folder-dev",
                      "name":"TASK004 dev",
                      "mimeType":"application/vnd.google-apps.folder",
                      "trashed":false,
                      "capabilities":{"canAddChildren":true,"canDownload":true}
                    }
                    """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });
        var options = CompleteOptions();
        using var client = new HttpClient(handler);
        var executor = new GoogleApiRequestExecutor(
            options,
            new StaticTokenProvider(),
            client,
            (_, _) => Task.CompletedTask);
        var sheets = new GoogleSheetsDataStore(options, executor);
        var drive = new GoogleDriveFileStore(options, executor);

        var sheetsReadiness = await sheets.CheckReadinessAsync();
        var driveReadiness = await drive.CheckReadinessAsync();

        Assert.True(sheetsReadiness.LiveConnectionVerified);
        Assert.True(driveReadiness.LiveConnectionVerified);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SafeRead_Retries429AndHonorsAttemptLimit()
    {
        var handlerRequestCount = -1;
        var handler = new DelegateHandler(_ =>
        {
            handlerRequestCount++;
            return handlerRequestCount == 0
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });
        var options = CompleteOptions();
        using var client = new HttpClient(handler);
        var executor = new GoogleApiRequestExecutor(
            options,
            new StaticTokenProvider(),
            client,
            (_, _) => Task.CompletedTask);

        using var response = await executor.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/read"),
            GoogleRequestSemantics.SafeRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SafeRead_RetriesNetworkTimeout()
    {
        var first = true;
        var handler = new AsyncDelegateHandler(_ =>
        {
            if (first)
            {
                first = false;
                return Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var options = CompleteOptions();
        using var client = new HttpClient(handler);
        var executor = new GoogleApiRequestExecutor(
            options,
            new StaticTokenProvider(),
            client,
            (_, _) => Task.CompletedTask);

        using var response = await executor.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/read"),
            GoogleRequestSemantics.SafeRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task DriveFileAccess_GrantedFileSucceedsAndUngrantedFileIsDenied()
    {
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                    "/drive/v3/files/granted-file",
                    StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"id":"granted-file","trashed":false}""");
            }

            if (request.RequestUri.AbsolutePath.EndsWith(
                    "/drive/v3/files/ungranted-file",
                    StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });
        var options = CompleteOptions();
        using var client = new HttpClient(handler);
        var executor = new GoogleApiRequestExecutor(
            options,
            new StaticTokenProvider(),
            client,
            (_, _) => Task.CompletedTask);
        var verifier = new GoogleDriveAccessVerifier(options, executor);

        await verifier.EnsureAccessibleAsync("granted-file");
        var exception = await Assert.ThrowsAsync<ExternalStorageAccessDeniedException>(
            () => verifier.EnsureAccessibleAsync("ungranted-file"));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    private static GoogleWorkspaceOptions CompleteOptions() => new()
    {
        OAuthClientId = "client",
        OAuthClientSecret = "secret",
        OAuthRefreshToken = "refresh",
        SpreadsheetId = "sheet-dev",
        DriveFolderId = "folder-dev",
        SheetsApiBaseUrl = "https://example.test/v4/",
        DriveApiBaseUrl = "https://example.test/",
        InitialRetryDelayMilliseconds = 1,
        MaxRetryAttempts = 3
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StaticTokenProvider : IGoogleAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("test-access-token");

        public void Invalidate()
        {
        }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(handler(request));
        }
    }

    private sealed class AsyncDelegateHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return handler(request);
        }
    }
}
