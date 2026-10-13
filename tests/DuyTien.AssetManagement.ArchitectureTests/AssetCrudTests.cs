using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AssetCrudTests
{
    [Fact]
    public async Task Concurrent_create_has_unique_codes_and_same_key_retry_has_one_entity()
    {
        var f = await AssetFixture.Start();
        var result = await Task.WhenAll(f.Service.CreateAsync("ADMIN", f.Request(), "one"), f.Service.CreateAsync("ADMIN", f.Request(), "two"));
        Assert.Equal(2, result.Select(r => r.Asset.ObjectCode).Distinct().Count());
        Assert.Equal(new[] { "TEST-TST-000001", "TEST-TST-000002" }, result.Select(r => r.Asset.ObjectCode).Order());
        var repeated = await Task.WhenAll(f.Service.CreateAsync("ADMIN", f.Request(), "three"), f.Service.CreateAsync("ADMIN", f.Request(), "three"));
        Assert.Equal(repeated[0].Asset.AssetId, repeated[1].Asset.AssetId); Assert.Single(repeated, r => r.Replayed);
        Assert.Equal(3, f.Memory.Writes); Assert.Equal(3, f.Memory.State.Counters.Single().LastAllocated);
    }
    [Fact]
    public async Task Restarted_service_replays_operation_and_rejects_changed_payload_or_reused_action()
    {
        var f = await AssetFixture.Start(); var created = await f.Service.CreateAsync("ADMIN", f.Request(), "key");
        var restarted = f.NewService(f.Memory); var repeat = await restarted.CreateAsync("ADMIN", f.Request(), "key"); Assert.True(repeat.Replayed); Assert.Equal(created.Asset, repeat.Asset);
        await Assert.ThrowsAsync<AssetConflictException>(() => restarted.CreateAsync("ADMIN", f.Request(f.Input() with { Name = "Khác" }), "key"));
        await Assert.ThrowsAsync<AssetConflictException>(() => restarted.DeleteAsync("ADMIN", created.Asset.AssetId, new(1, "Giả"), "key")); Assert.Equal(1, f.Memory.Writes);
    }
    [Fact]
    public async Task Update_location_classification_and_name_keeps_identity_issued_group_and_counter()
    {
        var f = await AssetFixture.Start(); var a = (await f.Create()).Asset;
        var moved = (await f.Service.UpdateAsync("ADMIN", a.AssetId, f.Request(f.Input() with { Name = "Máy đã sửa", CategoryId = "CAT-OTHER", LocationId = "LOC-A-CHILD" }, 1), "edit")).Asset;
        Assert.Equal(a.AssetId, moved.AssetId); Assert.Equal(a.ObjectCode, moved.ObjectCode); Assert.Equal("TST", moved.IssuedCcc); Assert.Equal("CAT-OTHER", moved.CategoryId); Assert.Equal(2, moved.Version); Assert.Single(f.Memory.State.Counters);
        var view = await f.Service.DetailAsync("ADMIN", a.AssetId); Assert.Equal("DRAFT", view.CategoryState); Assert.Equal("DRAFT_DEMO", view.Asset.DataState);
    }
    [Fact]
    public async Task Two_editors_old_version_conflicts_without_overwriting_new_value()
    {
        var f = await AssetFixture.Start(); await f.Grant(AuthRole.AssetEditor); var a = (await f.Create()).Asset;
        await f.Service.UpdateAsync("ADMIN", a.AssetId, f.Request(f.Input() with { Name = "Bản mới" }, 1), "edit-one");
        await Assert.ThrowsAsync<AssetConflictException>(() => f.Service.UpdateAsync("USER", a.AssetId, f.Request(f.Input() with { Name = "Bản cũ" }, 1), "edit-two"));
        Assert.Equal("Bản mới", (await f.Service.DetailAsync("ADMIN", a.AssetId)).Asset.Name); Assert.Equal(2, f.Memory.Writes);
    }
    [Fact]
    public async Task Soft_delete_restore_preserves_code_and_historical_alias_is_reserved_and_searchable()
    {
        var f = await AssetFixture.Start(); var a = (await f.Create(f.Input() with { Aliases = ["0000-OLD", "MÃ-CŨ"] })).Asset;
        await f.Service.UpdateAsync("ADMIN", a.AssetId, f.Request(f.Input() with { Aliases = ["MÃ-CŨ"] }, 1), "alias-retire");
        Assert.Equal(1, (await f.Service.ListAsync("ADMIN", new(Q: "0000-old"))).Total);
        await Assert.ThrowsAsync<AssetConflictException>(() => f.Create(f.Input() with { Aliases = ["0000-OLD"] }));
        await f.Service.DeleteAsync("ADMIN", a.AssetId, new(2, "Dữ liệu giả"), "delete"); Assert.Equal(0, (await f.Service.ListAsync("ADMIN", new())).Total);
        var second = (await f.Create()).Asset; Assert.Equal("TEST-TST-000002", second.ObjectCode);
        var restored = (await f.Service.RestoreAsync("ADMIN", a.AssetId, new(3, "Khôi phục giả"), "restore")).Asset;
        Assert.Equal(a.ObjectCode, restored.ObjectCode); Assert.Equal(a.AssetId, restored.AssetId); Assert.Equal(4, restored.Version); Assert.False(restored.IsDeleted);
        Assert.Equal(2, f.Memory.State.Aliases.Count); Assert.Contains(f.Memory.State.Aliases, x => !x.IsActive);
    }
    [Theory]
    [InlineData("Name")]
    [InlineData("CategoryId")]
    [InlineData("OrgUnitId")]
    [InlineData("LocationId")]
    [InlineData("Type")]
    [InlineData("Aliases")]
    [InlineData("Control")]
    [InlineData("LongName")]
    public async Task Invalid_fields_are_rejected_before_counter_or_write(string field)
    {
        var f = await AssetFixture.Start(); var input = f.Input();
        input = field switch { "Name" => input with { Name = " " }, "CategoryId" => input with { CategoryId = "" }, "OrgUnitId" => input with { OrgUnitId = "" }, "LocationId" => input with { LocationId = "" }, "Type" => input with { Type = "PERSON" }, "Aliases" => input with { Aliases = ["a", "A"] }, "Control" => input with { Name = "bad\0name" }, _ => input with { Name = new string('a', 301) } };
        await Assert.ThrowsAsync<AssetValidationException>(() => f.Create(input)); Assert.Empty(f.Memory.State.Assets); Assert.Empty(f.Memory.State.Counters); Assert.Equal(0, f.Memory.Writes);
    }
    [Theory]
    [InlineData("absent")]
    [InlineData("unapproved")]
    [InlineData("proposed")]
    [InlineData("ambiguous")]
    public async Task Draft_category_does_not_authorize_unapproved_or_missing_code_rule(string mode)
    {
        var f = await AssetFixture.Start(); var rule = f.Memory.State.Rules[0];
        if (mode == "absent") f.Memory.State.Rules.Clear(); else if (mode == "ambiguous") f.Memory.State.Rules.Add(rule with { RuleId = "SECOND" });
        else f.Memory.State.Rules[0] = mode == "unapproved" ? rule with { ApprovedForDemo = false } : rule with { Status = "PROPOSED" };
        await Assert.ThrowsAsync<AssetValidationException>(() => f.Create()); Assert.Equal(0, f.Memory.Writes);
    }
    [Fact]
    public async Task Missing_or_mismatched_reference_stale_tree_and_exhausted_counter_do_not_write()
    {
        var f = await AssetFixture.Start();
        await Assert.ThrowsAsync<AssetValidationException>(() => f.Create(f.Input() with { LocationId = "LOC-B" }));
        await Assert.ThrowsAsync<AssetNotFoundException>(() => f.Create(f.Input() with { CategoryId = "missing" }));
        await Assert.ThrowsAsync<AssetConflictException>(() => f.Service.CreateAsync("ADMIN", f.Request() with { TreeVersionId = "old-tree" }, "stale"));
        f.Memory.State.Counters.Add(new("TEST-TST", 999999)); await Assert.ThrowsAsync<AssetConflictException>(() => f.Create()); Assert.Equal(0, f.Memory.Writes);
    }
    [Fact]
    public async Task Viewer_is_read_only_scoped_counts_filters_and_paging_never_include_other_branch()
    {
        var f = await AssetFixture.Start(); await f.Grant(AuthRole.Viewer, "OU-A", "LOC-A", "CAT-MACHINE", true);
        var first = (await f.Create()).Asset; await f.Create(f.Input() with { Name = "Động cơ giả", Serial = "0000123" });
        var hidden = (await f.Create(f.Input() with { Name = "Hidden fixture", OrgUnitId = "OU-B", LocationId = "LOC-B" })).Asset;
        var list = await f.Service.ListAsync("USER", new(PageSize: 1)); Assert.Equal(2, list.Total); Assert.Single(list.Items);
        Assert.Equal(1, (await f.Service.ListAsync("USER", new(Q: "dong co"))).Total); Assert.Equal(1, (await f.Service.ListAsync("USER", new(Q: "0000123"))).Total);
        Assert.Equal(2, (await f.Service.ListAsync("USER", new(CategoryId: "CAT-MACHINE"))).Total); Assert.Equal(0, (await f.Service.ListAsync("USER", new(CategoryId: "CAT-MACHINE", IncludeDescendants: false))).Total);
        Assert.Equal(0, (await f.Service.ListAsync("USER", new(Q: "Hidden"))).Total);
        await Assert.ThrowsAsync<AssetNotFoundException>(() => f.Service.DetailAsync("USER", hidden.AssetId));
        await Assert.ThrowsAsync<AssetForbiddenException>(() => f.Service.CreateAsync("USER", f.Request(), "viewer-create"));
        await Assert.ThrowsAsync<AssetForbiddenException>(() => f.Service.UpdateAsync("USER", first.AssetId, f.Request(version: 1), "viewer-edit"));
        await Assert.ThrowsAsync<AssetForbiddenException>(() => f.Service.DeleteAsync("USER", first.AssetId, new(1, "fake"), "viewer-delete"));
        await Assert.ThrowsAsync<AssetForbiddenException>(() => f.Service.RestoreAsync("USER", first.AssetId, new(1, "fake"), "viewer-restore"));
    }
    [Fact]
    public async Task Assignment_dimensions_cannot_cross_combine_and_move_needs_both_sides_in_one_assignment()
    {
        var f = await AssetFixture.Start(); await f.Grant(AuthRole.AssetEditor, "OU-A", "LOC-A", "CAT-MACHINE", true); await f.Grant(AuthRole.DocumentManager, "ALL", "ALL", "ALL", true);
        await Assert.ThrowsAsync<AssetNotFoundException>(() => f.Service.CreateAsync("USER", f.Request(f.Input() with { OrgUnitId = "OU-B", LocationId = "LOC-B" }), "cross-role"));
        var a = (await f.Create()).Asset;
        await f.Grant(AuthRole.AssetEditor, "OU-B", "LOC-B", "CAT-MACHINE", true);
        await Assert.ThrowsAsync<AssetNotFoundException>(() => f.Service.UpdateAsync("USER", a.AssetId, f.Request(f.Input() with { OrgUnitId = "OU-B", LocationId = "LOC-B" }, 1), "cross-move"));
        await f.Grant(AuthRole.AssetEditor, "ALL", "ALL", "ALL", true);
        var moved = await f.Service.UpdateAsync("USER", a.AssetId, f.Request(f.Input() with { OrgUnitId = "OU-B", LocationId = "LOC-B" }, 1), "valid-move"); Assert.Equal(a.ObjectCode, moved.Asset.ObjectCode);
    }
    [Fact]
    public async Task Missing_scope_is_quarantined_even_for_all_and_revoked_assignments_cannot_replay()
    {
        var f = await AssetFixture.Start(); await f.Grant(AuthRole.AssetEditor); var a = (await f.Service.CreateAsync("USER", f.Request(), "user-key")).Asset;
        await f.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(x => x.UserId == "USER"); return true; });
        await Assert.ThrowsAsync<AssetForbiddenException>(() => f.Service.CreateAsync("USER", f.Request(), "user-key"));
        f.Memory.State.Assets[0] = a with { OrgUnitId = "" }; Assert.Equal(0, (await f.Service.ListAsync("ADMIN", new())).Total);
        await Assert.ThrowsAsync<AssetNotFoundException>(() => f.Service.DetailAsync("ADMIN", a.AssetId));
    }
    [Fact]
    public async Task Actor_keys_are_isolated_and_reserved_future_alias_cannot_be_issued_again()
    {
        var f = await AssetFixture.Start(); await f.Grant(AuthRole.AssetEditor);
        var first = await f.Service.CreateAsync("ADMIN", f.Request(f.Input() with { Aliases = ["TEST-TST-000002"] }), "shared");
        var second = await f.Service.CreateAsync("USER", f.Request(), "shared");
        Assert.NotEqual(first.Asset.AssetId, second.Asset.AssetId); Assert.Equal("TEST-TST-000003", second.Asset.ObjectCode); Assert.Equal(3, f.Memory.State.Counters.Single().LastAllocated);
    }
    [Fact]
    public async Task Exact_scope_does_not_include_location_child_until_descendants_granted()
    {
        var f = await AssetFixture.Start(); var a = (await f.Create(f.Input() with { LocationId = "LOC-A-CHILD" })).Asset;
        await f.Grant(AuthRole.Viewer, "OU-A", "LOC-A", "CAT-CHILD", false);
        Assert.Equal(0, (await f.Service.ListAsync("USER", new())).Total);
        await Assert.ThrowsAsync<AssetNotFoundException>(() => f.Service.DetailAsync("USER", a.AssetId));
        await f.Grant(AuthRole.Viewer, "OU-A", "LOC-A", "CAT-CHILD", true);
        Assert.Equal(1, (await f.Service.ListAsync("USER", new())).Total);
    }
    [Fact]
    public async Task Invalid_query_and_lifecycle_state_are_explicit_and_failures_do_not_claim_save()
    {
        var f = await AssetFixture.Start(); await Assert.ThrowsAsync<AssetValidationException>(() => f.Service.ListAsync("ADMIN", new(PageSize: 101)));
        var a = (await f.Create()).Asset; await Assert.ThrowsAsync<AssetConflictException>(() => f.Service.RestoreAsync("ADMIN", a.AssetId, new(1, "fake"), "bad-restore"));
        f.Memory.FailWrite = true; await Assert.ThrowsAsync<ExternalStorageException>(() => f.Service.UpdateAsync("ADMIN", a.AssetId, f.Request(version: 1), "failed")); Assert.Equal(1, f.Memory.Writes); Assert.Equal(1, f.Memory.State.Assets.Single().Version);
    }
}
// Every reference/name/rule is synthetic. No company workbook, Google credential or live resource.
internal sealed class AssetFixture
{
    public ManualTimeProvider Clock { get; } = new(new(2026, 10, 13, 2, 0, 0, TimeSpan.Zero));
    public InMemoryAuthSystemStore Auth { get; }
    public CategoryMemoryStore Categories { get; } = new();
    public AssetCrudMemoryStore Memory { get; } = new(References());
    public InProcessSingleWriterCoordinator Writer { get; } = new();
    public AssetCrudService Service { get; private set; } = null!;
    private AssetFixture() { var auth = new AuthSystemState(); CategoryFixture.AddUser(auth, "ADMIN", AuthRole.Admin, Clock.GetUtcNow()); CategoryFixture.AddUser(auth, "USER", null, Clock.GetUtcNow()); Auth = new(auth); }
    public static AssetCatalogState References()
    {
        var s = new AssetCatalogState(); s.Rules.Add(new("RULE-FAKE", "CAT-MACHINE", "TEST", "TST", "DEMO", true, 1));
        s.OrgUnits.AddRange([new("OU-A", null, "Đơn vị A giả", true), new("OU-A-CHILD", "OU-A", "Đơn vị con giả", true), new("OU-B", null, "Đơn vị B giả", true)]);
        s.Locations.AddRange([new("LOC-A", null, "OU-A", "Địa điểm A giả", true), new("LOC-A-CHILD", "LOC-A", "OU-A", "Địa điểm con giả", true), new("LOC-B", null, "OU-B", "Địa điểm B giả", true)]); return s;
    }
    public static async Task<AssetFixture> Start(IAssetCatalogStore? store = null)
    {
        var f = new AssetFixture(); var tree = new CategoryCatalogState { Revision = 1, ActiveTreeVersionId = "TREE-FAKE" };
        tree.Categories.AddRange([Node("CAT-MACHINE", null, "TST", "Máy demo giả", 1), Node("CAT-CHILD", "CAT-MACHINE", "MCN", "Nhánh máy giả", 2), Node("CAT-OTHER", null, "ZZZ", "Nhánh khác giả", 1)]);
        await f.Categories.CommitAsync(tree, 0); f.Service = f.NewService(store ?? f.Memory); return f;
    }
    private static CategoryNode Node(string id, string? parent, string code, string name, int level) => new(id, "TREE-FAKE", parent, level, 1, name, code, code, "FIXTURE_GIA", "DRAFT", 1, id, "FIXTURE_GIA", 1, id);
    public AssetCrudService NewService(IAssetCatalogStore s) => new(s, Categories, Auth, Writer, Clock);
    public AssetInput Input() => new("Máy ép giả", "CAT-CHILD", "OU-A", "LOC-A", Serial: "00001", Aliases: []);
    public AssetWriteRequest Request(AssetInput? input = null, long version = 0) => new(input ?? Input(), version, Categories.State.ActiveTreeVersionId!);
    public Task<AssetWriteResult> Create(AssetInput? input = null) => Service.CreateAsync("ADMIN", Request(input), Guid.NewGuid().ToString("N"));
    public Task<bool> Grant(string role, string org = "ALL", string loc = "ALL", string category = "ALL", bool descendants = true) => Auth.MutateAsync(s =>
    {
        var key = Guid.NewGuid().ToString("N"); var now = Clock.GetUtcNow();
        s.RoleAssignments.Add(new(key, "USER", role, null, "FIXTURE_GIA", RoleAssignmentState.Active, now.AddMinutes(-1), null, now, "ADMIN", 1));
        foreach (var (type, scope) in new[] { (AuthScopeType.OrgUnit, org), (AuthScopeType.Location, loc), (AuthScopeType.CategorySubtree, category) }) s.RoleAssignmentScopes.Add(new(Guid.NewGuid().ToString("N"), key, type, scope, descendants, 1)); return true;
    });
}
internal sealed class AssetCrudMemoryStore(AssetCatalogState initial) : IAssetCatalogStore
{
    public AssetCatalogState State { get; private set; } = initial.Clone();
    public int Writes { get; private set; }
    public bool FailRead { get; set; }
    public bool FailWrite { get; set; }
    public Task<AssetCatalogState> ReadAsync(CancellationToken ct = default) => FailRead ? throw new ExternalStorageException("PRIVATE-ASSET-CANARY") : Task.FromResult(State.Clone());
    public Task CommitAsync(AssetCatalogState candidate, long expectedRevision, CancellationToken ct = default)
    { if (FailWrite) throw new ExternalStorageException("PRIVATE-ASSET-CANARY"); if (State.Revision != expectedRevision) throw new AssetConflictException("Fake conflict"); State = candidate.Clone(); Writes++; return Task.CompletedTask; }
}
