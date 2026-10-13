using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class InventoryTests
{
    [Fact]
    public async Task Import_preserves_codes_both_descriptions_references_and_unknown_without_assets()
    { var f = await InventoryFixture.Start(); var r = f.Request(); var j = await f.Import(r); var item = Assert.Single(f.Store.State.Items); Assert.Equal("00001", item.SourceCode); Assert.Equal("Mô tả Item GIẢ", item.Cells[4]); Assert.Equal("Mô tả conversion GIẢ", item.Cells[11]); Assert.Equal("999", item.Cells[19]); Assert.Empty(f.Store.State.Balances); Assert.Empty(f.Base.Memory.State.Assets); Assert.Equal("COMPLETE", j.State); Assert.Equal("DRAFT_DEMO", item.State); Assert.Equal("WH-FAKE", item.Cells[7]); InventoryIntegrity.Validate(f.Store.State); }
    [Fact]
    public async Task Same_data_second_job_is_noop_and_changed_version_diff_preserves_id()
    { var f = await InventoryFixture.Start(); var r = f.Request(); await f.Import(r); var id = f.Store.State.Items.Single().ItemId; var p = await f.Service.PreviewAsync("ADMIN", r); Assert.Equal(1, p.Skipped); await f.Import(r); Assert.Single(f.Store.State.Items); Assert.Equal(1, f.Store.State.Items.Single().Version); var changed = r with { Document = r.Document with { Rows = [r.Document.Rows[0] with { Cells = r.Document.Rows[0].Cells.ToArray() }] } }; changed.Document.Rows[0].Cells[4] = "Mô tả mới GIẢ"; p = await f.Service.PreviewAsync("ADMIN", changed); Assert.Equal(1, p.Updated); await f.Import(changed); Assert.Equal(id, f.Store.State.Items.Single().ItemId); Assert.Equal(2, f.Store.State.Items.Single().Version); }
    [Fact]
    public async Task Duplicate_CDKEM_blocks_until_explicit_same_data_merge_with_audit()
    { var f = await InventoryFixture.Start(); var r = f.Request(2, true); var p = await f.Service.PreviewAsync("ADMIN", r); Assert.Contains(p.Errors, e => e.Message.Contains("trùng", StringComparison.Ordinal)); await Assert.ThrowsAsync<InventoryValidationException>(() => f.Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "bad", true)); Assert.Equal(0, f.Store.Writes); r = r with { Decisions = [new("CĐKEM", "MERGE", null, "Dữ liệu GIẢ đồng nhất đã quyết định merge")] }; await f.Import(r); Assert.Single(f.Store.State.Items); Assert.Contains("MERGE", f.Store.State.Jobs.Single().DecisionsJson, StringComparison.Ordinal); Assert.Equal(2, f.Store.State.Jobs.Single().TotalSourceRows); }
    [Fact]
    public async Task Different_duplicate_requires_split_stable_identities_and_cannot_change_existing_mapping()
    { var f = await InventoryFixture.Start(); var r = f.Request(2, true); r.Document.Rows[1].Cells[4] = "Khác mô tả GIẢ"; var merge = r with { Decisions = [new("CĐKEM", "MERGE", null, "GIẢ")] }; Assert.NotEmpty((await f.Service.PreviewAsync("ADMIN", merge)).Errors); var split = r with { Decisions = [new("CĐKEM", "SPLIT", new() { { 3, "GIẢ-A" }, { 4, "GIẢ-B" } }, "Quyết định fixture split")] }; await f.Import(split); Assert.Equal(2, f.Store.State.Items.Count); Assert.Equal(2, f.Store.State.Items.Select(i => i.ItemId).Distinct().Count()); await f.Import(split); Assert.Equal(2, f.Store.State.Items.Count); var remap = split with { Decisions = [new("CĐKEM", "SPLIT", new() { { 3, "GIẢ-X" }, { 4, "GIẢ-Y" } }, "Đổi identity")] }; Assert.NotEmpty((await f.Service.PreviewAsync("ADMIN", remap)).Errors); }
    [Fact]
    public async Task Partial_chunk_restart_retry_same_key_no_duplicate_and_progress_is_not_complete()
    { var f = await InventoryFixture.Start(); var r = f.Request(205); var p = await f.Service.PreviewAsync("ADMIN", r); var j = await f.Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "start", true); j = await f.Service.CommitChunkAsync("ADMIN", j.JobId, 0, "c1"); Assert.Equal("PARTIAL", j.State); Assert.Equal(100, j.Processed); f.Store.FailNext = true; await Assert.ThrowsAsync<ExternalStorageException>(() => f.Service.CommitChunkAsync("ADMIN", j.JobId, 100, "c2")); Assert.Equal(100, f.Store.State.Items.Count); var restarted = f.NewService(); Assert.Equal(100, (await restarted.JobAsync("ADMIN", j.JobId)).Processed); var replay = await restarted.CommitChunkAsync("ADMIN", j.JobId, 0, "c1"); Assert.Equal(100, replay.Processed); j = await restarted.CommitChunkAsync("ADMIN", j.JobId, 100, "c2"); j = await restarted.CommitChunkAsync("ADMIN", j.JobId, 200, "c3"); Assert.Equal("COMPLETE", j.State); Assert.Equal(205, f.Store.State.Items.Count); await Assert.ThrowsAsync<InventoryConflictException>(() => restarted.CommitChunkAsync("ADMIN", j.JobId, 0, "c3")); }
    [Fact]
    public async Task Staging_failure_restores_prefix_without_duplicate_or_early_items()
    { var f = await InventoryFixture.Start(); var r = f.Request(205); var p = await f.Service.PreviewAsync("ADMIN", r); f.Store.FailWriteNumber = 3; await Assert.ThrowsAsync<ExternalStorageException>(() => f.Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "staging", true)); Assert.Empty(f.Store.State.Items); Assert.Equal(100, f.Store.State.Plan.Count); Assert.Equal("STAGING", f.Store.State.Jobs.Single().State); f.Store.FailWriteNumber = 0; var j = await f.NewService().StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "staging", true); Assert.Equal("VALIDATED", j.State); Assert.Equal(205, f.Store.State.Plan.Count); Assert.Equal(205, f.Store.State.Plan.Select(p => p.Row.Item.ItemId).Distinct().Count()); }
    [Fact]
    public async Task Synthetic_20744_rows_imports_in_chunks_and_returns_only_requested_page()
    { var f = await InventoryFixture.Start(); var r = f.Request(20744); var p = await f.Service.PreviewAsync("ADMIN", r, page: 2, pageSize: 7); Assert.Equal(20744, p.Added); Assert.Equal(7, p.Diff.Count); var j = await f.Import(r); Assert.Equal(20744, j.Processed); Assert.Equal(20744, f.Store.State.Items.Count); var page = await f.Service.ListAsync("ADMIN", new(Page: 2, PageSize: 7)); Assert.Equal(7, page.Items.Count); Assert.Equal(20744, page.Total); Assert.Empty(f.Base.Memory.State.Assets); }
    [Theory]
    [InlineData(null, null, "UNKNOWN")]
    [InlineData(0, 5, "OUT")]
    [InlineData(2, 5, "LOW")]
    [InlineData(6, 5, "AVAILABLE")]
    public async Task Snapshot_null_zero_low_and_available_keep_asof_unit_source(int? quantity, int? minimum, string expected)
    { var f = await InventoryFixture.Start(); await f.Import(f.Request()); var r = f.Stock(quantity, minimum); var b = await f.Service.WriteStockAsync("ADMIN", r, "stock"); Assert.Equal(expected, b.StockStatus); Assert.Equal((decimal?)quantity, b.Quantity); Assert.Equal("cái", b.Unit); Assert.Equal("GIẢ snapshot nhập riêng", b.Source); Assert.Equal(r.AsOf, b.AsOf); var again = await f.Service.WriteStockAsync("ADMIN", r, "stock"); Assert.Equal(b.BalanceId, again.BalanceId); Assert.Equal(1, again.Version); }
    [Theory]
    [InlineData("negative")]
    [InlineData("unit")]
    [InlineData("future")]
    [InlineData("source")]
    [InlineData("timezone")]
    [InlineData("stale")]
    public async Task Invalid_stock_input_never_writes(string fault)
    { var f = await InventoryFixture.Start(); await f.Import(f.Request()); var r = f.Stock(1, 2); r = fault switch { "negative" => r with { Quantity = -1 }, "unit" => r with { Unit = "kg" }, "future" => r with { AsOf = r.AsOf.AddDays(1) }, "source" => r with { Source = "" }, "timezone" => r with { AsOf = r.AsOf.ToOffset(TimeSpan.FromHours(7)) }, _ => r with { ExpectedVersion = 1 } }; await Assert.ThrowsAnyAsync<Exception>(() => f.Service.WriteStockAsync("ADMIN", r, "bad")); Assert.Empty(f.Store.State.Balances); }
    [Fact]
    public async Task Snapshot_triple_scope_no_cross_assignment_hidden_and_revoke_rechecked()
    { var f = await InventoryFixture.Start(); await f.Import(f.Request()); await f.Base.Grant(AuthRole.CatalogManager, "OU-A", "LOC-B", "CAT-CHILD"); await f.Base.Grant(AuthRole.Viewer); await Assert.ThrowsAsync<InventoryNotFoundException>(() => f.Service.WriteStockAsync("USER", f.Stock(1, 2), "denied")); Assert.Empty(f.Store.State.Balances); await f.Base.Grant(AuthRole.CatalogManager, "OU-A", "LOC-A", "CAT-CHILD"); await f.Service.WriteStockAsync("USER", f.Stock(1, 2), "allowed"); await f.Base.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER" && a.RoleCode == AuthRole.CatalogManager); return true; }); await Assert.ThrowsAsync<InventoryForbiddenException>(() => f.Service.WriteStockAsync("USER", f.Stock(1, 2), "allowed")); }
    [Fact]
    public async Task Item_scope_descendants_siblings_quarantine_and_viewer_import_denied()
    { var f = await InventoryFixture.Start(); await f.Import(f.Request()); var other = f.Request() with { Groups = [new("GIẢ", "CAT-OTHER", true)] }; other.Document.Rows[0].Cells[0] = "OTHER"; await f.Import(other); var quarantine = f.Request() with { Groups = [] }; quarantine.Document.Rows[0].Cells[0] = "UNCLASSIFIED"; await f.Import(quarantine); await f.Base.Grant(AuthRole.Viewer, category: "CAT-MACHINE"); var list = await f.Service.ListAsync("USER", new()); Assert.Single(list.Items); Assert.Equal(1, list.Total); var hidden = f.Store.State.Items.Single(i => i.SourceCode == "OTHER"); await Assert.ThrowsAsync<InventoryNotFoundException>(() => f.Service.DetailAsync("USER", hidden.ItemId)); Assert.Equal(2, (await f.Service.ListAsync("ADMIN", new())).Total); await Assert.ThrowsAsync<InventoryForbiddenException>(() => f.Service.PreviewAsync("USER", f.Request())); }
    [Fact]
    public async Task Preview_stale_confirm_key_change_and_tree_change_are_blocked()
    { var f = await InventoryFixture.Start(); var r = f.Request(); var p = await f.Service.PreviewAsync("ADMIN", r); await Assert.ThrowsAsync<InventoryValidationException>(() => f.Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "k", false)); await Assert.ThrowsAsync<InventoryConflictException>(() => f.Service.StartAsync("ADMIN", r, 99, p.PreviewHash, "k", true)); var j = await f.Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "k", true); await Assert.ThrowsAsync<InventoryConflictException>(() => f.Service.StartAsync("ADMIN", r with { Reason = "khác" }, p.Revision, p.PreviewHash, "k", true)); var c = f.Base.Categories.State.Clone(); c.Categories.AddRange(c.ActiveNodes.Select(n => n with { TreeVersionId = "TREE-OTHER" }).ToArray()); c.ActiveTreeVersionId = "TREE-OTHER"; await f.Base.Categories.CommitAsync(c, c.Revision); await Assert.ThrowsAsync<InventoryConflictException>(() => f.Service.CommitChunkAsync("ADMIN", j.JobId, 0, "c")); Assert.Empty(f.Store.State.Items); }
    [Fact]
    public async Task Import_change_unit_or_tracking_with_snapshot_and_unknown_tracking_service_are_blocked()
    { var f = await InventoryFixture.Start(); await f.Import(f.Request()); await f.Service.WriteStockAsync("ADMIN", f.Stock(1, 2), "stock"); var r = f.Request(); r.Document.Rows[0].Cells[5] = "kg"; Assert.NotEmpty((await f.Service.PreviewAsync("ADMIN", r)).Errors); var g = await InventoryFixture.Start(); await g.Import(g.Request() with { Groups = [new("GIẢ", "CAT-CHILD", null)] }); await Assert.ThrowsAsync<InventoryValidationException>(() => g.Service.WriteStockAsync("ADMIN", g.Stock(1, 2), "bad")); var service = g.Request(); service.Document.Rows[0].Cells[2] = "Dịch vụ"; Assert.NotEmpty((await g.Service.PreviewAsync("ADMIN", service)).Errors); }
    [Fact]
    public async Task Concurrent_jobs_stale_expected_item_version_no_silent_overwrite()
    { var f = await InventoryFixture.Start(); var r = f.Request(); var p = await f.Service.PreviewAsync("ADMIN", r); var a = await f.Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "a", true); p = await f.Service.PreviewAsync("ADMIN", r); var b = await f.Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, "b", true); await f.Service.CommitChunkAsync("ADMIN", a.JobId, 0, "ac"); await Assert.ThrowsAnyAsync<Exception>(() => f.Service.CommitChunkAsync("ADMIN", b.JobId, 0, "bc")); Assert.Single(f.Store.State.Items); }
    [Fact]
    public async Task Reclassification_cannot_combine_two_import_assignments()
    { var f = await InventoryFixture.Start(); await f.Import(f.Request()); await f.Base.Grant(AuthRole.CatalogManager, category: "CAT-CHILD", descendants: false); await f.Base.Grant(AuthRole.CatalogManager, category: "CAT-OTHER", descendants: false); await Assert.ThrowsAsync<InventoryForbiddenException>(() => f.Service.PreviewAsync("USER", f.Request() with { Groups = [new("GIẢ", "CAT-OTHER", true)] })); }
    [Fact]
    public async Task Empty_group_quarantine_not_global_and_partial_job_revoked_permission_cannot_replay()
    { var f = await InventoryFixture.Start(); await f.Base.Grant(AuthRole.CatalogManager, category: "CAT-CHILD"); await Assert.ThrowsAsync<InventoryForbiddenException>(() => f.Service.PreviewAsync("USER", f.Request() with { Groups = [] })); var r = f.Request(); var p = await f.Service.PreviewAsync("USER", r); var j = await f.Service.StartAsync("USER", r, p.Revision, p.PreviewHash, "s", true); await f.Base.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER"); return true; }); await Assert.ThrowsAsync<InventoryForbiddenException>(() => f.Service.CommitChunkAsync("USER", j.JobId, 0, "c")); Assert.Empty(f.Store.State.Items); }
    [Fact]
    public async Task Shared_writer_reader_waits_and_source_row_does_not_become_identity()
    { var f = await InventoryFixture.Start(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var hold = f.Base.Writer.ExecuteAsync(async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); return true; }); await entered.Task; var read = f.Service.ListAsync("ADMIN", new()); Assert.False(read.IsCompleted); release.SetResult(); await hold; await read; var r = f.Request(); await f.Import(r); var id = f.Store.State.Items.Single().ItemId; var changed = r with { Document = r.Document with { Rows = [r.Document.Rows.Single() with { SourceRow = 999 }] } }; await f.Import(changed); Assert.Single(f.Store.State.Items); Assert.Equal(id, f.Store.State.Items.Single().ItemId); }
    [Fact]
    public async Task Existing_Item_blocks_structural_category_import_without_bypassing_guard()
    { var f = await InventoryFixture.Start(); await f.Import(f.Request()); var cats = new CategoryCatalogService(f.Base.Categories, f.Base.Auth, f.Base.Writer, f.Base.Clock, f.Base.Memory, f.Store); var d = new CategoryDocument("GIẢ", CategoryImportReader.MappingVersion, [new("F1-F3 Mã ngang", 3, "1", null, 1, "Root", "TST", CategoryId: "CAT-MACHINE"), new("F1-F3 Mã ngang", 4, "2", null, 1, "Other", "ZZZ", CategoryId: "CAT-OTHER"), new("F1-F3 Mã ngang", 5, "2.1", "2", 2, "Child", "MCN", CategoryId: "CAT-CHILD")], []); var p = await cats.PreviewAsync("ADMIN", d); Assert.Empty(p.Issues); await Assert.ThrowsAsync<CategoryConflictException>(() => cats.ImportAsync("ADMIN", d, p.Revision, p.PreviewHash, "tree", true)); Assert.Equal("CAT-MACHINE", f.Base.Categories.State.ActiveNodes.Single(n => n.CategoryId == "CAT-CHILD").ParentId); }

}
internal sealed class InventoryFixture
{
    public AssetFixture Base { get; }
    public InventoryMemoryStore Store { get; }
    public InventoryService Service { get; }
    private InventoryFixture(AssetFixture b, InventoryMemoryStore s) { Base = b; Store = s; Service = NewService(); }
    public static async Task<InventoryFixture> Start() => new(await AssetFixture.Start(), new());
    public InventoryService NewService() => new(Store, Base.Categories, Base.Memory, Base.Auth, Base.Writer, Base.Clock);
    public ItemImportRequest Request(int count = 1, bool duplicate = false)
    { var rows = Enumerable.Range(0, count).Select(i => { var cells = Enumerable.Repeat("", 20).ToArray(); cells[0] = duplicate ? "CĐKEM" : i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) == "00000" ? "00001" : (i + 1).ToString("D5", System.Globalization.CultureInfo.InvariantCulture); cells[1] = "Item GIẢ"; cells[2] = "Vật tư hàng hóa"; cells[3] = "GIẢ"; cells[4] = "Mô tả Item GIẢ"; cells[5] = "cái"; cells[7] = "WH-FAKE"; cells[11] = "Mô tả conversion GIẢ"; cells[19] = "999"; return new ItemSourceRow(i + 3, cells); }).ToArray(); return new(new("MISA_VTHH", ItemImportReader.Headers.ToArray(), rows), [new("GIẢ", "CAT-CHILD", true)], [], "Fixture GIẢ, không dữ liệu công ty"); }
    public async Task<ItemImportJob> Import(ItemImportRequest r) { var p = await Service.PreviewAsync("ADMIN", r); Assert.Empty(p.Errors); var j = await Service.StartAsync("ADMIN", r, p.Revision, p.PreviewHash, Guid.NewGuid().ToString("N"), true); while (j.State != "COMPLETE") j = await Service.CommitChunkAsync("ADMIN", j.JobId, j.Processed, Guid.NewGuid().ToString("N")); return j; }
    public StockWriteRequest Stock(decimal? quantity, decimal? min) => new(Store.State.Items.First().ItemId, "LOC-A", "cái", quantity, min, Base.Clock.GetUtcNow(), "GIẢ snapshot nhập riêng", 0, "Fixture GIẢ");
}
internal sealed class InventoryMemoryStore : IInventoryStore
{
    public InventoryState State { get; private set; } = new(); public int Writes { get; private set; }
    public bool FailNext { get; set; }
    public int FailWriteNumber { get; set; }
    public Task<InventoryState> ReadAsync(CancellationToken ct = default) => Task.FromResult(State.Clone());
    public Task CommitAsync(InventoryState s, long expectedRevision, CancellationToken ct = default) { if (FailNext || Writes + 1 == FailWriteNumber) { FailNext = false; throw new ExternalStorageException("PRIVATE-INVENTORY-CANARY"); } if (State.Revision != expectedRevision) throw new InventoryConflictException("fake stale"); InventoryIntegrity.Validate(s); State = s.Clone(); Writes++; return Task.CompletedTask; }
}
