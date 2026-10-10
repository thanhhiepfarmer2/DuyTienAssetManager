using System.Net;
using System.Text;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleCategoryCatalogStoreTests
{
    [Fact]
    public async Task Noop_ledger_survives_adapter_reconstruction_and_blocks_reuse_with_changed_payload()
    {
        var h = new CategorySheetsHandler(); var f = new CategoryFixture(Store(h)); await f.Import();
        var doc = CategoryFixture.Doc(); var preview = await f.Service.PreviewAsync("ADMIN", doc);
        await f.Service.ImportAsync("ADMIN", doc, preview.Revision, preview.PreviewHash, "noop-key", true);
        var restarted = new CategoryCatalogService(Store(h), f.Auth, new DuyTien.AssetManagement.Infrastructure.Google.Concurrency.InProcessSingleWriterCoordinator(), f.Clock);
        var changed = doc with { SourceHash = "changed", Rows = doc.Rows.Select(r => r with { Name = r.Name + "x" }).ToArray() };
        var newer = await restarted.PreviewAsync("ADMIN", changed);
        await Assert.ThrowsAsync<CategoryConflictException>(() => restarted.ImportAsync("ADMIN", changed, newer.Revision, newer.PreviewHash, "noop-key", true));
        Assert.Equal(2, h.Posts); Assert.Equal(4, h.Tables["Categories"].Rows.Count);
    }
    [Fact]
    public async Task Atomic_batch_creates_schema_history_ledger_audit_and_pointer_together_and_reads_after_restart()
    {
        var h = new CategorySheetsHandler(); var store = Store(h); var f = new CategoryFixture(store); await f.Import();
        Assert.Equal(1, h.Posts); Assert.Equal(7, h.Tables.Count); Assert.Equal("COMPLETE", h.Tables["ImportJobs"].Rows[1][3]);
        Assert.Equal(4, h.Tables["Categories"].Rows.Count); Assert.Equal(4, h.Tables["ImportRows"].Rows.Count);
        var restarted = Store(h); var current = await restarted.ReadAsync(); Assert.Equal(3, current.ActiveNodes.Length); Assert.Equal(1, current.Revision);
        var d = CategoryFixture.Doc(CategoryFixture.Rows.Select(r => r with { Name = r.Name + " mới" }).ToArray(), "newversion"); var p = await f.Service.PreviewAsync("ADMIN", d);
        await f.Service.ImportAsync("ADMIN", d, p.Revision, p.PreviewHash, "newversion", true);
        Assert.Equal(7, h.Tables["Categories"].Rows.Count); Assert.Equal(3, h.Tables["TreeVersions"].Rows.Count);
        Assert.Equal(2, (await restarted.ReadAsync()).Revision); Assert.Equal("Nhánh bí mật", h.Tables["Categories"].Rows[1][5]);
        Assert.All(h.Uris, u => Assert.Contains("business-fake", u, StringComparison.Ordinal)); Assert.DoesNotContain(h.Uris, u => u.Contains("system-fake", StringComparison.Ordinal));
        Assert.DoesNotContain("formulaValue", h.LastBody, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Lost_response_after_atomic_commit_is_reconciled_without_duplicate_append()
    {
        var h = new CategorySheetsHandler { LoseResponseOnce = true }; var store = Store(h); var f = new CategoryFixture(store); await f.Import();
        Assert.Equal(1, h.Posts); Assert.Equal(3, (await store.ReadAsync()).ActiveNodes.Length);
        var p = await f.Service.PreviewAsync("ADMIN", CategoryFixture.Doc()); var r = await f.Service.ImportAsync("ADMIN", CategoryFixture.Doc(), p.Revision, p.PreviewHash, "again", true);
        Assert.True(r.AlreadyImported); Assert.Equal(2, h.Posts); Assert.Equal(4, h.Tables["Categories"].Rows.Count);
    }
    [Fact]
    public async Task Precommit_failure_preserves_empty_catalog_and_explicit_retry_succeeds()
    {
        var h = new CategorySheetsHandler { FailBeforeCommitOnce = true }; var f = new CategoryFixture(Store(h));
        await Assert.ThrowsAsync<ExternalStorageTransientException>(() => f.Import()); Assert.Empty(h.Tables);
        await f.Import(); Assert.Equal(3, (await f.Service.ListAsync("ADMIN")).Total);
    }
    [Theory]
    [InlineData("partial")]
    [InlineData("header")]
    [InlineData("wrong-range")]
    [InlineData("extra-audit")]
    public async Task Unknown_or_malformed_tables_are_not_overwritten(string fault)
    {
        var h = new CategorySheetsHandler(); var store = Store(h); var f = new CategoryFixture(store); await f.Import(); var posts = h.Posts;
        if (fault == "partial") h.Tables.Remove("ImportRows"); else if (fault == "header") h.Tables["Categories"].Rows[0][0] = "unknown"; else if (fault == "extra-audit") h.Tables["AuditEvents"].Rows.Add(["unknown", "unknown", "unknown", "unknown", "unknown", "{}", "{}", "unknown", "2026-10-10T00:00:00Z"]); else h.WrongRange = true;
        await Assert.ThrowsAsync<ExternalStorageException>(() => store.ReadAsync()); Assert.Equal(posts, h.Posts);
    }
    [Fact]
    public async Task Business_and_auth_spreadsheets_must_be_different()
    {
        var h = new CategorySheetsHandler(); var o = Options("business-fake"); var s = new GoogleCategoryCatalogStore(o, new(o, new Token(), new HttpClient(h)));
        await Assert.ThrowsAsync<ExternalStorageException>(() => s.ReadAsync()); Assert.Empty(h.Uris);
    }
    [Fact]
    public async Task Real_staging_size_fits_atomic_batch_and_second_version_diff_preserves_665_stable_ids()
    {
        var h = new CategorySheetsHandler(); var f = new CategoryFixture(Store(h)); var d = CategoryImportReader.Read(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "category-candidates.json")), "staging.json"); await f.Import(d);
        var first = (await f.Service.ListAsync("ADMIN")).Nodes.Select(n => n.CategoryId).ToArray();
        var second = d with { SourceHash = "test-new-version", Rows = d.Rows.Select((r, i) => i == 0 ? r with { Name = r.Name + " nháp" } : r).ToArray() };
        var p = await f.Service.PreviewAsync("ADMIN", second); await f.Service.ImportAsync("ADMIN", second, p.Revision, p.PreviewHash, "next", true);
        Assert.Equal(first, (await f.Service.ListAsync("ADMIN")).Nodes.Select(n => n.CategoryId)); Assert.Equal(1331, h.Tables["Categories"].Rows.Count);
        Assert.True(Encoding.UTF8.GetByteCount(h.LastBody) < 1800 * 1024);
    }
    private static GoogleWorkspaceOptions Options(string system = "system-fake") => new() { SpreadsheetId = "business-fake", SystemSpreadsheetId = system, OAuthClientId = "fake-client", OAuthClientSecret = "fake-client-config", OAuthRefreshToken = "fake-runtime-config", MaxRetryAttempts = 1 };
    private static GoogleCategoryCatalogStore Store(CategorySheetsHandler h) { var o = Options(); return new(o, new(o, new Token(), new HttpClient(h))); }
    private sealed class Token : IGoogleAccessTokenProvider { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fake-adapter-token"); public void Invalidate() { } }
    internal sealed class Table(int id, int columns) { public int Id { get; } = id; public int Columns { get; } = columns; public List<string[]> Rows { get; } = []; }
    internal sealed class CategorySheetsHandler : HttpMessageHandler
    {
        public Dictionary<string, Table> Tables { get; } = new(StringComparer.Ordinal); public List<string> Uris { get; } = [];
        public int Posts { get; private set; }
        public string LastBody { get; private set; } = "";
        public bool LoseResponseOnce { get; set; }
        public bool FailBeforeCommitOnce { get; set; }
        public bool WrongRange { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uris.Add(request.RequestUri!.AbsoluteUri);
            if (request.Method == HttpMethod.Get)
            {
                if (!request.RequestUri.AbsolutePath.Contains("values:batchGet", StringComparison.Ordinal))
                    return Response(new { sheets = Tables.Select(p => new { properties = new { sheetId = p.Value.Id, title = p.Key, gridProperties = new { rowCount = Math.Max(100, p.Value.Rows.Count), columnCount = p.Value.Columns } } }).ToArray() });
                var ranges = request.RequestUri.Query.TrimStart('?').Split('&').Where(q => q.StartsWith("ranges=", StringComparison.Ordinal)).Select(q => Uri.UnescapeDataString(q[7..])).ToArray();
                return Response(new { valueRanges = ranges.Select(range => new { range = WrongRange ? "'WRONG'!A1" : range, values = Tables[range.Split('!')[0].Trim('\'')].Rows.ToArray() }).ToArray() });
            }
            Posts++; LastBody = await request.Content!.ReadAsStringAsync(ct);
            if (FailBeforeCommitOnce) { FailBeforeCommitOnce = false; throw new HttpRequestException("Synthetic precommit failure"); }
            using var json = JsonDocument.Parse(LastBody);
            foreach (var r in json.RootElement.GetProperty("requests").EnumerateArray())
            {
                if (r.TryGetProperty("addSheet", out var add)) { var p = add.GetProperty("properties"); Tables.Add(p.GetProperty("title").GetString()!, new(p.GetProperty("sheetId").GetInt32(), p.GetProperty("gridProperties").GetProperty("columnCount").GetInt32())); }
                if (r.TryGetProperty("updateCells", out var update))
                {
                    var start = update.GetProperty("start"); var table = Tables.Values.Single(t => t.Id == start.GetProperty("sheetId").GetInt32()); var index = start.GetProperty("rowIndex").GetInt32();
                    foreach (var row in update.GetProperty("rows").EnumerateArray()) { var values = row.GetProperty("values").EnumerateArray().Select(v => v.GetProperty("userEnteredValue").GetProperty("stringValue").GetString()!).ToArray(); if (index == table.Rows.Count) table.Rows.Add(values); else table.Rows[index] = values; index++; }
                }
            }
            if (LoseResponseOnce) { LoseResponseOnce = false; throw new HttpRequestException("Synthetic lost response after commit"); }
            return Response(new { });
        }
        private static HttpResponseMessage Response(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
