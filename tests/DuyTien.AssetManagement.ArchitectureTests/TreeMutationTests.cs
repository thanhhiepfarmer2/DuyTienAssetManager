using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class TreeMutationTests
{
    [Fact]
    public async Task Edit_name_code_definition_keeps_ids_assets_draft_and_path_history()
    {
        var f = await TreeMutationFixture.Start(); var a = (await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "create")).Asset;
        var request = new TreeMutationRequest("EDIT_CATEGORY", "Fixture giả", "CAT-MACHINE", Name: "Máy đổi tên giả", SegmentCode: "NEW", Definition: "Định nghĩa giả");
        var preview = await f.Service.PreviewAsync("ADMIN", request); Assert.True(preview.CanCommit); Assert.Equal(2, preview.AffectedCategoryCount); Assert.Equal(1, preview.AffectedAssetCount);
        var result = await f.Commit(request, preview); Assert.Equal("DRAFT", f.Store.State.Categories.Versions.Last().State);
        var tree = await f.Catalog.ListAsync("ADMIN"); Assert.Contains(tree.Nodes, n => n.FullCode == "NEW.MCN");
        var current = (await f.AssetService.DetailAsync("ADMIN", a.AssetId)).Asset; Assert.Equal(a.AssetId, current.AssetId); Assert.Equal(a.ObjectCode, current.ObjectCode); Assert.Equal("TST", current.IssuedCcc);
        var history = await f.Catalog.HistoryAsync("ADMIN", "CAT-CHILD"); Assert.Contains(history, h => h.FullCode == "TST.MCN"); Assert.Contains(history, h => h.FullCode == "NEW.MCN");
        Assert.NotEqual(preview.TreeVersionId, result.TreeVersionId); Assert.Single(f.Store.State.Categories.TreeChanges);
    }
    [Fact]
    public async Task Move_subtree_rebuilds_levels_full_path_scoped_txt_and_asset_lookup_without_reallocation()
    {
        var f = await TreeMutationFixture.Start(); var a = (await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "create")).Asset;
        var request = new TreeMutationRequest("MOVE_CATEGORY", "Giả", "CAT-MACHINE", "CAT-OTHER"); var p = await f.Service.PreviewAsync("ADMIN", request); Assert.True(p.CanCommit); await f.Commit(request, p);
        var nodes = f.Store.State.Categories.ActiveNodes; Assert.Equal(2, nodes.Single(n => n.CategoryId == "CAT-MACHINE").FLevel); Assert.Equal(3, nodes.Single(n => n.CategoryId == "CAT-CHILD").FLevel); Assert.Equal("ZZZ.TST.MCN", nodes.Single(n => n.CategoryId == "CAT-CHILD").FullCode);
        var txt = System.Text.Encoding.UTF8.GetString(await f.Catalog.ExportAsync("ADMIN")); Assert.Contains("    ", txt, StringComparison.Ordinal); Assert.Contains("ZZZ.TST.MCN", txt, StringComparison.Ordinal); Assert.Contains("DRAFT", txt, StringComparison.Ordinal);
        Assert.Equal(a, (await f.AssetService.DetailAsync("ADMIN", a.AssetId)).Asset); Assert.Equal(1, (await f.AssetService.ListAsync("ADMIN", new(CategoryId: "CAT-OTHER"))).Total);
    }
    [Fact]
    public async Task Cycle_and_f11_are_preview_errors_and_commit_is_blocked_while_f10_is_valid()
    {
        var f = await TreeMutationFixture.Start(); var cycle = new TreeMutationRequest("MOVE_CATEGORY", "Giả", "CAT-MACHINE", "CAT-CHILD"); var p = await f.Service.PreviewAsync("ADMIN", cycle); Assert.False(p.CanCommit); Assert.Contains(p.Issues, i => i.Contains("Cycle", StringComparison.Ordinal)); await Assert.ThrowsAsync<CategoryValidationException>(() => f.Commit(cycle, p));
        var nodes = f.Store.State.Categories.Categories; nodes.Clear();
        for (var i = 1; i <= 10; i++) nodes.Add(new("N" + i, "TREE-FAKE", i == 1 ? null : "N" + (i - 1), i, i, "Cấp giả " + i, "N" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), string.Join('.', Enumerable.Range(1, i).Select(x => "N" + x.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))), "", "DRAFT", 1, "FAKE-" + i, "FIXTURE_GIA", i, i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Empty(CategoryTreeShape.Validate(nodes));
        var invalid = new TreeMutationRequest("MOVE_CATEGORY", "Giả", "N1", "N10"); var bad = await f.Service.PreviewAsync("ADMIN", invalid); Assert.False(bad.CanCommit);
        var extra = nodes[0] with { CategoryId = "EXTRA", ParentId = null, FLevel = 1, SegmentCode = "EXT", FullCode = "EXT", SourceKey = "EXTRA" }; nodes.Add(extra);
        var eleven = new TreeMutationRequest("MOVE_CATEGORY", "Giả", "EXTRA", "N10"); var overflow = await f.Service.PreviewAsync("ADMIN", eleven); Assert.Contains(overflow.Issues, x => x.Contains("F11", StringComparison.Ordinal));
        var ten = new TreeMutationRequest("MOVE_CATEGORY", "Giả", "EXTRA", "N9"); var valid = await f.Service.PreviewAsync("ADMIN", ten); Assert.True(valid.CanCommit); await f.Commit(ten, valid); Assert.Equal(10, f.Store.State.Categories.ActiveNodes.Single(n => n.CategoryId == "EXTRA").FLevel);
    }
    [Theory]
    [InlineData("RELOCATE")]
    [InlineData("SOFT_DELETE")]
    public async Task Used_branch_requires_plan_and_confirmation_and_atomically_relocates_or_soft_deletes(string mode)
    {
        var f = await TreeMutationFixture.Start(); var a = (await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "create")).Asset;
        var empty = new TreeMutationRequest("ARCHIVE_CATEGORY", "Giả", "CAT-MACHINE"); var blocked = await f.Service.PreviewAsync("ADMIN", empty); Assert.False(blocked.CanCommit); Assert.Equal(2, blocked.AffectedCategoryCount); Assert.Equal(1, blocked.AffectedAssetCount); await Assert.ThrowsAsync<CategoryValidationException>(() => f.Commit(empty, blocked));
        var request = empty with { DeletePlan = mode, DestinationCategoryId = mode == "RELOCATE" ? "CAT-OTHER" : null }; var p = await f.Service.PreviewAsync("ADMIN", request);
        await Assert.ThrowsAsync<CategoryValidationException>(() => f.Service.CommitAsync("ADMIN", request, p.TreeRevision, p.AssetRevision, p.PreviewHash, "without-confirm", false));
        await f.Commit(request, p); var saved = f.Store.State.Assets.Assets.Single(); Assert.Equal(a.ObjectCode, saved.ObjectCode); Assert.Equal(a.AssetId, saved.AssetId); Assert.Equal("TST", saved.IssuedCcc); Assert.Equal(mode == "SOFT_DELETE", saved.IsDeleted); Assert.Equal(2, saved.Version);
        Assert.All(f.Store.State.Categories.ActiveNodes.Where(n => n.CategoryId != "CAT-OTHER"), n => Assert.Equal("DRAFT_INACTIVE", n.State)); Assert.Single((await f.Catalog.ListAsync("ADMIN")).Nodes);
        Assert.Equal(1, f.Store.State.Assets.Counters.Single().LastAllocated); Assert.Single(f.Store.State.Categories.TreeChanges); Assert.Equal(2, f.Store.State.Assets.Operations.Count);
    }
    [Fact]
    public async Task Empty_category_can_be_stopped_but_target_inside_branch_and_historical_code_reuse_are_blocked()
    {
        var f = await TreeMutationFixture.Start(); var empty = new TreeMutationRequest("ARCHIVE_CATEGORY", "Giả", "CAT-OTHER"); var p = await f.Service.PreviewAsync("ADMIN", empty); Assert.True(p.CanCommit); await f.Commit(empty, p); Assert.Equal(2, (await f.Catalog.ListAsync("ADMIN")).Total);
        var invalid = new TreeMutationRequest("EDIT_CATEGORY", "Giả", "CAT-CHILD", Name: "Tên giả", SegmentCode: "ZZZ"); var bad = await f.Service.PreviewAsync("ADMIN", invalid); Assert.False(bad.CanCommit);
        var relocate = new TreeMutationRequest("ARCHIVE_CATEGORY", "Giả", "CAT-MACHINE", "CAT-CHILD", DeletePlan: "RELOCATE"); Assert.False((await f.Service.PreviewAsync("ADMIN", relocate)).CanCommit);
    }
    [Fact]
    public async Task Bulk_move_handles_multiple_assets_deleted_state_versions_id_code_aliases_and_noop()
    {
        var f = await TreeMutationFixture.Start(); var one = (await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "one")).Asset; var two = (await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "two")).Asset;
        await f.AssetService.DeleteAsync("ADMIN", two.AssetId, new(1, "Giả"), "delete");
        var request = new TreeMutationRequest("MOVE_ASSETS", "Giả", DestinationCategoryId: "CAT-OTHER", AssetIds: [one.AssetId, two.AssetId], DestinationOrgUnitId: "OU-B", DestinationLocationId: "LOC-B"); var p = await f.Service.PreviewAsync("ADMIN", request); Assert.Equal(2, p.ChangedAssetCount); await f.Commit(request, p);
        foreach (var a in f.Store.State.Assets.Assets) { Assert.Equal("OU-B", a.OrgUnitId); Assert.Equal("LOC-B", a.LocationId); Assert.Equal("CAT-OTHER", a.CategoryId); Assert.Equal("TST", a.IssuedCcc); }
        Assert.True(f.Store.State.Assets.Assets.Single(a => a.AssetId == two.AssetId).IsDeleted); Assert.Equal(one.ObjectCode, f.Store.State.Assets.Assets.Single(a => a.AssetId == one.AssetId).ObjectCode);
        Assert.False((await f.Service.PreviewAsync("ADMIN", request)).CanCommit);
    }
    [Fact]
    public async Task Stale_tree_asset_or_reference_preview_rejects_and_same_key_retries_after_service_reconstruction()
    {
        var f = await TreeMutationFixture.Start(); var request = new TreeMutationRequest("EDIT_CATEGORY", "Giả", "CAT-MACHINE", Name: "Sửa", SegmentCode: "NEW"); var p = await f.Service.PreviewAsync("ADMIN", request);
        await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "interleaving-create"); await Assert.ThrowsAsync<CategoryConflictException>(() => f.Commit(request, p));
        p = await f.Service.PreviewAsync("ADMIN", request); f.Store.State.Assets.Locations[0] = f.Store.State.Assets.Locations[0] with { Name = "Reference thay đổi" }; await Assert.ThrowsAsync<CategoryConflictException>(() => f.Commit(request, p));
        p = await f.Service.PreviewAsync("ADMIN", request); var result = await f.Commit(request, p, "retry"); var restarted = f.NewService(); var replay = await restarted.CommitAsync("ADMIN", request, p.TreeRevision, p.AssetRevision, p.PreviewHash, "retry", true); Assert.True(replay.Replayed); Assert.Equal(result.TreeRevision, replay.TreeRevision);
        await Assert.ThrowsAsync<CategoryConflictException>(() => restarted.CommitAsync("ADMIN", request with { Name = "Khác" }, p.TreeRevision, p.AssetRevision, p.PreviewHash, "retry", true)); Assert.Equal(1, f.Store.JointWrites);
    }
    [Fact]
    public async Task Viewer_and_cross_assignment_move_fail_without_exposing_counts_and_asset_permission_is_required()
    {
        var f = await TreeMutationFixture.Start(); await f.Base.Grant(AuthRole.Viewer); var req = new TreeMutationRequest("MOVE_CATEGORY", "Giả", "CAT-MACHINE", "CAT-OTHER"); await Assert.ThrowsAsync<CategoryForbiddenException>(() => f.Service.PreviewAsync("USER", req));
        await f.Base.Grant(AuthRole.CatalogManager, category: "CAT-MACHINE"); await f.Base.Grant(AuthRole.CatalogManager, category: "CAT-OTHER"); await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.PreviewAsync("USER", req));
        await f.Base.Grant(AuthRole.CatalogManager); await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "create"); await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.PreviewAsync("USER", req));
        await f.Base.Grant(AuthRole.AssetEditor); Assert.True((await f.Service.PreviewAsync("USER", req)).CanCommit);
    }
    [Fact]
    public async Task Limited_scope_cannot_preview_hidden_asset_or_child_and_bulk_assignment_cannot_cross_combine()
    {
        var f = await TreeMutationFixture.Start(); await f.Base.Grant(AuthRole.AssetEditor, "OU-A", "LOC-A", "CAT-MACHINE"); var a = (await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "one")).Asset;
        var req = new TreeMutationRequest("MOVE_ASSETS", "Giả", DestinationCategoryId: "CAT-OTHER", AssetIds: [a.AssetId]); await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.PreviewAsync("USER", req));
        await f.Base.Grant(AuthRole.AssetEditor, "OU-A", "LOC-A", "CAT-OTHER"); await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.PreviewAsync("USER", req));
        await f.Base.Grant(AuthRole.AssetEditor); var p = await f.Service.PreviewAsync("USER", req); Assert.True(p.CanCommit);
        await f.Base.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER"); return true; }); await Assert.ThrowsAsync<CategoryForbiddenException>(() => f.Service.CommitAsync("USER", req, p.TreeRevision, p.AssetRevision, p.PreviewHash, "revoked", true));
    }
    [Theory]
    [InlineData("kind")]
    [InlineData("reason")]
    [InlineData("name")]
    [InlineData("code")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    public async Task Invalid_requests_and_missing_scope_are_blocked_without_write(string mode)
    {
        var f = await TreeMutationFixture.Start(); var req = new TreeMutationRequest("EDIT_CATEGORY", "Giả", "CAT-MACHINE", Name: "Tên", SegmentCode: "NEW");
        req = mode switch { "kind" => req with { Kind = "APPROVE" }, "reason" => req with { Reason = "" }, "name" => req with { Name = "" }, "code" => req with { SegmentCode = "?!!" }, "duplicate" => new("MOVE_ASSETS", "Giả", DestinationCategoryId: "CAT-OTHER", AssetIds: ["same", "same"]), _ => req with { CategoryId = "missing" } };
        if (mode == "code") Assert.False((await f.Service.PreviewAsync("ADMIN", req)).CanCommit); else if (mode == "missing") await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.PreviewAsync("ADMIN", req)); else await Assert.ThrowsAsync<CategoryValidationException>(() => f.Service.PreviewAsync("ADMIN", req)); Assert.Equal(0, f.Store.JointWrites);
    }
    [Fact]
    public async Task History_masks_unreadable_ancestors_and_filters_versions_outside_current_assignment()
    {
        var f=await TreeMutationFixture.Start();var req=new TreeMutationRequest("MOVE_CATEGORY","Giả","CAT-MACHINE","CAT-OTHER");await f.Commit(req,await f.Service.PreviewAsync("ADMIN",req));
        await f.Base.Grant(AuthRole.Viewer,category:"CAT-CHILD",descendants:false);
        var history=await f.Catalog.HistoryAsync("USER","CAT-CHILD");Assert.Equal(2,history.Count);Assert.All(history,x=>{Assert.Equal("MCN",x.FullCode);Assert.True(x.PathIsPartial);Assert.DoesNotContain("TST",x.Path,StringComparison.Ordinal);Assert.DoesNotContain("ZZZ",x.Path,StringComparison.Ordinal);});
        await Assert.ThrowsAsync<CategoryNotFoundException>(()=>f.Catalog.HistoryAsync("USER","CAT-MACHINE"));
        await f.Base.Auth.MutateAsync(a=>{a.RoleAssignments.RemoveAll(x=>x.UserId=="USER");return true;});await f.Base.Grant(AuthRole.Viewer,category:"CAT-OTHER");
        var limited=await f.Catalog.HistoryAsync("USER","CAT-CHILD");Assert.Single(limited);Assert.Equal(f.Store.State.Categories.ActiveTreeVersionId,limited[0].TreeVersionId);Assert.Equal("ZZZ.TST.MCN",limited[0].FullCode);
    }
    [Fact]
    public async Task Import_cannot_bypass_bulk_policy_by_moving_an_ancestor_of_an_existing_asset()
    {
        var f = await TreeMutationFixture.Start(); await f.AssetService.CreateAsync("ADMIN", f.AssetRequest(), "asset");
        CategoryInput[] rows = [new("F1-F3 Mã ngang",2,"1",null,1,"Đích","ZZZ",CategoryId:"CAT-OTHER"),new("F1-F3 Mã ngang",3,"1.1","1",2,"Máy","TST",CategoryId:"CAT-MACHINE"),new("F1-F3 Mã ngang",4,"1.1.1","1.1",3,"Con","MCN",CategoryId:"CAT-CHILD")];
        var doc = CategoryFixture.Doc(rows,"changed-ancestor"); var p = await f.Catalog.PreviewAsync("ADMIN",doc);
        await Assert.ThrowsAsync<CategoryConflictException>(() => f.Catalog.ImportAsync("ADMIN",doc,p.Revision,p.PreviewHash,"bypass",true));
        Assert.Equal("TREE-FAKE",f.Store.State.Categories.ActiveTreeVersionId); Assert.Single(f.Store.State.Assets.Assets);
    }
    [Fact]
    public async Task Failure_before_commit_keeps_both_snapshots_and_reader_waits_for_writer_without_partial_tree()
    {
        var f = await TreeMutationFixture.Start(); var req = new TreeMutationRequest("MOVE_CATEGORY", "Giả", "CAT-MACHINE", "CAT-OTHER"); var p = await f.Service.PreviewAsync("ADMIN", req); f.Store.FailJointWrite = true;
        await Assert.ThrowsAsync<ExternalStorageException>(() => f.Commit(req, p)); Assert.Equal("TREE-FAKE", f.Store.State.Categories.ActiveTreeVersionId); Assert.Empty(f.Store.State.Categories.TreeChanges);
        f.Store.FailJointWrite = false; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = f.Writer.ExecuteAsync(async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); return true; }); await entered.Task;
        var reader = f.AssetService.ListAsync("ADMIN", new()); Assert.False(reader.IsCompleted); release.SetResult(); await held; await reader;
    }
}
internal sealed class TreeMutationFixture
{
    public AssetFixture Base { get; }
    public InProcessSingleWriterCoordinator Writer { get; } = new();
    public TreeMutationMemoryStore Store { get; }
    public TreeMutationService Service { get; }
    public AssetCrudService AssetService { get; }
    public CategoryCatalogService Catalog { get; }
    private TreeMutationFixture(AssetFixture b, TreeMutationMemoryStore store)
    { Base = b; Store = store; Service = NewService(); AssetService = new(store.AssetAdapter, store.CategoryAdapter, b.Auth, Writer, b.Clock); Catalog = new(store.CategoryAdapter, b.Auth, Writer, b.Clock, store.AssetAdapter); }
    public static async Task<TreeMutationFixture> Start()
    {
        var b = await AssetFixture.Start(); var c = b.Categories.State.Clone(); var child = c.Categories.FindIndex(n => n.CategoryId == "CAT-CHILD"); c.Categories[child] = c.Categories[child] with { FullCode = "TST.MCN" };
        c.Versions.Add(new("TREE-FAKE", "DRAFT", "FIXTURE_GIA", "FIXTURE_GIA", "FIXTURE_GIA", b.Clock.GetUtcNow(), "ADMIN"));
        return new(b, new(new(c, b.Memory.State.Clone())));
    }
    public TreeMutationService NewService() => new(Store, Base.Auth, Writer, Base.Clock);
    public AssetWriteRequest AssetRequest() => Base.Request() with { TreeVersionId = Store.State.Categories.ActiveTreeVersionId! };
    public Task<TreeMutationResult> Commit(TreeMutationRequest r, TreeMutationPreview p, string? key = null) => Service.CommitAsync("ADMIN", r, p.TreeRevision, p.AssetRevision, p.PreviewHash, key ?? Guid.NewGuid().ToString("N"), true);
}
internal sealed class TreeMutationMemoryStore(TreeMutationSnapshot initial) : ITreeMutationStore
{
    public TreeMutationSnapshot State { get; private set; } = new(initial.Categories.Clone(), initial.Assets.Clone());
    public int JointWrites { get; private set; }
    public bool FailJointWrite { get; set; }
    public bool FailReadAfterJointCommit { get; set; }
    public bool FailRead { get; set; }
    public ICategoryCatalogStore CategoryAdapter => new Cat(this);
    public IAssetCatalogStore AssetAdapter => new Asset(this);
    public Task<TreeMutationSnapshot> ReadAsync(CancellationToken ct = default) => FailRead ? throw new ExternalStorageException("PRIVATE-TREE-CANARY") : Task.FromResult(new TreeMutationSnapshot(State.Categories.Clone(), State.Assets.Clone()));
    public Task CommitAsync(TreeMutationSnapshot candidate, long expectedTreeRevision, long expectedAssetRevision, CancellationToken ct = default)
    { if (FailJointWrite) throw new ExternalStorageException("PRIVATE-TREE-CANARY"); if (State.Categories.Revision != expectedTreeRevision || State.Assets.Revision != expectedAssetRevision) throw new CategoryConflictException("Fake conflict"); State = new(candidate.Categories.Clone(), candidate.Assets.Clone()); JointWrites++; FailRead |= FailReadAfterJointCommit; return Task.CompletedTask; }
    private sealed class Cat(TreeMutationMemoryStore s) : ICategoryCatalogStore
    {
        public async Task<CategoryCatalogState> ReadAsync(CancellationToken ct = default) => (await s.ReadAsync(ct)).Categories;
        public Task CommitAsync(CategoryCatalogState c, long expectedRevision, CancellationToken ct = default) { if (s.State.Categories.Revision != expectedRevision) throw new CategoryConflictException("Fake conflict"); s.State = new(c.Clone(), s.State.Assets.Clone()); return Task.CompletedTask; }
    }
    private sealed class Asset(TreeMutationMemoryStore s) : IAssetCatalogStore
    {
        public async Task<AssetCatalogState> ReadAsync(CancellationToken ct = default) => (await s.ReadAsync(ct)).Assets;
        public Task CommitAsync(AssetCatalogState a, long expectedRevision, CancellationToken ct = default) { if (s.State.Assets.Revision != expectedRevision) throw new AssetConflictException("Fake conflict"); s.State = new(s.State.Categories.Clone(), a.Clone()); return Task.CompletedTask; }
    }
}
