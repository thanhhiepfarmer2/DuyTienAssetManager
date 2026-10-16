using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
using DuyTien.AssetManagement.Server.ConnectedCache;
namespace DuyTien.AssetManagement.ArchitectureTests;

// Fake HTTP, actual adapters/validators; no Google or browser claim.
public sealed class GoogleAssetCategoryBatchTests
{
    [Fact]
    public async Task Same_business_source_uses_one_metadata_and_one_union_batch_without_skipping_materialization()
    {
        var f = await GoogleTreeMutationTests.Fixture.Start(); await f.Create();
        using var http = new HttpClient(f.Handler); var api = new GoogleApiRequestExecutor(f.Options, new Token(), http);
        var assets = new GoogleAssetCatalogStore(f.Options, api); var categories = new GoogleCategoryCatalogStore(f.Options, api);
        f.Handler.Uris.Clear(); AssetCatalogState beforeAssets; CategoryCatalogState beforeCategories;
        using (GoogleMetadataReadMemo.Begin(true)) { beforeAssets = await assets.ReadAsync(); beforeCategories = await categories.ReadAsync(); }
        Assert.Equal(3, f.Handler.Uris.Count);
        f.Handler.Uris.Clear(); var result = await new GoogleAssetCategorySnapshotReader(api, assets, categories).ReadAssetCategoriesAsync();
        Assert.Equal(2, f.Handler.Uris.Count); Assert.Single(f.Handler.Uris, u => u.Contains("values:batchGet"));
        Assert.All(f.Handler.Uris, u => { Assert.Contains("business-fake", u); Assert.DoesNotContain("system-fake", u); });
        Assert.Equal(JsonSerializer.Serialize(beforeAssets), JsonSerializer.Serialize(result.Assets));
        Assert.Equal(JsonSerializer.Serialize(beforeCategories), JsonSerializer.Serialize(result.Categories));
        var posts = f.Handler.Posts; f.Handler.Uris.Clear(); await new GoogleAssetCategorySnapshotReader(api, assets, categories).ReadAssetCategoriesAsync();
        Assert.Equal(2, f.Handler.Uris.Count); Assert.Equal(posts, f.Handler.Posts); // next call cannot reuse prior values
    }
    [Theory]
    [InlineData("asset-header")][InlineData("category-header")][InlineData("counter")][InlineData("journal")][InlineData("partial")][InlineData("range")]
    public async Task Combined_batch_keeps_headers_partial_schema_counter_journal_and_range_fail_closed(string fault)
    {
        var f = await GoogleTreeMutationTests.Fixture.Start(); await f.Create();
        using var http = new HttpClient(f.Handler); var api = new GoogleApiRequestExecutor(f.Options, new Token(), http);
        var reader = new GoogleAssetCategorySnapshotReader(api, new(f.Options, api), new(f.Options, api)); var posts = f.Handler.Posts;
        if (fault == "asset-header") f.Handler.Tables["Assets"].Rows[0][0] = "INVALID";
        if (fault == "category-header") f.Handler.Tables["Categories"].Rows[0][0] = "INVALID";
        if (fault == "counter") f.Handler.Tables["IdCounters"].Rows[1][1] = "0";
        if (fault == "journal") f.Handler.Tables["Assets"].Rows[1][5] = "INVALID overwrite";
        if (fault == "partial") f.Handler.Tables.Remove("TreeVersions");
        if (fault == "range") f.Handler.WrongRange = true;
        await Assert.ThrowsAsync<ExternalStorageException>(() => reader.ReadAssetCategoriesAsync()); Assert.Equal(posts, f.Handler.Posts);
    }
    [Fact]
    public async Task Other_source_or_executor_cannot_be_combined()
    {
        var f = await GoogleTreeMutationTests.Fixture.Start(); using var http = new HttpClient(f.Handler); var api = new GoogleApiRequestExecutor(f.Options, new Token(), http);
        var other = new DuyTien.AssetManagement.Infrastructure.Google.Configuration.GoogleWorkspaceOptions { SpreadsheetId = "OTHER", SystemSpreadsheetId = "system-fake" };
        f.Handler.Uris.Clear();
        await Assert.ThrowsAsync<ExternalStorageException>(() => new GoogleAssetCategorySnapshotReader(api, new(f.Options, api), new(other, api)).ReadAssetCategoriesAsync()); Assert.Empty(f.Handler.Uris);
        var otherApi = new GoogleApiRequestExecutor(f.Options, new Token(), http);
        await Assert.ThrowsAsync<ExternalStorageException>(() => new GoogleAssetCategorySnapshotReader(api, new(f.Options, api), new(f.Options, otherApi)).ReadAssetCategoriesAsync()); Assert.Empty(f.Handler.Uris);
    }
    [Fact]
    public async Task Cancelled_or_failed_batch_does_not_poison_next_read()
    {
        var f = await GoogleTreeMutationTests.Fixture.Start(); await f.Create(); using var cts = new CancellationTokenSource();
        using var pipeline = new FaultOnce(f.Handler, cts); using var http = new HttpClient(pipeline); var api = new GoogleApiRequestExecutor(f.Options, new Token(), http);
        var reader = new GoogleAssetCategorySnapshotReader(api, new(f.Options, api), new(f.Options, api)); var posts = f.Handler.Posts;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAssetCategoriesAsync(cts.Token));
        Assert.Single((await reader.ReadAssetCategoriesAsync()).Assets.Assets); Assert.Equal(posts, f.Handler.Posts);
    }
    [Fact]
    public async Task Actor_scope_and_revocation_are_applied_after_raw_batch_and_never_shared_as_filtered_results()
    {
        var r = await TreeRegistryRig.Start(false); var assets = r.Assets; var categories = r.Categories;
        var combined = new GoogleAssetCategorySnapshotReader(r.Api, assets, categories);
        var observed = new ObservedBusinessStores(assets, categories, r.Inventory, r.Media, r.Joint(), combined);
        var service = new AssetCrudService(observed, observed, r.F.AuthFixture.Auth, r.F.Writer, r.F.AuthFixture.Clock);
        await r.F.AuthFixture.Grant("USER", AuthRole.Viewer, r.F.Other, true);
        await r.F.AuthFixture.Auth.MutateAsync(s => { var a = s.RoleAssignments.Last(); foreach (var type in new[] { AuthScopeType.OrgUnit, AuthScopeType.Location }) s.RoleAssignmentScopes.Add(new(Guid.NewGuid().ToString("N"), a.AssignmentId, type, "ALL", true, 1)); return true; });
        using var proof = BusinessReadProof.Begin(true);
        Assert.Equal(0, (await service.BrowseAsync("USER", new())).List.Total);
        Assert.Equal(1, (await service.BrowseAsync("ADMIN", new())).List.Total); Assert.Equal(2, BusinessReadProof.Values.Count); Assert.False(BusinessReadProof.Mixed);
        var posts = r.F.Handler.Posts; r.F.Handler.Uris.Clear();
        var detail = await service.DetailWithChoicesAsync("ADMIN", r.Asset.AssetId, true);
        Assert.NotNull(detail.Choices); Assert.Equal(2, r.F.Handler.Uris.Count); Assert.Equal(posts, r.F.Handler.Posts);
        await r.F.AuthFixture.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER"); return true; });
        r.F.Handler.Uris.Clear(); await Assert.ThrowsAsync<AssetForbiddenException>(() => service.BrowseAsync("USER", new())); Assert.Empty(r.F.Handler.Uris);
    }
    [Fact]
    public async Task Compiled_Razor_root_and_detail_use_shared_context_without_extra_choice_or_history_read()
    {
        await using var host = await WebIntegrationHost.Start(batchReads: true); using var client = await host.Cookie("admin");
        foreach (var path in new[] { "/Assets", "/Assets/Detail/" + host.R.Asset.AssetId, "/Categories/Detail/" + host.R.F.Child })
        {
            host.R.F.Handler.Uris.Clear(); var posts = host.R.F.Handler.Posts;
            using var response = await client.GetAsync(path); Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, host.R.F.Handler.Uris.Count); Assert.Equal(posts, host.R.F.Handler.Posts);
        }
    }
    [Fact]
    public async Task Category_detail_history_share_one_snapshot_and_hidden_history_stays_not_found()
    {
        var f = await GoogleTreeMutationTests.Fixture.Start(); using var http = new HttpClient(f.Handler); var api = new GoogleApiRequestExecutor(f.Options, new Token(), http);
        var service = new CategoryCatalogService(new GoogleCategoryCatalogStore(f.Options, api), f.AuthFixture.Auth, f.Writer, f.AuthFixture.Clock);
        f.Handler.Uris.Clear(); using (GoogleMetadataReadMemo.Begin(true)) { await service.DetailAsync("ADMIN", f.Child); await service.HistoryAsync("ADMIN", f.Child); }
        Assert.Equal(3, f.Handler.Uris.Count);
        f.Handler.Uris.Clear(); var result = await service.DetailWithHistoryAsync("ADMIN", f.Child); Assert.Equal(2, f.Handler.Uris.Count); Assert.NotEmpty(result.History);
        await f.AuthFixture.Grant("USER", AuthRole.Viewer, f.Other, true);
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => service.DetailWithHistoryAsync("USER", f.Child));
        await f.AuthFixture.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER"); return true; });
        await Assert.ThrowsAsync<CategoryForbiddenException>(() => service.DetailWithHistoryAsync("USER", f.Other));
    }
    private sealed class Token : IGoogleAccessTokenProvider { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("FAKE-batch-token"); public void Invalidate() { } }
    private sealed class FaultOnce(HttpMessageHandler inner, CancellationTokenSource source) : DelegatingHandler(inner)
    {
        private bool pending = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { if (pending && request.RequestUri!.AbsolutePath.Contains("values:batchGet")) { pending = false; source.Cancel(); ct.ThrowIfCancellationRequested(); } return base.SendAsync(request, ct); }
    }
}
