using System.Text.Json;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class InventoryGoogleTests
{
    [Fact]
    public async Task Atomic_delta_chunks_audit_progress_restart_sort_and_no_formula_execution()
    { var r = await Rig.Start(); var req = r.F.Request(205); req.Document.Rows[0].Cells[4] = "=FORMULA_GIẢ"; var p = await r.Service.PreviewAsync("ADMIN", req); var j = await r.Service.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "start", true); Assert.Empty((await r.Store.ReadAsync()).Items); Assert.Equal(8, r.Handler.Tables.Count); j = await r.Service.CommitChunkAsync("ADMIN", j.JobId, 0, "c1"); Assert.Equal("PARTIAL", j.State); Assert.Equal(100, (await r.Store.ReadAsync()).Items.Count); var restarted = r.NewService(); j = await restarted.CommitChunkAsync("ADMIN", j.JobId, 100, "c2"); j = await restarted.CommitChunkAsync("ADMIN", j.JobId, 200, "c3"); Assert.Equal("COMPLETE", j.State); Assert.Equal(206, r.Handler.Tables["Items"].Rows.Count); Assert.DoesNotContain("formulaValue", r.Handler.LastBody, StringComparison.Ordinal); Assert.True(System.Text.Encoding.UTF8.GetByteCount(r.Handler.LastBody) < 1800 * 1024); var t = r.Handler.Tables["Items"]; t.Rows.Reverse(1, t.Rows.Count - 1); Assert.Equal(205, (await r.Store.ReadAsync()).Items.Count); var posts = r.Handler.Posts; await restarted.CommitChunkAsync("ADMIN", j.JobId, 200, "c3"); Assert.Equal(posts, r.Handler.Posts); Assert.All(r.Handler.Uris, u => Assert.Contains("business-fake", u, StringComparison.Ordinal)); }
    [Fact]
    public async Task Lost_chunk_response_reconciles_one_batch_and_no_duplicate()
    { var r = await Rig.Start(); var req = r.F.Request(); var p = await r.Service.PreviewAsync("ADMIN", req); var j = await r.Service.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "start", true); var n = r.Handler.Posts; r.Handler.LoseResponseOnce = true; var actual = await r.Service.CommitChunkAsync("ADMIN", j.JobId, 0, "c"); Assert.Equal(n + 1, r.Handler.Posts); Assert.Equal("COMPLETE", actual.State); Assert.Single((await r.Store.ReadAsync()).Items); await r.NewService().CommitChunkAsync("ADMIN", j.JobId, 0, "c"); Assert.Equal(n + 1, r.Handler.Posts); }
    [Theory]
    [InlineData("partial")]
    [InlineData("header")]
    [InlineData("materialized")]
    [InlineData("audit")]
    [InlineData("detail")]
    [InlineData("range")]
    public async Task Unknown_schema_or_journal_tamper_fails_closed(string fault)
    { var r = await Rig.Start(); var req = r.F.Request(); var p = await r.Service.PreviewAsync("ADMIN", req); var j = await r.Service.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "start", true); await r.Service.CommitChunkAsync("ADMIN", j.JobId, 0, "c"); var count = r.Handler.Posts; if (fault == "partial") r.Handler.Tables.Remove("ItemImportPlan"); else if (fault == "header") r.Handler.Tables["Items"].Rows[0][0] = "unknown"; else if (fault == "materialized") r.Handler.Tables["Items"].Rows[1][1] = r.Handler.Tables["Items"].Rows[1][1].Replace("DRAFT_DEMO", "APPROVED", StringComparison.Ordinal); else if (fault == "audit") r.Handler.Tables["InventoryAuditEvents"].Rows[1][3] = "unknown"; else if (fault == "detail") r.Handler.Tables["InventoryDetails"].Rows.RemoveAt(1); else r.Handler.WrongRange = true; await Assert.ThrowsAsync<ExternalStorageException>(() => r.Store.ReadAsync()); Assert.Equal(count, r.Handler.Posts); }
    [Fact]
    public async Task Stock_snapshot_atomic_persistence_and_version_replayed_after_adapter_restart()
    { var r = await Rig.Start(); var req = r.F.Request(); var p = await r.Service.PreviewAsync("ADMIN", req); var j = await r.Service.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "start", true); await r.Service.CommitChunkAsync("ADMIN", j.JobId, 0, "c"); var s = await r.Store.ReadAsync(); var b = new StockWriteRequest(s.Items.Single().ItemId, "LOC-A", "cái", null, null, r.F.Base.Clock.GetUtcNow(), "GIẢ", 0, "GIẢ snapshot"); await r.Service.WriteStockAsync("ADMIN", b, "b"); var current = (await r.Store.ReadAsync()).Balances.Single(); Assert.Null(current.Quantity); Assert.Equal("UNKNOWN", current.StockStatus); await r.NewService().WriteStockAsync("ADMIN", b, "b"); Assert.Single((await r.Store.ReadAsync()).Balances); }
    [Fact]
    public async Task Item_registry_keeps_existing_tree_metadata_guard_fail_closed()
    { var r = await Rig.Start(); var req = r.F.Request(); var p = await r.Service.PreviewAsync("ADMIN", req); await r.Service.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "start", true); var tree = new GoogleTreeMutationStore(r.Options, r.Executor, new(r.Options, r.Executor), new(r.Options, r.Executor)); var posts = r.Handler.Posts; await Assert.ThrowsAsync<ExternalStorageException>(() => tree.ReadAsync()); Assert.Equal(posts, r.Handler.Posts); }
    [Fact]
    public async Task Large_text_rows_reduce_chunk_by_byte_budget_and_reach_complete()
    { var r = await Rig.Start(); var req = r.F.Request(110); foreach (var row in req.Document.Rows) row.Cells[4] = new string('X', 1800); var p = await r.Service.PreviewAsync("ADMIN", req); var j = await r.Service.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "s", true); j = await r.Service.CommitChunkAsync("ADMIN", j.JobId, 0, "c0"); Assert.InRange(j.Processed, 1, 99); while (j.State != "COMPLETE") { Assert.True(System.Text.Encoding.UTF8.GetByteCount(r.Handler.LastBody) < 1800 * 1024); j = await r.Service.CommitChunkAsync("ADMIN", j.JobId, j.Processed, "c" + j.Processed); } Assert.Equal(110, (await r.Store.ReadAsync()).Items.Count); }
    [Fact]
    public async Task Sorting_materialized_plan_does_not_change_chunk_sequence_or_progress()
    { var r = await Rig.Start(); var req = r.F.Request(205); var p = await r.Service.PreviewAsync("ADMIN", req); var j = await r.Service.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "s", true); var table = r.Handler.Tables["ItemImportPlan"]; table.Rows.Reverse(1, table.Rows.Count - 1); j = await r.NewService().CommitChunkAsync("ADMIN", j.JobId, 0, "c0"); Assert.Equal(100, j.Processed); var state = await r.Store.ReadAsync(); Assert.Equal(Enumerable.Range(1, 100).Select(i => i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)).Order(), state.Items.Select(i => i.SourceCode).Order()); j = await r.NewService().CommitChunkAsync("ADMIN", j.JobId, 100, "c100"); j = await r.NewService().CommitChunkAsync("ADMIN", j.JobId, 200, "c200"); Assert.Equal("COMPLETE", j.State); Assert.Equal(205, (await r.Store.ReadAsync()).Items.Count); }
    private sealed class Rig(InventoryFixture f, GoogleCategoryCatalogStoreTests.CategorySheetsHandler h, GoogleWorkspaceOptions o)
    {
        public InventoryFixture F { get; } = f; public GoogleCategoryCatalogStoreTests.CategorySheetsHandler Handler { get; } = h; public GoogleWorkspaceOptions Options { get; } = o;
        public DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleApiRequestExecutor Executor => new(Options, new Token(), new HttpClient(Handler));
        public GoogleInventoryStore Store => new(Options, Executor); public InventoryService Service => NewService(); public InventoryService NewService() => new(Store, F.Base.Categories, F.Base.Memory, F.Base.Auth, F.Base.Writer, F.Base.Clock);
        public static async Task<Rig> Start() => new(await InventoryFixture.Start(), new(), new() { SpreadsheetId = "business-fake", SystemSpreadsheetId = "system-fake", OAuthClientId = "fixture", OAuthClientSecret = "fixture-config", OAuthRefreshToken = "fixture-config", MaxRetryAttempts = 1 });
    }
    private sealed class Token : IGoogleAccessTokenProvider { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("synthetic-inventory-token"); public void Invalidate() { } }
}
