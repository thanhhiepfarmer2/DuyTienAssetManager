using System.Net;
using System.Text;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleAuthSystemStoreTests
{
    [Fact]
    public async Task Init_targets_system_spreadsheet_and_seeds_refresh_marker_schema_and_catalog()
    {
        var handler = new SystemSheetHandler();
        var store = CreateStore(handler);

        var result = await store.InitializeAsync("operator");

        Assert.True(result.Changed);
        Assert.Contains(handler.RequestUris, uri => uri.Contains("system-sheet-dev", StringComparison.Ordinal));
        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains("RefreshTokenId", body, StringComparison.Ordinal);
        Assert.Contains(AuthRole.Admin, body, StringComparison.Ordinal);
        Assert.Contains(AuthPermission.AuthzAssign, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mutation_rewrites_auth_tables_in_one_batch_without_plaintext_password()
    {
        var handler = new SystemSheetHandler();
        var store = CreateStore(handler);
        const string hash = "FRAMEWORK-HASH-WITHOUT-PLAINTEXT";
        await store.MutateAsync(state =>
        {
            var now = new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
            state.Users.Add(new AuthUser(
                "USER-1", "demo", "DEMO", "Demo", hash, AuthUserState.Active,
                true, 0, null, null, 1, now, now, "operator", now, "operator", 1));
            return true;
        });

        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains(hash, body, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-password", body, StringComparison.Ordinal);
        Assert.Contains("appendCells", body, StringComparison.Ordinal);
        Assert.Contains("updateCells", body, StringComparison.Ordinal);
    }

    private static GoogleAuthSystemStore CreateStore(SystemSheetHandler handler)
    {
        var options = new GoogleWorkspaceOptions
        {
            OAuthClientId = "client",
            OAuthClientSecret = "secret",
            OAuthRefreshToken = "refresh",
            SystemSpreadsheetId = "system-sheet-dev",
            SheetsApiBaseUrl = "https://example.test/v4/",
            MaxRetryAttempts = 1
        };
        var client = new HttpClient(handler);
        var executor = new GoogleApiRequestExecutor(
            options,
            new StaticTokenProvider(),
            client,
            (_, _) => Task.CompletedTask);
        return new GoogleAuthSystemStore(options, executor, new InProcessSingleWriterCoordinator());
    }

    private sealed class SystemSheetHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];
        public List<string> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!.ToString());
            if (request.Method == HttpMethod.Post)
            {
                RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Json("{}");
            }

            if (request.RequestUri.AbsolutePath.Contains("/values/", StringComparison.Ordinal))
            {
                return Json("{\"values\":[[\"header\"]]}");
            }

            return Json(Metadata);
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        private const string Metadata = """
            {
              "spreadsheetId":"system-sheet-dev",
              "sheets":[
                {"properties":{"sheetId":1,"title":"SystemSchemaVersions"}},
                {"properties":{"sheetId":2,"title":"Users"}},
                {"properties":{"sheetId":3,"title":"Roles"}},
                {"properties":{"sheetId":4,"title":"Permissions"}},
                {"properties":{"sheetId":5,"title":"RolePermissions"}},
                {"properties":{"sheetId":6,"title":"Positions"}},
                {"properties":{"sheetId":7,"title":"PositionRoleTemplates"}},
                {"properties":{"sheetId":8,"title":"UserRoleAssignments"}},
                {"properties":{"sheetId":9,"title":"RoleAssignmentScopes"}},
                {"properties":{"sheetId":10,"title":"Sessions"}},
                {"properties":{"sheetId":11,"title":"RefreshTokens"}},
                {"properties":{"sheetId":12,"title":"SecurityEvents"}},
                {"properties":{"sheetId":13,"title":"AuthOperations"}}
              ]
            }
            """;
    }

    private sealed class StaticTokenProvider : IGoogleAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("access-token");

        public void Invalidate()
        {
        }
    }
}
