using System.Text;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Reports;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class ReportTests
{
    [Fact]
    public async Task R01_counts_assets_not_items_excludes_deleted_invalid_and_paginates_after_scope()
    { var f = await ReportFixture.Start(); await f.Assets(Enumerable.Range(1, 25).Select(i => f.Asset("A" + i)).Append(f.Asset("DELETED") with { IsDeleted = true }).Append(f.Asset("INVALID", location: "NO-LOCATION"))); for (var i = 0; i < 100; i++) f.Item("I" + i); var r = await f.Service.ReadAsync("ADMIN", "R01", new(Page: 2, PageSize: 10)); Assert.Equal(25, r.Summary.AssetCount); Assert.Null(r.Summary.ItemCount); Assert.Equal(25, r.Total); Assert.Equal(10, r.Assets.Count); Assert.Empty(r.Stock); Assert.All(r.Assets, a => Assert.Equal("DRAFT_DEMO", a.DataState)); Assert.Equal(f.Base.Clock.GetUtcNow(), r.ReadAtUtc); Assert.Equal(0, f.Inventory.Writes); }
    [Fact]
    public async Task R02_direct_subtree_unique_counts_and_Flevel_filter_are_separate()
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("ROOT", category: "CAT-MACHINE"), f.Asset("LEAF1"), f.Asset("LEAF2"), f.Asset("OTHER", category: "CAT-OTHER")]); var r = await f.Service.ReadAsync("ADMIN", "R02", new()); Assert.Equal(4, r.Summary.AssetCount); var root = r.Categories.Single(c => c.CategoryId == "CAT-MACHINE"); Assert.Equal(1, root.DirectAssetCount); Assert.Equal(3, root.SubtreeAssetCount); Assert.Equal(2, r.Categories.Single(c => c.CategoryId == "CAT-CHILD").SubtreeAssetCount); r = await f.Service.ReadAsync("ADMIN", "R02", new(CategoryId: "CAT-MACHINE", GroupLevel: 2)); Assert.Equal(3, r.Summary.AssetCount); Assert.Single(r.Categories); Assert.Equal(2, r.Categories[0].SubtreeAssetCount); r = await f.Service.ReadAsync("ADMIN", "R01", new(CategoryId: "CAT-MACHINE", IncludeDescendants: false)); Assert.Single(r.Assets); Assert.Equal("ROOT", r.Assets[0].AssetId); }
    [Fact]
    public async Task Hidden_assets_ancestors_totals_revision_counters_and_search_do_not_leak()
    { var f = await ReportFixture.Start(); var c = f.Base.Categories.State.Clone(); var pos = c.Categories.FindIndex(n => n.CategoryId == "CAT-MACHINE"); c.Categories[pos] = c.Categories[pos] with { Name = "HIDDEN-PARENT-CANARY" }; await f.Base.Categories.CommitAsync(c, c.Revision); await f.Assets([f.Asset("OWN"), f.Asset("HIDDEN", category: "CAT-OTHER", name: "HIDDEN-ASSET-CANARY"), f.Asset("HIDDEN-LOC", org: "OU-B", location: "LOC-B")]); await f.Base.Grant(AuthRole.Viewer, "OU-A", "LOC-A", "CAT-CHILD", false); var r = await f.Service.ReadAsync("USER", "R02", new()); Assert.Equal(1, r.Summary.AssetCount); Assert.Single(r.Categories); Assert.True(r.Categories[0].PathIsPartial); Assert.Null(r.Categories[0].VisibleParentId); Assert.Equal(0, r.Categories[0].VisibleDepth); Assert.Equal(1, r.Categories[0].SubtreeAssetCount); var body = JsonSerializer.Serialize(r); Assert.DoesNotContain("HIDDEN-PARENT-CANARY", body, StringComparison.Ordinal); Assert.DoesNotContain("HIDDEN-ASSET-CANARY", body, StringComparison.Ordinal); Assert.DoesNotContain("assetRevision", body, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("inventoryRevision", body, StringComparison.OrdinalIgnoreCase); Assert.Equal(0, (await f.Service.ReadAsync("USER", "R01", new(Q: "HIDDEN-PARENT-CANARY"))).Total); await Assert.ThrowsAsync<ReportNotFoundException>(() => f.Service.ReadAsync("USER", "R02", new(CategoryId: "CAT-MACHINE"))); await Assert.ThrowsAsync<ReportNotFoundException>(() => f.Service.ReadAsync("USER", "R02", new(CategoryId: "UNKNOWN"))); }
    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task Category_exact_and_descendant_scope_precedes_filter_and_count(bool descendants, int count)
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("ROOT", category: "CAT-MACHINE"), f.Asset("CHILD"), f.Asset("OTHER", category: "CAT-OTHER")]); await f.Base.Grant(AuthRole.Viewer, category: "CAT-MACHINE", descendants: descendants); var r = await f.Service.ReadAsync("USER", "R01", new()); Assert.Equal(count, r.Total); }
    [Fact]
    public async Task Report_read_and_export_recheck_revoke_and_do_not_cross_combine_wider_viewer()
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("OWN"), f.Asset("OTHER", category: "CAT-OTHER")]); await f.Base.Grant(AuthRole.Viewer); await f.Base.Grant(AuthRole.AssetEditor, category: "CAT-CHILD", descendants: false); Assert.Equal(2, (await f.Service.ReadAsync("USER", "R01", new())).Total); var csv = Encoding.UTF8.GetString((await f.Service.ExportAsync("USER", "R01", new())).Bytes); Assert.Contains("OWN", csv, StringComparison.Ordinal); Assert.DoesNotContain("OTHER", csv, StringComparison.Ordinal); await f.Base.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER" && a.RoleCode == AuthRole.AssetEditor); return true; }); await Assert.ThrowsAsync<ReportForbiddenException>(() => f.Service.ExportAsync("USER", "R01", new())); await f.Base.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER"); return true; }); await Assert.ThrowsAsync<ReportForbiddenException>(() => f.Service.ReadAsync("USER", "R01", new())); }
    [Fact]
    public async Task Stock_zero_low_equal_above_unknown_missing_min_and_nonstock_are_distinct()
    { var f = await ReportFixture.Start(); f.Snapshot(f.Item("ZERO"), 0, null); f.Snapshot(f.Item("LOW"), 2, 5); f.Snapshot(f.Item("EQUAL"), 5, 5); f.Snapshot(f.Item("HIGH"), 6, 5); f.Snapshot(f.Item("NULL"), null, null); f.Snapshot(f.Item("NOMIN"), 2, null); f.Item("NONSTOCK", tracked: false); f.Item("UNCONFIRMED", tracked: null); f.Item("NO-SNAPSHOT"); var r3 = await f.Service.ReadAsync("ADMIN", "R03", new()); Assert.Single(r3.Stock); Assert.Equal("ZERO", r3.Stock[0].ItemId); Assert.Equal(0m, r3.Stock[0].Quantity); Assert.Null(r3.Stock[0].Minimum); var r4 = await f.Service.ReadAsync("ADMIN", "R04", new()); Assert.Equal(2, r4.Total); Assert.Equal(new[] { "EQUAL", "LOW" }, r4.Stock.Select(r => r.ItemId).Order()); Assert.Equal(2, r4.Summary.StatusCounts.Single(s => s.Status == "UNKNOWN").Count); Assert.Equal(3, r4.Summary.StatusCounts.Single(s => s.Status == "MISSING_MINIMUM").Count); Assert.Equal(1, r4.Summary.StatusCounts.Single(s => s.Status == "NOT_TRACKED").Count); Assert.Equal(1, r4.Summary.StatusCounts.Single(s => s.Status == "TRACKING_UNCONFIRMED").Count); Assert.Null(r4.Summary.AssetCount); Assert.Equal(9, r4.Summary.ItemCount); Assert.Equal(6, r4.Summary.SnapshotCount); var missing = await f.Service.ReadAsync("ADMIN", "R04", new(Section: "MISSING_MINIMUM")); Assert.Equal(3, missing.Stock.Count); Assert.Contains(missing.Stock, r => r.Status == "OUT"); }
    [Fact]
    public async Task Quantities_are_aggregated_by_exact_unit_not_a_grand_total_and_before_paging()
    { var f = await ReportFixture.Start(); f.Snapshot(f.Item("KG1", unit: "kg"), 2, 5); f.Snapshot(f.Item("KG2", unit: "kg"), 3, 5); f.Snapshot(f.Item("PIECE", unit: "cái"), 1, 2); var r = await f.Service.ReadAsync("ADMIN", "R04", new(PageSize: 1)); Assert.Equal(3, r.Total); Assert.Single(r.Stock); Assert.Equal(2, r.Summary.Units.Count); Assert.Equal(5m, r.Summary.Units.Single(u => u.Unit == "kg").QuantitySum); Assert.Equal(1m, r.Summary.Units.Single(u => u.Unit == "cái").QuantitySum); Assert.DoesNotContain("grandTotal", JsonSerializer.Serialize(r), StringComparison.OrdinalIgnoreCase); r = await f.Service.ReadAsync("ADMIN", "R04", new(Unit: "kg")); Assert.Equal(2, r.Total); }
    [Fact]
    public async Task Unknown_or_nontracked_catalog_items_have_no_invented_location_asof_quantity_or_BOM_stock()
    { var f = await ReportFixture.Start(); f.Item("NO-SNAPSHOT"); f.Item("NONSTOCK", tracked: false); var r = await f.Service.ReadAsync("ADMIN", "R03", new(Section: "UNKNOWN")); var row = Assert.Single(r.Stock); Assert.Null(row.Quantity); Assert.Null(row.LocationId); Assert.Null(row.AsOf); Assert.Null(row.Source); Assert.Equal("ITEM_WITHOUT_VISIBLE_SNAPSHOT", row.RowKind); Assert.Null(r.Summary.Units.Single().QuantitySum); Assert.Equal(0, (await f.Service.ReadAsync("ADMIN", "R03", new(Section: "UNKNOWN", LocationId: "LOC-A"))).Total); Assert.Equal(1, (await f.Service.ReadAsync("ADMIN", "R03", new(Section: "NOT_TRACKED"))).Total); Assert.Empty((await f.Service.ReadAsync("ADMIN", "R03", new())).Stock); }
    [Fact]
    public async Task Hidden_snapshot_is_indistinguishable_from_no_visible_snapshot_and_no_location_filter_inference()
    { var f = await ReportFixture.Start(); var item = f.Item("OWN-CATALOG"); await f.Base.Grant(AuthRole.Viewer, "OU-A", "LOC-A", "CAT-CHILD"); var before = await f.Service.ReadAsync("USER", "R03", new(Section: "UNKNOWN")); f.Snapshot(item, 0, 5, location: "LOC-B", source: "HIDDEN-SOURCE-CANARY"); var after = await f.Service.ReadAsync("USER", "R03", new(Section: "UNKNOWN")); Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)); Assert.DoesNotContain("HIDDEN-SOURCE-CANARY", JsonSerializer.Serialize(after), StringComparison.Ordinal); Assert.Equal(0, (await f.Service.ReadAsync("USER", "R03", new())).Total); }
    [Fact]
    public async Task Org_location_category_permissions_do_not_combine_between_assignments()
    { var f = await ReportFixture.Start(); f.Snapshot(f.Item("OWN"), 0, 5); await f.Base.Grant(AuthRole.Viewer, "OU-A", "LOC-B", "CAT-CHILD"); await f.Base.Grant(AuthRole.Viewer, "OU-B", "LOC-A", "CAT-CHILD"); Assert.Equal(0, (await f.Service.ReadAsync("USER", "R03", new())).Total); Assert.Equal(0, (await f.Service.ReadAsync("USER", "R03", new())).Summary.SnapshotCount); }
    [Theory]
    [InlineData("source", "SOURCE_MISSING")]
    [InlineData("unit", "INVALID_SNAPSHOT")]
    [InlineData("time", "INVALID_SNAPSHOT")]
    [InlineData("negative", "INVALID_SNAPSHOT")]
    [InlineData("service", "NOT_TRACKED")]
    public async Task Missing_source_invalid_snapshot_or_service_cannot_be_zero_or_low_matches(string fault, string status)
    { var f = await ReportFixture.Start(); var item = f.Item("I", nature: fault == "service" ? "Dịch vụ" : "Vật tư hàng hóa"); var b = f.Snapshot(item, 0, 5, source: fault == "source" ? "" : "GIẢ"); f.Inventory.State.Balances[0] = fault switch { "unit" => b with { Unit = "DIFFERENT" }, "time" => b with { AsOf = default }, "negative" => b with { Quantity = -1 }, _ => b }; Assert.Equal(0, (await f.Service.ReadAsync("ADMIN", "R03", new())).Total); Assert.Equal(0, (await f.Service.ReadAsync("ADMIN", "R04", new())).Total); var r = await f.Service.ReadAsync("ADMIN", "R03", new(Section: status)); Assert.Single(r.Stock); Assert.NotEqual("OUT", r.Stock[0].Status); }
    [Fact]
    public async Task Current_snapshot_date_filters_preserve_sources_timestamps_and_do_not_time_travel()
    { var f = await ReportFixture.Start(); var b = f.Snapshot(f.Item("OLD"), 0, 5); f.Snapshot(f.Item("NEW"), 0, 5, asOf: f.Base.Clock.GetUtcNow()); var r = await f.Service.ReadAsync("ADMIN", "R03", new(AsOfFrom: f.Base.Clock.GetUtcNow().AddMinutes(-1))); Assert.Single(r.Stock); Assert.Equal("NEW", r.Stock[0].ItemId); Assert.Equal(f.Base.Clock.GetUtcNow(), r.Summary.LatestSnapshotAsOf); r = await f.Service.ReadAsync("ADMIN", "R03", new(AsOfTo: b.AsOf)); Assert.Single(r.Stock); Assert.Equal("OLD", r.Stock[0].ItemId); }
    [Fact]
    public async Task Invalid_inactive_or_quarantined_references_are_excluded_for_admin_also()
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("VALID"), f.Asset("BAD", category: "NONE")]); f.Item("Q", category: ""); var item = f.Item("BAD-LOC"); f.Snapshot(item, 0, 5, location: "NO-LOCATION"); Assert.Equal(1, (await f.Service.ReadAsync("ADMIN", "R01", new())).Total); Assert.Equal(0, (await f.Service.ReadAsync("ADMIN", "R03", new())).Total); Assert.DoesNotContain("Q", (await f.Service.ReadAsync("ADMIN", "R03", new(Section: "UNKNOWN"))).Stock.Select(r => r.ItemId)); }
    [Theory]
    [InlineData("page")]
    [InlineData("size")]
    [InlineData("group")]
    [InlineData("section")]
    [InlineData("long")]
    [InlineData("timezone")]
    [InlineData("date-order")]
    [InlineData("wrong-filter")]
    public async Task Invalid_queries_are_explicit_before_storage(string fault)
    { var f = await ReportFixture.Start(); var q = fault switch { "page" => new ReportQuery(Page: 0), "size" => new(PageSize: 101), "group" => new(GroupLevel: 11), "section" => new(Section: "R12"), "long" => new(Q: new string('x', 201)), "timezone" => new(AsOfFrom: f.Base.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7))), "date-order" => new(AsOfFrom: f.Base.Clock.GetUtcNow(), AsOfTo: f.Base.Clock.GetUtcNow().AddDays(-1)), _ => new(Type: "MACHINE") }; await Assert.ThrowsAsync<ReportValidationException>(() => f.Service.ReadAsync("ADMIN", "R03", q)); }
    [Fact]
    public async Task No_proposed_report_upstream_failure_not_zero_and_R01_does_not_require_inventory()
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("OWN")]); await Assert.ThrowsAsync<ReportNotFoundException>(() => f.Service.ReadAsync("ADMIN", "R12", new())); f.Inventory.FailRead = true; Assert.Equal(1, (await f.Service.ReadAsync("ADMIN", "R01", new())).Total); await Assert.ThrowsAsync<ExternalStorageException>(() => f.Service.ReadAsync("ADMIN", "R03", new())); f.Base.Memory.FailRead = true; await Assert.ThrowsAsync<ExternalStorageException>(() => f.Service.ReadAsync("ADMIN", "R01", new())); }
    [Fact]
    public async Task Csv_exports_all_filtered_rows_not_page_utf8_unicode_and_formula_safe()
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("A1", name: "=HYPERLINK(\"GIẢ\")"), f.Asset("A2", name: " +CANARY"), f.Asset("A3", name: "Máy có dấu, \"trích\"\nGIẢ")]); var bytes = (await f.Service.ExportAsync("ADMIN", "R01", new(PageSize: 1, Page: 2))).Bytes; Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3)); var csv = Encoding.UTF8.GetString(bytes); Assert.Contains("A1", csv, StringComparison.Ordinal); Assert.Contains("A2", csv, StringComparison.Ordinal); Assert.Contains("A3", csv, StringComparison.Ordinal); Assert.Contains("'=HYPERLINK", csv, StringComparison.Ordinal); Assert.Contains("' +CANARY", csv, StringComparison.Ordinal); Assert.Contains("Máy có dấu", csv, StringComparison.Ordinal); Assert.Contains("SelectedRows\",\"3", csv, StringComparison.Ordinal); }
    [Fact]
    public async Task Shared_writer_blocks_report_until_snapshot_release_and_report_never_writes()
    { var f = await ReportFixture.Start(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var hold = f.Base.Writer.ExecuteAsync(async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); return true; }); await entered.Task; var read = f.Service.ReadAsync("ADMIN", "R03", new()); Assert.False(read.IsCompleted); release.SetResult(); await hold; await read; Assert.Equal(0, f.Inventory.Writes); }
    [Theory]
    [InlineData("@CANARY")]
    [InlineData("-CANARY")]
    [InlineData("\t=CANARY")]
    [InlineData("\r=CANARY")]
    [InlineData("\uFEFF=CANARY")]
    public async Task Csv_prefixes_dangerous_text_even_after_whitespace_or_BOM(string name)
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("A1", name: name)]); var text = Encoding.UTF8.GetString((await f.Service.ExportAsync("ADMIN", "R01", new())).Bytes); Assert.Contains("\"'" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"", text, StringComparison.Ordinal); }
    [Fact]
    public async Task Unknown_quantity_known_snapshot_vs_minimum_missing_counts_are_not_faked_zero()
    { var f = await ReportFixture.Start(); var b = f.Snapshot(f.Item("UNKNOWN"), null, null); var report = await f.Service.ReadAsync("ADMIN", "R04", new(Section: "UNKNOWN")); Assert.Equal(b.AsOf, report.Stock.Single().AsOf); Assert.Null(report.Summary.Units.Single().QuantitySum); Assert.Equal(1, report.Summary.Units.Single().UnknownQuantityCount); Assert.Equal(1, (await f.Service.ReadAsync("ADMIN", "R04", new(Section: "MISSING_MINIMUM"))).Total); }

    [Fact]
    public async Task Synthetic_20744_items_scoped_unknown_summary_paging_does_not_count_assets_or_send_all_rows()
    { var f = await ReportFixture.Start(); for (var i = 0; i < 20744; i++) f.Item("GIA-" + i, unit: i % 4 == 0 ? "kg" : "cái", category: i % 2 == 0 ? "CAT-CHILD" : "CAT-OTHER"); await f.Base.Grant(AuthRole.Viewer, category: "CAT-CHILD", descendants: false); var r = await f.Service.ReadAsync("USER", "R03", new(Section: "UNKNOWN", Page: 2, PageSize: 7)); Assert.Equal(10372, r.Total); Assert.Equal(7, r.Stock.Count); Assert.Equal(10372, r.Summary.ItemCount); Assert.Equal(0, r.Summary.SnapshotCount); Assert.Null(r.Summary.AssetCount); Assert.All(r.Summary.Units, u => Assert.Null(u.QuantitySum)); Assert.All(r.Stock, s => Assert.Null(s.LocationId)); Assert.True(JsonSerializer.Serialize(r).Length < 20000); }
    [Fact]
    public async Task Report_rechecks_current_tree_version_and_descendant_permission_after_snapshot_change()
    { var f = await ReportFixture.Start(); await f.Assets([f.Asset("OWN")]); await f.Base.Grant(AuthRole.Viewer, category: "CAT-MACHINE", descendants: true); Assert.Equal(1, (await f.Service.ReadAsync("USER", "R01", new())).Total); var state = f.Base.Categories.State.Clone(); state.Categories.AddRange(state.ActiveNodes.Select(n => n with { TreeVersionId = "TREE-NEW", ParentId = n.CategoryId == "CAT-CHILD" ? "CAT-OTHER" : n.ParentId }).ToArray()); state.ActiveTreeVersionId = "TREE-NEW"; await f.Base.Categories.CommitAsync(state, state.Revision); var after = await f.Service.ReadAsync("USER", "R02", new()); Assert.Equal("TREE-NEW", after.TreeVersionId); Assert.Equal(0, after.Summary.AssetCount); Assert.DoesNotContain(after.Categories, c => c.CategoryId == "CAT-CHILD"); await Assert.ThrowsAsync<ReportNotFoundException>(() => f.Service.ReadAsync("USER", "R01", new(CategoryId: "CAT-CHILD"))); }
    [Fact]
    public async Task Overflow_is_explicit_and_hidden_overflow_does_not_affect_authorized_report()
    { var f = await ReportFixture.Start(); f.Snapshot(f.Item("OWN"), 1, 2); f.Snapshot(f.Item("H1", category: "CAT-OTHER", unit: "kg"), decimal.MaxValue, decimal.MaxValue); f.Snapshot(f.Item("H2", category: "CAT-OTHER", unit: "kg"), decimal.MaxValue, decimal.MaxValue); await f.Base.Grant(AuthRole.Viewer, category: "CAT-CHILD", descendants: false); Assert.Equal(1m, (await f.Service.ReadAsync("USER", "R04", new())).Summary.Units.Single().QuantitySum); await Assert.ThrowsAsync<ReportValidationException>(() => f.Service.ReadAsync("ADMIN", "R04", new())); }

}
internal sealed class ReportFixture
{
    public AssetFixture Base { get; }
    public ReportInventoryStore Inventory { get; } = new(); public ReportService Service { get; }
    private ReportFixture(AssetFixture f) { Base = f; Service = new(f.Memory, f.Categories, Inventory, f.Auth, f.Writer, f.Clock); }
    public static async Task<ReportFixture> Start() => new(await AssetFixture.Start());
    public AssetRecord Asset(string id, string category = "CAT-CHILD", string org = "OU-A", string location = "LOC-A", string? name = null) => new(id, "TEST-GIA-" + id, "TST", "RULE-FAKE", category, name ?? "Asset " + id + " GIẢ", "MACHINE", org, location, "", "", "", "", null, "UNKNOWN", "Fixture GIẢ", "DRAFT_DEMO", false, Base.Clock.GetUtcNow(), "ADMIN", Base.Clock.GetUtcNow(), "ADMIN", 1);
    public async Task Assets(IEnumerable<AssetRecord> assets) { var s = Base.Memory.State.Clone(); s.Assets.AddRange(assets); await Base.Memory.CommitAsync(s, s.Revision); }
    public ItemRecord Item(string id, string unit = "cái", bool? tracked = true, string category = "CAT-CHILD", string nature = "Vật tư hàng hóa")
    { var cells = Enumerable.Repeat("", 20).ToArray(); cells[0] = "000-" + id; cells[1] = "Item " + id + " GIẢ"; cells[2] = nature; cells[5] = unit; cells[7] = "LOC-B"; cells[19] = "99999"; var item = new ItemRecord(id, "MISA_VTHH", "CODE:" + id, category, tracked, cells, 1, category.Length == 0 ? "QUARANTINED" : "DRAFT_DEMO"); Inventory.State.Items.Add(item); return item; }
    public StockBalance Snapshot(ItemRecord item, decimal? qty, decimal? min, string location = "LOC-A", string source = "Snapshot GIẢ", DateTimeOffset? asOf = null)
    { var b = new StockBalance("BAL-" + item.ItemId + "-" + location, item.ItemId, location, item.PrimaryUnit, qty, min, asOf ?? Base.Clock.GetUtcNow().AddHours(-1), source, "ADMIN", 1); Inventory.State.Balances.Add(b); return b; }
}
internal sealed class ReportInventoryStore : IInventoryStore
{
    public InventoryState State { get; } = new(); public bool FailRead { get; set; }
    public int Writes { get; private set; }
    public Task<InventoryState> ReadAsync(CancellationToken ct = default) => FailRead ? throw new ExternalStorageException("PRIVATE-REPORT-INVENTORY-CANARY") : Task.FromResult(State.Clone());
    public Task CommitAsync(InventoryState candidate, long expectedRevision, CancellationToken ct = default) { Writes++; throw new InvalidOperationException("Report source stub must never write."); }
}
