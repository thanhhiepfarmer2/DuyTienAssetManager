using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Application.ExternalStorage;
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

    [Fact]
    public async Task Batch_read_loads_all_six_auth_tables_without_six_individual_requests()
    {
        var handler = new SystemSheetHandler { BatchResponse = MakeBatch(seed: true) };
        var state = await CreateStore(handler).ReadAsync(value => value);

        Assert.Equal("USER-1", Assert.Single(state.Users).UserId);
        Assert.Equal(AuthRole.Admin, Assert.Single(state.RoleAssignments).RoleCode);
        Assert.True(Assert.Single(state.RoleAssignmentScopes).IncludeDescendants);
        Assert.Equal(AuthClientType.Mobile, Assert.Single(state.Sessions).ClientType);
        Assert.Equal(RefreshTokenState.Active, Assert.Single(state.RefreshTokens).State);
        Assert.Equal("LOGIN", Assert.Single(state.SecurityEvents).Action);
        var uri = Assert.Single(handler.RequestUris);
        Assert.Contains("/values:batchGet?", uri, StringComparison.Ordinal);
        Assert.Equal(6, uri.Split("ranges=", StringSplitOptions.None).Length - 1);
        Assert.Empty(handler.RequestBodies);
    }

    [Fact]
    public async Task No_change_mutation_keeps_persisted_data_and_does_not_write_or_fetch_metadata()
    {
        var handler = new SystemSheetHandler { BatchResponse = MakeBatch(seed: true) };
        var result = await CreateStore(handler).MutateAsync(state => state.Users.Single().UserId);
        Assert.Equal("USER-1", result);
        Assert.Single(handler.RequestUris);
        Assert.Empty(handler.RequestBodies);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task A_change_in_any_auth_table_is_persisted(int table)
    {
        var handler = new SystemSheetHandler { BatchResponse = MakeBatch(seed: true) };
        await CreateStore(handler).MutateAsync(state =>
        {
            switch (table)
            {
                case 0: state.Users[0] = state.Users[0] with { SecurityVersion = 2 }; break;
                case 1: state.RoleAssignments[0] = state.RoleAssignments[0] with { State = RoleAssignmentState.Revoked }; break;
                case 2: state.RoleAssignmentScopes[0] = state.RoleAssignmentScopes[0] with { ScopeId = "OTHER-SCOPE" }; break;
                case 3: state.Sessions[0] = state.Sessions[0] with { RevokedReason = "test-revoke" }; break;
                case 4: state.RefreshTokens[0] = state.RefreshTokens[0] with { State = RefreshTokenState.Rotated }; break;
                case 5: state.SecurityEvents[0] = state.SecurityEvents[0] with { Action = "LOGOUT" }; break;
            }
            return true;
        });
        Assert.Single(handler.RequestBodies);
    }

    [Theory]
    [InlineData("missing-table")]
    [InlineData("wrong-spreadsheet")]
    [InlineData("wrong-header")]
    [InlineData("wrong-order")]
    [InlineData("empty-values")]
    public async Task Incomplete_or_mismatched_batch_fails_closed_and_never_overwrites_data(string fault)
    {
        var handler = new SystemSheetHandler { BatchResponse = MakeBatch(fault: fault) };
        var store = CreateStore(handler);
        await Assert.ThrowsAsync<ExternalStorageException>(() => store.ReadAsync(state => state.Users.Count));
        await Assert.ThrowsAsync<ExternalStorageException>(() => store.MutateAsync(state => true));
        Assert.Empty(handler.RequestBodies);
    }

    [Fact]
    public async Task Fresh_Google_session_validation_reads_current_batch_without_entering_business_writer()
    {
        var now = new DateTimeOffset(2026, 10, 16, 1, 0, 0, TimeSpan.Zero);
        var handler = new SystemSheetHandler { BatchResponse = SessionBatch(now) };
        var writer = new ValidationWriter { BlockWrites = true }; var store = CreateStore(handler, writer);
        Assert.True(((IAuthSnapshotValidation)store).CanValidateReadOnly);
        var service = new AuthApplicationService(store, new TestPasswordHashService(), new(), new ManualTimeProvider(now));
        Assert.NotNull(await service.ValidateAccessTokenAsync("FAKE-session"));
        Assert.Equal(0, writer.Calls); Assert.Single(handler.RequestUris); Assert.Empty(handler.RequestBodies);
        // A second request must see a central revocation rather than reuse an auth snapshot across requests.
        handler.BatchResponse = SessionBatch(now, "locked");
        Assert.Null(await service.ValidateAccessTokenAsync("FAKE-session"));
        Assert.Equal(2, handler.RequestUris.Count); Assert.Empty(handler.RequestBodies);
    }
    [Theory]
    [InlineData("expired-idle")][InlineData("expired-absolute")][InlineData("revoked")][InlineData("security-version")][InlineData("locked")]
    public async Task Google_read_only_validation_rejects_stale_authority_without_writes(string fault)
    {
        var now = new DateTimeOffset(2026, 10, 16, 1, 0, 0, TimeSpan.Zero);
        var handler = new SystemSheetHandler { BatchResponse = SessionBatch(now, fault) }; var writer = new ValidationWriter { BlockWrites = true };
        var service = new AuthApplicationService(CreateStore(handler, writer), new TestPasswordHashService(), new(), new ManualTimeProvider(now));
        Assert.Null(await service.ValidateAccessTokenAsync("FAKE-session")); Assert.Equal(0, writer.Calls); Assert.Empty(handler.RequestBodies);
    }
    [Fact]
    public async Task Five_minute_touch_still_reloads_under_writer_and_preserves_idle_and_absolute_policy()
    {
        var now = new DateTimeOffset(2026, 10, 16, 1, 0, 0, TimeSpan.Zero);
        var handler = new SystemSheetHandler { BatchResponse = SessionBatch(now, "touch") }; var writer = new ValidationWriter();
        var service = new AuthApplicationService(CreateStore(handler, writer), new TestPasswordHashService(), new(), new ManualTimeProvider(now));
        Assert.NotNull(await service.ValidateAccessTokenAsync("FAKE-session")); Assert.Equal(1, writer.Calls);
        Assert.Equal(2, handler.RequestUris.Count(u => u.Contains("values:batchGet", StringComparison.Ordinal)));
        using var body = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var sessionRow = body.RootElement.GetProperty("requests").EnumerateArray().First(x => x.TryGetProperty("appendCells", out var a) && a.GetProperty("sheetId").GetInt32() == 10).GetProperty("appendCells").GetProperty("rows")[0].GetProperty("values");
        string Value(int index) => sessionRow[index].GetProperty("userEnteredValue").GetProperty("stringValue").GetString()!;
        Assert.Equal(now, DateTimeOffset.Parse(Value(8))); Assert.Equal(now.AddMinutes(30), DateTimeOffset.Parse(Value(9))); Assert.Equal(now.AddHours(8), DateTimeOffset.Parse(Value(10)));
    }
    private static string SessionBatch(DateTimeOffset now, string? fault = null)
    {
        var json = JsonNode.Parse(MakeBatch(seed: true))!; var rows = json["valueRanges"]!.AsArray();
        var user = rows[0]!["values"]![1]!; var session = rows[3]!["values"]![1]!;
        session[2] = "Web"; session[3] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("FAKE-session")));
        session[8] = (fault == "touch" ? now.AddMinutes(-5) : now.AddMinutes(-1)).ToString("O"); session[9] = now.AddMinutes(29).ToString("O"); session[10] = now.AddHours(8).ToString("O");
        if (fault == "expired-idle") session[9] = now.ToString("O");
        if (fault == "expired-absolute") session[10] = now.ToString("O");
        if (fault == "revoked") session[11] = now.ToString("O");
        if (fault == "security-version") user[10] = "2";
        if (fault == "locked") user[5] = "Locked";
        return json.ToJsonString();
    }
    private sealed class ValidationWriter : ISingleWriterCoordinator
    {
        public int Calls; public bool BlockWrites;
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
        { Calls++; if (BlockWrites) throw new InvalidOperationException("Synthetic busy writer must not block fresh auth reads"); return operation(ct); }
    }

    private static string MakeBatch(bool seed = false, string? fault = null)
    {
        string[][] headers =
        [
            ["UserId", "Username", "NormalizedUsername", "DisplayName", "PasswordHash", "State", "MustChangePassword", "FailedAccessCount", "FailedWindowStartedAt", "LockoutEndAt", "SecurityVersion", "PasswordChangedAt", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Version"],
            ["AssignmentId", "UserId", "RoleId", "PositionId", "Source", "State", "EffectiveFrom", "EffectiveTo", "CreatedAt", "CreatedBy", "Version"],
            ["ScopeGrantId", "AssignmentId", "ScopeType", "ScopeId", "IncludeDescendants", "Version"],
            ["SessionId", "UserId", "ClientType", "AccessTokenHash", "RefreshFamilyId", "SecurityVersion", "RememberMe", "CreatedAt", "LastSeenAt", "IdleExpiresAt", "AbsoluteExpiresAt", "RevokedAt", "RevokedReason", "Version"],
            ["RefreshTokenId", "SessionId", "RefreshFamilyId", "TokenHash", "State", "CreatedAt", "IdleExpiresAt", "AbsoluteExpiresAt", "RotatedAt", "ReplacedByRefreshTokenId", "RevokedAt", "RevokedReason", "Version"],
            ["EventId", "ActorUserId", "TargetUserId", "SessionId", "Action", "Outcome", "FailureCode", "CorrelationId", "Timestamp"]
        ];
        const string timestamp = "2026-10-09T08:00:00.0000000Z";
        string[][] data =
        [
            ["USER-1", "demo", "DEMO", "Demo", "FAKE-HASH", "Active", "False", "0", "", "", "1", timestamp, timestamp, "test", timestamp, "test", "1"],
            ["ASSIGN-1", "USER-1", "Admin", "", "MANUAL", "Active", timestamp, "", timestamp, "test", "1"],
            ["SCOPE-1", "ASSIGN-1", "CategorySubtree", "ALL", "True", "1"],
            ["SESSION-1", "USER-1", "Mobile", "FAKE-ACCESS-HASH", "FAMILY-1", "1", "False", timestamp, timestamp, timestamp, timestamp, "", "", "1"],
            ["REFRESH-1", "SESSION-1", "FAMILY-1", "FAKE-REFRESH-HASH", "Active", timestamp, timestamp, timestamp, "", "", "", "", "1"],
            ["EVENT-1", "USER-1", "USER-1", "SESSION-1", "LOGIN", "Success", "", "CORRELATION-1", timestamp]
        ];
        string[] titles = ["Users", "UserRoleAssignments", "RoleAssignmentScopes", "Sessions", "RefreshTokens", "SecurityEvents"];
        if (fault == "wrong-header") headers[0][0] = "InvalidHeader";
        if (fault == "wrong-order") (titles[0], titles[1]) = (titles[1], titles[0]);
        var valueRanges = titles.Select((title, index) => new
        {
            range = $"'{title}'!A1:Q1000",
            values = fault == "empty-values" && index == 0 ? Array.Empty<string[]>() :
                seed ? new[] { headers[index], data[index] } : new[] { headers[index] }
        }).ToArray();
        return JsonSerializer.Serialize(new
        {
            spreadsheetId = fault == "wrong-spreadsheet" ? "unexpected-sheet" : "system-sheet-dev",
            valueRanges = fault == "missing-table" ? valueRanges[..5] : valueRanges
        });
    }

    private static GoogleAuthSystemStore CreateStore(SystemSheetHandler handler, ISingleWriterCoordinator? coordinator = null)
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
        return new GoogleAuthSystemStore(options, executor, coordinator ?? new InProcessSingleWriterCoordinator());
    }

    private sealed class SystemSheetHandler : HttpMessageHandler
    {
        public string BatchResponse { get; set; } = MakeBatch();
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

            if (request.RequestUri.AbsolutePath.Contains("/values:batchGet", StringComparison.Ordinal))
            {
                return Json(BatchResponse);
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
