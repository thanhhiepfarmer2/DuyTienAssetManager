using System.Net;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleTreeMutationTests
{
    [Theory]
    [InlineData("RELOCATE")]
    [InlineData("SOFT_DELETE")]
    public async Task Tree_asset_pointer_history_audit_commit_together_and_retry_survives_adapter_reconstruction(string mode)
    {
        var f = await Fixture.Start(); var a = await f.Create(); var beforePosts = f.Handler.Posts;
        var req = new TreeMutationRequest("ARCHIVE_CATEGORY", "Synthetic", f.Root, mode == "RELOCATE" ? f.Other : null, DeletePlan: mode); var p = await f.Service.PreviewAsync("ADMIN", req); await f.Commit(req, p, "joint");
        Assert.Equal(beforePosts + 1, f.Handler.Posts); Assert.Contains("TreeChanges", f.Handler.LastBody, StringComparison.Ordinal); using (var body = JsonDocument.Parse(f.Handler.LastBody))
        {
            var writes = body.RootElement.GetProperty("requests").EnumerateArray().Where(r => r.TryGetProperty("updateCells", out _)).Select(r => r.GetProperty("updateCells").GetProperty("start").GetProperty("sheetId").GetInt32()).ToArray();
            Assert.Contains(f.Handler.Tables["AssetOperations"].Id, writes); Assert.Contains(f.Handler.Tables["Assets"].Id, writes); Assert.Contains(f.Handler.Tables["SchemaVersions"].Id, writes);
        } Assert.DoesNotContain("formulaValue", f.Handler.LastBody, StringComparison.Ordinal);
        var state = await f.Store().ReadAsync(); Assert.Equal(a.AssetId, state.Assets.Assets.Single().AssetId); Assert.Equal(a.ObjectCode, state.Assets.Assets.Single().ObjectCode); Assert.Equal(mode == "SOFT_DELETE", state.Assets.Assets.Single().IsDeleted); Assert.Equal(1, state.Assets.Counters.Single().LastAllocated);
        Assert.Single(state.Categories.TreeChanges); Assert.True(f.Handler.Tables["TreeChangeDetails"].Rows.Count > 1); Assert.All(state.Categories.ActiveNodes.Where(n => n.CategoryId != f.Other), n => Assert.Equal("DRAFT_INACTIVE", n.State));
        var restarted = f.NewService(f.Store()); var replay = await restarted.CommitAsync("ADMIN", req, p.TreeRevision, p.AssetRevision, p.PreviewHash, "joint", true); Assert.True(replay.Replayed); Assert.Equal(beforePosts + 1, f.Handler.Posts);
        Assert.All(f.Handler.Uris, u => Assert.Contains("business-fake", u, StringComparison.Ordinal)); Assert.DoesNotContain(f.Handler.Uris, u => u.Contains("system-fake", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Lost_response_after_atomic_commit_reconciles_without_partial_tree_or_second_write()
    {
        var f = await Fixture.Start(); await f.Create(); var req = new TreeMutationRequest("ARCHIVE_CATEGORY", "Synthetic", f.Root, f.Other, DeletePlan: "RELOCATE"); var p = await f.Service.PreviewAsync("ADMIN", req); var posts = f.Handler.Posts; f.Handler.LoseResponseOnce = true;
        await f.Commit(req, p); Assert.Equal(posts + 1, f.Handler.Posts); var state = await f.Store().ReadAsync(); Assert.Equal(f.Other, state.Assets.Assets.Single().CategoryId); Assert.Single(state.Categories.TreeChanges);
    }
    [Fact]
    public async Task Precommit_failure_keeps_both_stores_and_explicit_same_key_retry_commits_once()
    {
        var f = await Fixture.Start(); await f.Create(); var before = await f.Store().ReadAsync(); var req = new TreeMutationRequest("ARCHIVE_CATEGORY", "Synthetic", f.Root, f.Other, DeletePlan: "RELOCATE"); var p = await f.Service.PreviewAsync("ADMIN", req); f.Handler.FailBeforeCommitOnce = true;
        await Assert.ThrowsAsync<ExternalStorageTransientException>(() => f.Commit(req, p, "retry")); var failed = await f.Store().ReadAsync(); Assert.Equal(before.Categories.ActiveTreeVersionId, failed.Categories.ActiveTreeVersionId); Assert.Equal(before.Assets.Revision, failed.Assets.Revision); Assert.Empty(failed.Categories.TreeChanges);
        await f.Commit(req, p, "retry"); Assert.Single((await f.Store().ReadAsync()).Categories.TreeChanges);
    }
    [Fact]
    public async Task Failed_reconciliation_after_commit_recovers_from_ledger_after_restart()
    {
        var f = await Fixture.Start(); await f.Create(); var req = new TreeMutationRequest("ARCHIVE_CATEGORY", "Synthetic", f.Root, f.Other, DeletePlan: "RELOCATE"); var p = await f.Service.PreviewAsync("ADMIN", req); var baseline = f.Handler.Posts;
        f.Handler.LoseResponseOnce = true; var pipeline = new FailReadAfterPost(f.Handler, baseline); var service = f.NewService(f.Store(pipeline));
        await Assert.ThrowsAsync<ExternalStorageTransientException>(() => service.CommitAsync("ADMIN", req, p.TreeRevision, p.AssetRevision, p.PreviewHash, "lost", true));
        var replay = await f.NewService(f.Store()).CommitAsync("ADMIN", req, p.TreeRevision, p.AssetRevision, p.PreviewHash, "lost", true); Assert.True(replay.Replayed); Assert.Equal(baseline + 1, f.Handler.Posts);
    }
    [Theory]
    [InlineData("missing-details")]
    [InlineData("tree-digest")]
    [InlineData("details")]
    [InlineData("counter")]
    [InlineData("null-result")]
    public async Task Malformed_or_partial_joint_journal_fails_closed_without_overwrite(string mode)
    {
        var f = await Fixture.Start(); await f.Create(); var req = new TreeMutationRequest("ARCHIVE_CATEGORY", "Synthetic", f.Root, f.Other, DeletePlan: "RELOCATE"); await f.Commit(req, await f.Service.PreviewAsync("ADMIN", req)); var writes = f.Handler.Posts;
        if (mode == "missing-details") f.Handler.Tables.Remove("TreeChangeDetails");
        else if (mode == "tree-digest") f.Handler.Tables["Categories"].Rows.Last()[5] = "Tampered name";
        else if (mode == "details") f.Handler.Tables["TreeChangeDetails"].Rows.Add(f.Handler.Tables["TreeChangeDetails"].Rows[1].ToArray());
        else if (mode == "counter") f.Handler.Tables["IdCounters"].Rows[1][1] = "0";
        else f.Handler.Tables["TreeChanges"].Rows[1][16] = "null";
        await Assert.ThrowsAsync<ExternalStorageException>(() => f.Store().ReadAsync()); Assert.Equal(writes, f.Handler.Posts);
    }
    [Fact]
    public async Task Unsupported_objects_are_not_assumed_empty_and_no_existing_google_schema_is_overwritten()
    {
        var f = await Fixture.Start(); f.Handler.Tables.Add("Documents", new(9000, 1)); f.Handler.Tables["Documents"].Rows.Add(["Unmanaged"]); var writes = f.Handler.Posts;
        await Assert.ThrowsAsync<ExternalStorageException>(() => f.Service.PreviewAsync("ADMIN", new("MOVE_CATEGORY", "Synthetic", f.Root, f.Other))); Assert.Equal(writes, f.Handler.Posts);
    }
    [Fact]
    public async Task Large_665_node_synthetic_branch_uses_per_row_diff_and_complete_draft_snapshot()
    {
        var rows = new List<CategoryInput> { new("F1-F3 Mã ngang",2,"1",null,1,"Root synthetic","ROT"),new("F1-F3 Mã ngang",3,"2",null,1,"Destination synthetic","OTH") };
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for (var i = 0; i < 663; i++) rows.Add(new("F1-F3 Mã ngang",i+4,"1."+(i+1),"1",2,"Nhánh giả Unicode "+i,"X"+alphabet[i/36]+alphabet[i%36]));
        var f = await Fixture.Start(rows); var req = new TreeMutationRequest("MOVE_CATEGORY", "Synthetic-only665; không chứng minh workbook công ty", f.Root, f.Other); var p = await f.Service.PreviewAsync("ADMIN", req); Assert.True(p.CanCommit); Assert.Equal(664, p.Categories.Count); await f.Commit(req, p);
        var state = await f.Store().ReadAsync(); Assert.Equal(665, state.Categories.ActiveNodes.Length); Assert.Equal(1331, f.Handler.Tables["Categories"].Rows.Count); Assert.Equal(665, f.Handler.Tables["TreeChangeDetails"].Rows.Count); Assert.True(System.Text.Encoding.UTF8.GetByteCount(f.Handler.LastBody) < 1800*1024);
        Assert.All(f.Handler.Tables["TreeChanges"].Rows.SelectMany(r=>r), cell=>Assert.True(cell.Length<=45000));
    }
    [Theory]
    [InlineData("{")]
    [InlineData("{\"sheets\":null}")]
    [InlineData("{\"sheets\":[{\"properties\":{}}]}")]
    public async Task Malformed_metadata_is_safe_storage_error_and_never_causes_write(string malformed)
    {
        var f=await Fixture.Start();var posts=f.Handler.Posts;using var pipeline=new MetadataFault(f.Handler,malformed);
        await Assert.ThrowsAsync<ExternalStorageException>(()=>f.Store(pipeline).ReadAsync());Assert.Equal(posts,f.Handler.Posts);
    }
    private sealed class MetadataFault(HttpMessageHandler inner,string malformed):DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>request.RequestUri!.Query.Contains("fields=sheets.properties.title",StringComparison.Ordinal)?Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(malformed,System.Text.Encoding.UTF8,"application/json")}):base.SendAsync(request,ct);
    }
    private sealed class FailReadAfterPost(GoogleCategoryCatalogStoreTests.CategorySheetsHandler h, int before) : DelegatingHandler(h)
    { private bool pending=true; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) { if(pending && h.Posts>before && request.Method==HttpMethod.Get) {pending=false;throw new HttpRequestException("Synthetic failed reconciliation");}return base.SendAsync(request,ct); } }
    internal sealed class Fixture
    {
        public GoogleCategoryCatalogStoreTests.CategorySheetsHandler Handler { get; } = new();
        public CategoryFixture AuthFixture { get; } = new();
        public InProcessSingleWriterCoordinator Writer { get; } = new();
        public string Root { get; private set; } = ""; public string Other { get; private set; } = ""; public string Child { get; private set; } = "";
        public TreeMutationService Service { get; private set; } = null!;
        public GoogleWorkspaceOptions Options { get; } = new() { SpreadsheetId="business-fake",SystemSpreadsheetId="system-fake",OAuthClientId="fake-client",OAuthClientSecret="fake-config",OAuthRefreshToken="fake-config",MaxRetryAttempts=1 };
        public GoogleTreeMutationStore Store(HttpMessageHandler? pipeline=null) {var api=new GoogleApiRequestExecutor(Options,new Token(),new HttpClient(pipeline??Handler));return new(Options,api,new(Options,api),new(Options,api));}
        public TreeMutationService NewService(ITreeMutationStore store)=>new(store,AuthFixture.Auth,Writer,AuthFixture.Clock);
        public static async Task<Fixture> Start(IReadOnlyList<CategoryInput>? rows=null)
        {
            var f=new Fixture();var api=new GoogleApiRequestExecutor(f.Options,new Token(),new HttpClient(f.Handler));var categoryStore=new GoogleCategoryCatalogStore(f.Options,api);var catalog=new CategoryCatalogService(categoryStore,f.AuthFixture.Auth,f.Writer,f.AuthFixture.Clock);
            rows ??= CategoryFixture.Rows.Concat(new[]{new CategoryInput("F1-F3 Mã ngang",5,"2",null,1,"Đích giả","OTH")}).ToArray();
            var doc=CategoryFixture.Doc(rows);var preview=await catalog.PreviewAsync("ADMIN",doc);await catalog.ImportAsync("ADMIN",doc,preview.Revision,preview.PreviewHash,"fixture-import",true);var state=await categoryStore.ReadAsync();
            f.Root=state.ActiveNodes.Single(n=>n.SegmentCode=="ROT").CategoryId;f.Other=state.ActiveNodes.Single(n=>n.SegmentCode=="OTH").CategoryId;f.Child=state.ActiveNodes.First(n=>n.ParentId==f.Root).CategoryId;
            var references=AssetFixture.References();var id=3000;
            foreach(var pair in GoogleAssetCatalogStore.ReferenceHeaders){var t=new GoogleCategoryCatalogStoreTests.Table(id++,pair.Value.Length);t.Rows.Add(pair.Value.ToArray());f.Handler.Tables[pair.Key]=t;}
            f.Handler.Tables["CodeRules"].Rows.Add(["RULE-FAKE",f.Root,"TEST","ROT","DEMO","True","1"]);
            f.Handler.Tables["OrgUnits"].Rows.AddRange(references.OrgUnits.Select(o=>new[]{o.Id,o.ParentId??"",o.Name,"ACTIVE","1"}));f.Handler.Tables["Locations"].Rows.AddRange(references.Locations.Select(l=>new[]{l.Id,l.ParentId??"",l.OrgUnitId,l.Name,"ACTIVE","1"}));f.Service=f.NewService(f.Store());return f;
        }
        public async Task<AssetRecord> Create(){var api=new GoogleApiRequestExecutor(Options,new Token(),new HttpClient(Handler));var service=new AssetCrudService(new GoogleAssetCatalogStore(Options,api),new GoogleCategoryCatalogStore(Options,api),AuthFixture.Auth,Writer,AuthFixture.Clock);return(await service.CreateAsync("ADMIN",new(new("Máy giả",Child,"OU-A","LOC-A",Aliases:["OLD-FAKE"]),0,(await Store().ReadAsync()).Categories.ActiveTreeVersionId!),Guid.NewGuid().ToString("N"))).Asset;}
        public Task<TreeMutationResult> Commit(TreeMutationRequest req,TreeMutationPreview p,string?key=null)=>Service.CommitAsync("ADMIN",req,p.TreeRevision,p.AssetRevision,p.PreviewHash,key??Guid.NewGuid().ToString("N"),true);
        private sealed class Token:IGoogleAccessTokenProvider{public Task<string> GetAccessTokenAsync(CancellationToken ct=default)=>Task.FromResult("fake-adapter-token");public void Invalidate(){}}
    }
}
