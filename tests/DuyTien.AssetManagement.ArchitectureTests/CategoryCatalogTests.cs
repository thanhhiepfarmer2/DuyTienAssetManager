using System.Text;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class CategoryCatalogTests
{
    [Fact]
    public async Task Full_export_preserves_Unicode_indent_and_root_to_leaf_path()
    {
        var f = new CategoryFixture(); await f.Import();
        var text = Encoding.UTF8.GetString(await f.Service.ExportAsync("ADMIN"));
        Assert.Contains("\n1 | Nhánh bí mật | ROT", text, StringComparison.Ordinal);
        Assert.Contains("\n  1.1 | Nhánh con | CHD | ROT.CHD", text, StringComparison.Ordinal);
        Assert.Contains("\n    1.1.1 | Lá có dấu | LEF | ROT.CHD.LEF | Nhánh bí mật / Nhánh con / Lá có dấu | DRAFT", text, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Repository_staging_matches_baseline_and_reimport_is_idempotent_even_concurrently()
    {
        var f = new CategoryFixture(); var doc = CategoryImportReader.Read(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "category-candidates.json")), "baseline.json");
        var preview = await f.Service.PreviewAsync("ADMIN", doc);
        Assert.True(preview.CanCommit); Assert.Equal((665, 15, 98, 552, 39), (preview.Total, preview.F1, preview.F2, preview.F3, preview.DuplicateNames.Count));
        Assert.Contains(preview.Warnings, w => w.Contains("workbook gốc", StringComparison.Ordinal));
        await Assert.ThrowsAsync<CategoryValidationException>(() => f.Service.ImportAsync("ADMIN", doc, 0, preview.PreviewHash, "baseline", false));
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => f.Service.ImportAsync("ADMIN", doc, 0, preview.PreviewHash, "baseline", true)));
        Assert.Single(results, r => !r.AlreadyImported); Assert.Equal(1, f.Store.Writes);
        Assert.Equal(665, f.Store.State.ActiveNodes.Length); Assert.All(f.Store.State.Categories, n => Assert.Equal("DRAFT", n.State));
        var repeated = await f.Service.PreviewAsync("ADMIN", doc); Assert.Empty(repeated.Diff);
        var noCopy = await f.Service.ImportAsync("ADMIN", doc, 1, repeated.PreviewHash, "another-key", true); Assert.True(noCopy.AlreadyImported); Assert.Equal(2, f.Store.Writes);
        var changed = doc with { SourceHash = "different", Rows = doc.Rows.Select((r, i) => i == 0 ? r with { Name = r.Name + "x" } : r).ToArray() };
        var changedPreview = await f.Service.PreviewAsync("ADMIN", changed);
        await Assert.ThrowsAsync<CategoryConflictException>(() => f.Service.ImportAsync("ADMIN", changed, changedPreview.Revision, changedPreview.PreviewHash, "another-key", true));
    }
    [Fact]
    public async Task New_version_keeps_ids_when_stt_or_name_changes_and_retains_old_snapshot()
    {
        var f = new CategoryFixture(); await f.Import(); var ids = f.Store.State.ActiveNodes.ToDictionary(n => n.SegmentCode, n => n.CategoryId);
        var rows = CategoryFixture.Rows.Select(r => r with { SourceStt = "2" + r.SourceStt[1..], ParentSourceStt = r.ParentSourceStt is null ? null : "2" + r.ParentSourceStt[1..], Name = r.Name + " mới" }).ToArray();
        var doc = CategoryFixture.Doc(rows, "version2"); var preview = await f.Service.PreviewAsync("ADMIN", doc);
        Assert.All(preview.Diff, d => Assert.Equal("CHANGE", d.Kind)); await f.Service.ImportAsync("ADMIN", doc, 1, preview.PreviewHash, "v2", true);
        Assert.Equal(2, f.Store.State.Versions.Count); Assert.Equal(6, f.Store.State.Categories.Count);
        Assert.All(f.Store.State.ActiveNodes, n => { Assert.Equal(ids[n.SegmentCode], n.CategoryId); Assert.Equal(2, n.Version); });
        Assert.Contains(f.Store.State.Categories, n => n.Name == "Nhánh bí mật");
    }
    [Fact]
    public async Task Stale_or_tampered_preview_and_key_reuse_do_not_write()
    {
        var f = new CategoryFixture(); var doc = CategoryFixture.Doc(); var p = await f.Service.PreviewAsync("ADMIN", doc);
        await Assert.ThrowsAsync<CategoryConflictException>(() => f.Service.ImportAsync("ADMIN", doc, 0, "tampered", "a", true));
        await f.Service.ImportAsync("ADMIN", doc, 0, p.PreviewHash, "a", true);
        var changed = CategoryFixture.Doc(CategoryFixture.Rows.Select(r => r with { Name = r.Name + "x" }).ToArray(), "changed");
        await Assert.ThrowsAsync<CategoryConflictException>(() => f.Service.ImportAsync("ADMIN", changed, 0, p.PreviewHash, "b", true));
        await Assert.ThrowsAsync<CategoryConflictException>(() => f.Service.ImportAsync("ADMIN", changed, 1, p.PreviewHash, "a", true));
        Assert.Equal(1, f.Store.Writes);
    }
    [Fact]
    public async Task Code_rename_uses_explicit_id_and_never_matches_by_name()
    {
        var f = new CategoryFixture(); await f.Import(); var id = f.Store.State.ActiveNodes.Single(n => n.SegmentCode == "CHD").CategoryId;
        var doc = CategoryFixture.Doc(CategoryFixture.Rows.Select(r => r.SegmentCode == "CHD" ? r with { SegmentCode = "NEW", CategoryId = id } : r).ToArray(), "rename");
        var p = await f.Service.PreviewAsync("ADMIN", doc); await f.Service.ImportAsync("ADMIN", doc, 1, p.PreviewHash, "rename", true);
        Assert.Equal(id, f.Store.State.ActiveNodes.Single(n => n.SegmentCode == "NEW").CategoryId);
        Assert.Contains(f.Store.State.Categories, n => n.CategoryId == id && n.SegmentCode == "CHD");
        var withoutId = CategoryFixture.Doc(doc.Rows.Select(r => r with { CategoryId = null, SegmentCode = r.SegmentCode == "NEW" ? "XYZ" : r.SegmentCode }).ToArray(), "unmapped");
        var changes = (await f.Service.PreviewAsync("ADMIN", withoutId)).Diff; Assert.Contains(changes, d => d.Kind == "ADD"); Assert.Contains(changes, d => d.Kind == "REMOVE_FROM_VERSION");
    }
    [Theory]
    [InlineData("CODE")]
    [InlineData("MISSING_PARENT")]
    [InlineData("LEVEL")]
    [InlineData("CYCLE")]
    [InlineData("DUPLICATE_STT")]
    [InlineData("DUPLICATE_CODE")]
    [InlineData("NAME")]
    public void Validation_reports_code_parent_level_cycle_and_unique_errors(string kind)
    {
        var rows = CategoryFixture.Rows.ToArray();
        rows[1] = kind switch
        {
            "CODE" => rows[1] with { SegmentCode = "=X" },
            "MISSING_PARENT" => rows[1] with { SourceStt = "9.1", ParentSourceStt = "9" },
            "LEVEL" => rows[1] with { Level = 4 },
            "CYCLE" => rows[1] with { ParentSourceStt = "1.1.1" },
            "DUPLICATE_STT" => rows[1] with { SourceStt = "1" },
            "DUPLICATE_CODE" => rows[1] with { SegmentCode = "ROT" },
            _ => rows[1] with { Name = "name\ninjection" }
        };
        Assert.Contains(CategoryCatalogService.Validate(rows), e => e.Code == kind);
    }
    [Fact]
    public async Task Filtering_precedes_count_search_detail_and_export_and_hides_ancestors()
    {
        var f = new CategoryFixture(); await f.Import(); var child = f.Store.State.ActiveNodes.Single(n => n.SegmentCode == "CHD").CategoryId;
        await f.Grant("USER", AuthRole.CatalogManager, child, true);
        var list = await f.Service.ListAsync("USER"); Assert.Equal(2, list.Total); Assert.All(list.Nodes, n => Assert.DoesNotContain("ROT", n.FullCode, StringComparison.Ordinal));
        Assert.Equal("Nhánh con", list.Nodes[0].Path); Assert.Null(list.Nodes[0].ParentId); Assert.True(list.Nodes[0].PathIsPartial);
        Assert.Equal(0, (await f.Service.ListAsync("USER", "bí mật")).Total);
        Assert.Equal(1, (await f.Service.ListAsync("USER", "La co dau")).Total);
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.DetailAsync("USER", f.Store.State.ActiveNodes.Single(n => n.SegmentCode == "ROT").CategoryId));
        var txt = Encoding.UTF8.GetString(await f.Service.ExportAsync("USER")); Assert.DoesNotContain("Nhánh bí mật", txt, StringComparison.Ordinal); Assert.DoesNotContain("ROT.CHD", txt, StringComparison.Ordinal);
        Assert.Contains("  [nhánh được phép] 1.1.1 | Lá có dấu", txt, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData(AuthRole.Viewer)]
    [InlineData(AuthRole.AssetEditor)]
    public async Task Reader_roles_cannot_import_or_export(string role)
    {
        var f = new CategoryFixture(); await f.Import(); await f.Grant("USER", role, "ALL", true);
        Assert.Equal(3, (await f.Service.ListAsync("USER")).Total);
        await Assert.ThrowsAsync<CategoryForbiddenException>(() => f.Service.PreviewAsync("USER", CategoryFixture.Doc()));
        await Assert.ThrowsAsync<CategoryForbiddenException>(() => f.Service.ExportAsync("USER"));
    }
    [Fact]
    public async Task Broad_viewer_does_not_expand_catalog_export_and_revoke_is_immediate()
    {
        var f = new CategoryFixture(); await f.Import(); var child = f.Store.State.ActiveNodes.Single(n => n.SegmentCode == "CHD").CategoryId;
        await f.Grant("USER", AuthRole.Viewer, "ALL", true); await f.Grant("USER", AuthRole.CatalogManager, child, false);
        Assert.Equal(3, (await f.Service.ListAsync("USER")).Total);
        var text = Encoding.UTF8.GetString(await f.Service.ExportAsync("USER")); Assert.DoesNotContain("Lá có dấu", text, StringComparison.Ordinal); Assert.DoesNotContain("Nhánh bí mật", text, StringComparison.Ordinal);
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.PreviewAsync("USER", CategoryFixture.Doc()));
        await f.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER"); return true; });
        await Assert.ThrowsAsync<CategoryForbiddenException>(() => f.Service.ListAsync("USER"));
    }
    [Fact]
    public async Task Move_does_not_combine_source_and_destination_from_different_assignments()
    {
        var rows = new[] { CategoryFixture.Rows[0], CategoryFixture.Rows[1], new CategoryInput("F1-F3 Mã ngang", 4, "1.2", "1", 2, "Nhánh khác", "OTH") };
        var f = new CategoryFixture(); await f.Import(CategoryFixture.Doc(rows));
        foreach (var node in f.Store.State.ActiveNodes) await f.Grant("USER", AuthRole.CatalogManager, node.CategoryId, false);
        var changed = rows.Select(r => r.SegmentCode == "CHD" ? r with { SourceStt = "1.2.1", ParentSourceStt = "1.2", Level = 3 } : r).ToArray();
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => f.Service.PreviewAsync("USER", CategoryFixture.Doc(changed, "move")));
    }
    [Fact]
    public async Task Failed_commit_keeps_old_version_and_can_be_retried_after_restart()
    {
        var f = new CategoryFixture(); f.Store.FailWrite = true; var doc = CategoryFixture.Doc(); var p = await f.Service.PreviewAsync("ADMIN", doc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ImportAsync("ADMIN", doc, 0, p.PreviewHash, "retry", true)); Assert.Empty(f.Store.State.Categories);
        f.Store.FailWrite = false; var restarted = new CategoryCatalogService(f.Store, f.Auth, new InProcessSingleWriterCoordinator(), f.Clock);
        await restarted.ImportAsync("ADMIN", doc, 0, p.PreviewHash, "retry", true); Assert.Equal(3, (await restarted.ListAsync("ADMIN")).Total);
    }
}
internal sealed class CategoryMemoryStore : ICategoryCatalogStore
{
    public CategoryCatalogState State { get; private set; } = new();
    public bool FailRead { get; set; }
    public bool FailWrite { get; set; }
    public int Writes { get; private set; }
    public Task<CategoryCatalogState> ReadAsync(CancellationToken ct = default) => FailRead ? throw new DuyTien.AssetManagement.Application.ExternalStorage.ExternalStorageException("PRIVATE-CATEGORY-FAULT-CANARY") : Task.FromResult(State.Clone());
    public Task CommitAsync(CategoryCatalogState state, long expected, CancellationToken ct = default)
    { if (FailWrite) throw new InvalidOperationException("Synthetic pre-commit failure"); if (State.Revision != expected) throw new CategoryConflictException("Conflict"); State = state.Clone(); Writes++; return Task.CompletedTask; }
}
internal sealed class CategoryFixture
{
    public static readonly CategoryInput[] Rows = [new("F1-F3 Mã ngang", 2, "1", null, 1, "Nhánh bí mật", "ROT"), new("F1-F3 Mã ngang", 3, "1.1", "1", 2, "Nhánh con", "CHD"), new("F1-F3 Mã ngang", 4, "1.1.1", "1.1", 3, "Lá có dấu", "LEF", "Định nghĩa giả; Liệt kê chỉ là ghi chú")];
    public static CategoryDocument Doc(IReadOnlyList<CategoryInput>? rows = null, string hash = "fixture") => new(hash, CategoryImportReader.MappingVersion, rows ?? Rows, []);
    public CategoryMemoryStore Store { get; } = new(); public InMemoryAuthSystemStore Auth { get; }
    public ManualTimeProvider Clock { get; } = new(new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));
    public CategoryCatalogService Service { get; }
    public CategoryFixture(ICategoryCatalogStore? store = null)
    {
        var state = new AuthSystemState(); AddUser(state, "ADMIN", AuthRole.Admin, Clock.GetUtcNow()); AddUser(state, "USER", null, Clock.GetUtcNow());
        Auth = new(state); Service = new(store ?? Store, Auth, new InProcessSingleWriterCoordinator(), Clock);
    }
    public async Task Import(CategoryDocument? doc = null)
    { doc ??= Doc(); var p = await Service.PreviewAsync("ADMIN", doc); await Service.ImportAsync("ADMIN", doc, p.Revision, p.PreviewHash, Guid.NewGuid().ToString("N"), true); }
    public Task<bool> Grant(string user, string role, string scope, bool descendants) => Auth.MutateAsync(s => { AddAssignment(s, user, role, scope, descendants, Clock.GetUtcNow()); return true; });
    public static void AddUser(AuthSystemState state, string id, string? role, DateTimeOffset now)
    {
        state.Users.Add(new(id, id.ToLowerInvariant(), id, id, new TestPasswordHashService().Hash(id, "fake-password"), AuthUserState.Active, false, 0, null, null, 1, now, now, "test", now, "test", 1));
        if (role is not null)
        {
            AddAssignment(state, id, role, "ALL", true, now);
            if (role == AuthRole.Admin)
            {
                var assignment = state.RoleAssignments.Last();
                foreach (var type in new[] { AuthScopeType.OrgUnit, AuthScopeType.Location }) state.RoleAssignmentScopes.Add(new(Guid.NewGuid().ToString("N"), assignment.AssignmentId, type, "ALL", true, 1));
            }
        }
    }
    private static void AddAssignment(AuthSystemState state, string id, string role, string scope, bool desc, DateTimeOffset now)
    { var key = Guid.NewGuid().ToString("N"); state.RoleAssignments.Add(new(key, id, role, null, "TEST", RoleAssignmentState.Active, now.AddMinutes(-1), null, now, "test", 1)); state.RoleAssignmentScopes.Add(new(Guid.NewGuid().ToString("N"), key, AuthScopeType.CategorySubtree, scope, desc, 1)); }
}
