using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class ReportGoogleReadTests
{
    [Fact]
    public async Task Report_reuses_persisted_adapter_state_after_service_restart_no_business_writes()
    { var r = await Rig.Start(); var n = r.F.Handler.Posts; var service = r.Service(); var assets = await service.ReadAsync("ADMIN", "R01", new()); Assert.Equal(1, assets.Summary.AssetCount); Assert.Equal(r.Asset.AssetId, assets.Assets.Single().AssetId); var stock = await r.Service().ReadAsync("ADMIN", "R04", new()); Assert.Single(stock.Stock); Assert.Equal(2m, stock.Stock[0].Quantity); Assert.Equal("cái", stock.Stock[0].Unit); Assert.Equal("GIẢ nguồn snapshot", stock.Stock[0].Source); var csv = await r.Service().ExportAsync("ADMIN", "R04", new()); Assert.Contains("R04-DRAFT", csv.FileName, StringComparison.Ordinal); Assert.Equal(n, r.F.Handler.Posts); }
    [Fact]
    public async Task Registry_metadata_guard_still_blocks_tree_mutation_while_reports_only_read_separate_sources()
    { var r = await Rig.Start(); var n = r.F.Handler.Posts; await Assert.ThrowsAsync<ExternalStorageException>(() => r.F.Store().ReadAsync()); var report = await r.Service().ReadAsync("ADMIN", "R02", new()); Assert.Equal(1, report.Summary.AssetCount); Assert.Equal(n, r.F.Handler.Posts); }
    [Theory]
    [InlineData("Items")]
    [InlineData("Assets")]
    [InlineData("Categories")]
    public async Task Corrupt_source_schema_is_503_not_empty_or_zero_and_never_written(string table)
    { var r = await Rig.Start(); var n = r.F.Handler.Posts; r.F.Handler.Tables[table].Rows[0][0] = "UNKNOWN-HEADER"; await Assert.ThrowsAsync<ExternalStorageException>(() => r.Service().ReadAsync("ADMIN", table == "Items" ? "R03" : "R01", new())); Assert.Equal(n, r.F.Handler.Posts); }
    [Fact]
    public async Task No_report_permission_causes_no_business_google_reads_or_writes()
    { var r = await Rig.Start(); var n = r.F.Handler.Uris.Count; var posts = r.F.Handler.Posts; await Assert.ThrowsAsync<ReportForbiddenException>(() => r.Service().ReadAsync("USER", "R04", new())); Assert.Equal(n, r.F.Handler.Uris.Count); Assert.Equal(posts, r.F.Handler.Posts); }
    private sealed class Rig(GoogleTreeMutationTests.Fixture fixture, AssetRecord asset)
    {
        public GoogleTreeMutationTests.Fixture F { get; } = fixture; public AssetRecord Asset { get; } = asset;
        private GoogleApiRequestExecutor Api() => new(F.Options, new Token(), new HttpClient(F.Handler));
        public ReportService Service() { var api = Api(); return new(new GoogleAssetCatalogStore(F.Options, api), new GoogleCategoryCatalogStore(F.Options, api), new GoogleInventoryStore(F.Options, api), F.AuthFixture.Auth, F.Writer, F.AuthFixture.Clock); }
        public static async Task<Rig> Start()
        {
            var f = await GoogleTreeMutationTests.Fixture.Start(); var asset = await f.Create(); var r = new Rig(f, asset); var api = r.Api(); var itemService = new InventoryService(new GoogleInventoryStore(f.Options, api), new GoogleCategoryCatalogStore(f.Options, api), new GoogleAssetCatalogStore(f.Options, api), f.AuthFixture.Auth, f.Writer, f.AuthFixture.Clock);
            var source = await InventoryFixture.Start(); var request = source.Request() with { Groups = [new("GIẢ", f.Child, true)] }; var preview = await itemService.PreviewAsync("ADMIN", request); var job = await itemService.StartAsync("ADMIN", request, preview.Revision, preview.PreviewHash, "report-fixture-start", true); job = await itemService.CommitChunkAsync("ADMIN", job.JobId, 0, "report-fixture-chunk"); var item = (await itemService.ListAsync("ADMIN", new())).Items.Single(); await itemService.WriteStockAsync("ADMIN", new(item.ItemId, "LOC-A", "cái", 2, 5, f.AuthFixture.Clock.GetUtcNow(), "GIẢ nguồn snapshot", 0, "Fixture GIẢ"), "report-fixture-stock"); return r;
        }
    }
    private sealed class Token : IGoogleAccessTokenProvider { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("synthetic-report-token"); public void Invalidate() { } }
}
