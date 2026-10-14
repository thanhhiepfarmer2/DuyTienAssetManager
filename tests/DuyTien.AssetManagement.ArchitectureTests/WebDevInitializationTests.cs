using System.Net;
using System.Text;
using System.Text.Json;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class WebDevInitializationTests
{
    [Fact]
    public async Task Init_creates_absent_modules_once_preserves_legacy_and_is_noop_on_repeat()
    {
        var r = new Rig(); r.Legacy(); var poc = JsonSerializer.Serialize(r.H.Tables["TASK004_POC"].Rows);
        var first = await r.Init().InitializeAsync(); Assert.Equal(23, first.CreatedTables); Assert.False(first.NoOp); Assert.Equal(1, r.H.Posts);
        var second = await r.Init().InitializeAsync(); Assert.True(second.NoOp); Assert.Equal(1, r.H.Posts);
        Assert.Equal(poc, JsonSerializer.Serialize(r.H.Tables["TASK004_POC"].Rows)); Assert.Empty(r.H.Tables["Sheet1"].Rows);
        Assert.False(r.H.Tables.ContainsKey("SchemaVersions")); Assert.False(r.H.Tables.ContainsKey("TreeRegistryDetails"));
    }
    [Fact]
    public async Task Lost_init_response_reconciles_actual_schema_without_second_write()
    { var r = new Rig(); r.H.LoseResponseOnce = true; var result = await r.Init().InitializeAsync(); Assert.Equal(23, result.CreatedTables); Assert.Equal(1, r.H.Posts); Assert.True((await r.Init().InitializeAsync()).NoOp); }
    [Theory]
    [InlineData("InventorySchemaVersions")]
    [InlineData("MediaSchemaVersions")]
    [InlineData("Assets")]
    [InlineData("Categories")]
    public async Task Partial_schema_is_rejected_before_write(string title)
    { var r = new Rig(); r.Add(title, ["bad", "header"]); await Assert.ThrowsAsync<ExternalStorageException>(() => r.Init().InitializeAsync()); Assert.Equal(0, r.H.Posts); }
    [Fact]
    public async Task Nonempty_legacy_default_sheet_is_not_ignored_or_deleted()
    { var r = new Rig(); r.Add("Sheet1", ["UNKNOWN OWNER"]); await Assert.ThrowsAsync<ExternalStorageException>(() => r.Init().InitializeAsync()); Assert.Equal(0, r.H.Posts); Assert.Equal("UNKNOWN OWNER", r.H.Tables["Sheet1"].Rows[0][0]); }
    [Fact]
    public async Task Unknown_legacy_poc_row_fails_closed()
    { var r = new Rig(); r.Legacy(); r.H.Tables["TASK004_POC"].Rows.Add(["ITEM", "unknown", "", "", "", "", "READY", "", "1"]); await Assert.ThrowsAsync<ExternalStorageException>(() => r.Init().InitializeAsync()); Assert.Equal(0, r.H.Posts); }
    [Fact]
    public async Task Wrong_dev_acl_prevents_schema_write()
    { var r = new Rig { Shared = true }; await Assert.ThrowsAsync<ExternalStorageException>(() => r.Init().InitializeAsync()); Assert.Equal(0, r.H.Posts); }
    [Fact]
    public async Task Unknown_table_is_still_blocked()
    { var r = new Rig(); r.Add("OtherRegistry", ["Owner"]); await Assert.ThrowsAsync<ExternalStorageException>(() => r.Init().InitializeAsync()); Assert.Equal(0, r.H.Posts); }
    [Fact]
    public async Task Demo_seed_rejects_unverified_or_missing_root_preserving_existing_data()
    { var r = new Rig(); await r.Init().InitializeAsync(); var posts = r.H.Posts; await Assert.ThrowsAsync<ExternalStorageException>(() => r.Init().SeedFakeReferencesAsync(["company-root"])); Assert.Equal(posts, r.H.Posts); Assert.Single(r.H.Tables["CodeRules"].Rows); }
    [Fact]
    public async Task Labeled_fake_root_reference_seed_is_idempotent_without_overwriting_other_references()
    {
        var r = new Rig(); await r.Init().InitializeAsync();
        var f = new CategoryFixture(); var service = new DuyTien.AssetManagement.Application.Categories.CategoryCatalogService(new GoogleCategoryCatalogStore(r.O, r.Api()), f.Auth, new DuyTien.AssetManagement.Infrastructure.Google.Concurrency.InProcessSingleWriterCoordinator(), f.Clock);
        var doc = CategoryFixture.Doc([new("F1-F3 Mã ngang", 2, "1", null, 1, "GIẢ TASK-014 nguồn", "WFA", "FAKE_TASK014_V1 fixture")]);
        var p = await service.PreviewAsync("ADMIN", doc); await service.ImportAsync("ADMIN", doc, p.Revision, p.PreviewHash, "fake-root", true);
        var c = await new GoogleCategoryCatalogStore(r.O, r.Api()).ReadAsync(); var root = c.ActiveNodes.Single().CategoryId;
        Assert.False((await r.Init().SeedFakeReferencesAsync([root])).NoOp); var posts = r.H.Posts;
        var before = JsonSerializer.Serialize(r.H.Tables["CodeRules"].Rows); Assert.True((await r.Init().SeedFakeReferencesAsync([root])).NoOp); Assert.Equal(posts, r.H.Posts); Assert.Equal(before, JsonSerializer.Serialize(r.H.Tables["CodeRules"].Rows));
        Assert.Equal("DEMO", r.H.Tables["CodeRules"].Rows[1][4]); Assert.StartsWith("GIẢ", r.H.Tables["OrgUnits"].Rows[1][2]);
    }
    private sealed class Rig
    {
        public GoogleCategoryCatalogStoreTests.CategorySheetsHandler H { get; } = new();
        public bool Shared { get; set; }
        public GoogleWorkspaceOptions O { get; } = new() { SpreadsheetId = "business-fake", SystemSpreadsheetId = "system-fake", DriveFolderId = "folder-fake", OAuthClientId = "fake", OAuthClientSecret = "fake", OAuthRefreshToken = "fake", MaxRetryAttempts = 1 };
        public GoogleApiRequestExecutor Api() => new(O, new Token(), new HttpClient(new Router(this)));
        public GoogleWebDevInitializer Init()
        {
            var api = Api(); var c = new GoogleCategoryCatalogStore(O, api); var a = new GoogleAssetCatalogStore(O, api);
            return new(O, api, new(O, api, c, a, new(O, api), new(O, api)), a);
        }
        public void Add(string title, string[] row) { var t = new GoogleCategoryCatalogStoreTests.Table(H.Tables.Count + 1, Math.Max(26, row.Length)); t.Rows.Add(row); H.Tables.Add(title, t); }
        public void Legacy()
        { H.Tables.Add("Sheet1", new(1, 26)); Add("TASK004_POC", ["RecordType", "Key", "Value1", "Value2", "Value3", "Value4", "State", "TimestampUtc", "Version"]); H.Tables["TASK004_POC"].Rows.Add(["COUNTER", "TASK004", "2", "POC", "", "", "READY", "2026-10-14T00:00:00Z", "2"]); }
    }
    private sealed class Router(Rig rig) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker inner = new(rig.H, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.StartsWith("/drive/v3/files/", StringComparison.Ordinal))
            {
                var id = request.RequestUri.Segments.Last(); var name = id == "business-fake" ? "DuyTien Assets DEV" : id == "system-fake" ? "DuyTien Asset Management - DEV System" : "DuyTien Assets DEV Files";
                var type = id == "folder-fake" ? "folder" : "spreadsheet";
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { name, mimeType = "application/vnd.google-apps." + type, ownedByMe = true, trashed = false, shared = rig.Shared, capabilities = new { canEdit = true }, permissions = new[] { new { type = "user", role = "owner" } } }), Encoding.UTF8, "application/json") };
            }
            var response = await inner.SendAsync(request, ct);
            if (request.Method == HttpMethod.Get && request.RequestUri.Query.Contains("valueRenderOption=FORMULA", StringComparison.Ordinal))
            {
                using var d = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var ranges = d.RootElement.GetProperty("valueRanges").EnumerateArray().Select(v => new { range = v.GetProperty("range").GetString() + "!A1:Z1000", values = v.GetProperty("values").Clone() }).ToArray();
                response.Content = new StringContent(JsonSerializer.Serialize(new { valueRanges = ranges }), Encoding.UTF8, "application/json");
            }
            return response;
        }
    }
    private sealed class Token : IGoogleAccessTokenProvider { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fake"); public void Invalidate() { } }
}
