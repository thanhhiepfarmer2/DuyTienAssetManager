using System.Diagnostics;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class OfflineBaselineTests
{
    [Fact]
    public async Task Capture_original_Google_adapter_pipeline_before_LocalOffline_changes()
    {
        var f = await GoogleTreeMutationTests.Fixture.Start(); await f.Create();
        var api = new GoogleApiRequestExecutor(f.Options, new Token(), new HttpClient(f.Handler));
        var a = new GoogleAssetCatalogStore(f.Options, api); var c = new GoogleCategoryCatalogStore(f.Options, api); var i = new GoogleInventoryStore(f.Options, api);
        var assets = new AssetCrudService(a, c, f.AuthFixture.Auth, f.Writer, f.AuthFixture.Clock);
        var categories = new CategoryCatalogService(c, f.AuthFixture.Auth, f.Writer, f.AuthFixture.Clock);
        var inventory = new InventoryService(i, c, a, f.AuthFixture.Auth, f.Writer, f.AuthFixture.Clock);
        var reports = new ReportService(a, c, i, f.AuthFixture.Auth, f.Writer, f.AuthFixture.Clock);
        var measures = new List<object>();
        async Task Measure(string name, Func<Task> run) { for (var n = 0; n < 4; n++) { var before = f.Handler.Uris.Count; var sw = Stopwatch.StartNew(); await run(); measures.Add(new { name, phase = n == 0 ? "cold-service" : "warm", ms = sw.Elapsed.TotalMilliseconds, googleHttpCalls = f.Handler.Uris.Count - before }); } }
        await Measure("categories", async () => { await categories.ListAsync("ADMIN"); });
        await Measure("assets", async () => { await assets.ListAsync("ADMIN", new()); });
        await Measure("items", async () => { await inventory.ListAsync("ADMIN", new()); });
        await Measure("reportsR04", async () => { await reports.ReadAsync("ADMIN", "R04", new()); });
        var target = Environment.GetEnvironmentVariable("DUYTIEN_BASELINE_OUTPUT");
        if (target is not null) await File.WriteAllTextAsync(target, JsonSerializer.Serialize(new { source = "FAKE_HTTP_GOOGLE_ADAPTER_SMALL_FIXTURE", auth = "fixture memory; not Google auth/login", pacing = "not active in fixture; not real Google latency", measures }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(16, measures.Count);
    }
    private sealed class Token : IGoogleAccessTokenProvider { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fake-baseline-only"); public void Invalidate() { } }
}
