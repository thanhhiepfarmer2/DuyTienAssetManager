using System.Net.Http;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;

// Complete production Google adapters over synthetic HTTP tables, no Google resource access.
public sealed class TreeRegistryTests
{
    [Fact]
    public async Task Relocate_reconciles_all_owners_one_atomic_batch_preserving_ids_codes_stock_bytes_and_versions()
    {
        var r = await TreeRegistryRig.Start(); var before = await r.Joint().ReadAsync(); var request = r.Archive();
        var preview = await r.Tree.PreviewAsync("ADMIN", request); Assert.True(preview.CanCommit); Assert.Contains(preview.Registry!, i => i.Kind == "STOCK"); Assert.Contains(preview.Registry!, i => i.Kind == "PDF");
        Assert.DoesNotContain("driveFileId", JsonSerializer.Serialize(preview, new JsonSerializerOptions(JsonSerializerDefaults.Web)), StringComparison.OrdinalIgnoreCase);
        var posts = r.F.Handler.Posts; var result = await r.Commit(request, preview, "registry"); Assert.Equal(posts + 1, r.F.Handler.Posts); Assert.Equal(1, result.ChangedItemCount); Assert.Equal(1, result.ChangedDocumentCount);
        var after = await r.Joint().ReadAsync(); Assert.Equal(r.F.Other, after.Inventory!.Items.Single().CategoryId); Assert.Equal(r.F.Other, after.Media!.Documents.Single().CategoryId);
        Assert.Equal(before.Inventory!.Items.Single().Cells, after.Inventory.Items.Single().Cells); Assert.Equal(before.Inventory.Items.Single().ItemId, after.Inventory.Items.Single().ItemId); Assert.Equal(2, after.Inventory.Items.Single().Version);
        Assert.Equal(before.Inventory.Balances, after.Inventory.Balances); Assert.Null(after.Inventory.Balances.Single().Quantity); Assert.Equal("UNKNOWN", after.Inventory.Balances.Single().StockStatus);
        Assert.Equal(before.Media!.Files, after.Media.Files); Assert.Equal(before.Media.Versions, after.Media.Versions); Assert.Equal(before.Assets.Assets.Single().ObjectCode, after.Assets.Assets.Single().ObjectCode); Assert.Equal(before.Assets.Counters, after.Assets.Counters);
        Assert.Contains("TreeRegistryDetails", r.F.Handler.LastBody); Assert.Contains("InventoryOperations", r.F.Handler.Tables.Keys); Assert.Contains("MediaOperations", r.F.Handler.Tables.Keys);
        Assert.Equal("INVENTORY_V1_TREE", r.F.Handler.Tables["InventorySchemaVersions"].Rows[1][0]); Assert.Equal("MEDIA_V1_TREE", r.F.Handler.Tables["MediaSchemaVersions"].Rows[1][0]);
        Assert.True((await r.NewTree().CommitAsync("ADMIN", request, preview.TreeRevision, preview.AssetRevision, preview.PreviewHash, "registry", true)).Replayed); Assert.Equal(posts + 1, r.F.Handler.Posts);
    }
    [Fact]
    public async Task Media_delta_preserves_reordered_rows_and_unrelated_document_metadata()
    {
        var r = await TreeRegistryRig.Start();
        var other = await r.MediaService.CreateDocumentAsync("ADMIN", new(new("Hồ sơ khác GIẢ", "FAKE.KEEP", r.F.Other, "Giữ metadata khác"), 0), "keep-other");
        var rows = r.F.Handler.Tables["Documents"].Rows; rows.Reverse(1, rows.Count - 1);
        var req = r.Archive(); var p = await r.Tree.PreviewAsync("ADMIN", req); await r.Commit(req, p);
        var after = await r.Media.ReadAsync(); Assert.Equal(2, after.Documents.Count); var unchanged = after.Documents.Single(d => d.DocumentId == other.EntityId);
        Assert.Equal("Giữ metadata khác", unchanged.Description); Assert.Equal(1, unchanged.Version);
        Assert.Equal(2, rows.Count - 1); Assert.Equal(2, r.F.Handler.Tables["Documents"].Rows.Count - 1);
    }
    [Fact]
    public async Task Hierarchy_move_rechecks_registry_scope_but_keeps_item_document_stock_file_versions()
    {
        var r = await TreeRegistryRig.Start(); var before = await r.Joint().ReadAsync(); var request = new TreeMutationRequest("MOVE_CATEGORY", "GIẢ", r.F.Root, r.F.Other);
        var p = await r.Tree.PreviewAsync("ADMIN", request); await r.Commit(request, p); var after = await r.Joint().ReadAsync();
        Assert.Equal(before.Inventory!.Revision, after.Inventory!.Revision); Assert.Equal(before.Media!.Revision, after.Media!.Revision); Assert.Equal(before.Media.Files, after.Media.Files); Assert.Equal(JsonSerializer.Serialize(before.Inventory.Items), JsonSerializer.Serialize(after.Inventory.Items));
        Assert.Equal(before.Assets.Assets, after.Assets.Assets); Assert.NotEqual(before.Categories.ActiveTreeVersionId, after.Categories.ActiveTreeVersionId);
    }
    [Theory]
    [InlineData("EMPTY_ONLY")]
    [InlineData("SOFT_DELETE")]
    public async Task Item_archive_without_relocate_is_explicitly_blocked_not_deleted_or_assumed_empty(string mode)
    {
        var r = await TreeRegistryRig.Start(); var request = new TreeMutationRequest("ARCHIVE_CATEGORY", "GIẢ", r.F.Root, DeletePlan: mode); var p = await r.Tree.PreviewAsync("ADMIN", request);
        Assert.False(p.CanCommit); Assert.NotEmpty(p.Registry!); var posts = r.F.Handler.Posts;
        await Assert.ThrowsAsync<CategoryValidationException>(() => r.Commit(request, p)); Assert.Equal(posts, r.F.Handler.Posts); Assert.Equal("DRAFT_DEMO", (await r.Inventory.ReadAsync()).Items.Single().State);
    }
    [Fact]
    public async Task Document_soft_archive_without_item_keeps_published_content_and_file_registry()
    {
        var r = await TreeRegistryRig.Start(withItems: false); var before = await r.Media.ReadAsync(); var request = new TreeMutationRequest("ARCHIVE_CATEGORY", "GIẢ", r.F.Root, DeletePlan: "SOFT_DELETE");
        var p = await r.Tree.PreviewAsync("ADMIN", request); Assert.True(p.CanCommit); await r.Commit(request, p); var after = await r.Media.ReadAsync();
        Assert.True(after.Documents.Single().IsDeleted); Assert.Equal(before.Files, after.Files); Assert.Equal(before.Versions, after.Versions); Assert.Equal(1, r.Content.Creates > 0 ? after.Documents.Count : 0);
    }
    [Fact]
    public async Task Registry_changed_after_preview_invalidates_digest_even_if_tree_asset_revisions_are_same()
    {
        var r = await TreeRegistryRig.Start(); var request = r.Archive(); var p = await r.Tree.PreviewAsync("ADMIN", request); var item = (await r.Inventory.ReadAsync()).Items.Single();
        var b = (await r.Inventory.ReadAsync()).Balances.Single(); await r.InventoryService.WriteStockAsync("ADMIN", new(item.ItemId, "LOC-A", item.PrimaryUnit, 2, null, r.F.AuthFixture.Clock.GetUtcNow(), "GIẢ", b.Version, "GIẢ đổi"), "stock-new");
        var posts = r.F.Handler.Posts; await Assert.ThrowsAsync<CategoryConflictException>(() => r.Commit(request, p)); Assert.Equal(posts, r.F.Handler.Posts);
    }
    [Fact]
    public async Task Lost_atomic_response_reconciles_all_four_registries_once()
    {
        var r = await TreeRegistryRig.Start(); var request = r.Archive(); var p = await r.Tree.PreviewAsync("ADMIN", request); var posts = r.F.Handler.Posts; r.F.Handler.LoseResponseOnce = true;
        await r.Commit(request, p); Assert.Equal(posts + 1, r.F.Handler.Posts); var s = await r.Joint().ReadAsync(); Assert.Equal(r.F.Other, s.Inventory!.Items.Single().CategoryId); Assert.Equal(r.F.Other, s.Media!.Documents.Single().CategoryId);
    }
    [Fact]
    public async Task Pending_import_blocks_tree_version_but_not_asset_only_move()
    {
        var r = await TreeRegistryRig.Start(withItems: false); var request = await r.ItemRequest(); var p = await r.InventoryService.PreviewAsync("ADMIN", request);
        await r.InventoryService.StartAsync("ADMIN", request, p.Revision, p.PreviewHash, "pending", true);
        var edit = new TreeMutationRequest("EDIT_CATEGORY", "GIẢ", r.F.Root, Name: "Đổi tên GIẢ", SegmentCode: "ROT"); var blocked = await r.Tree.PreviewAsync("ADMIN", edit);
        Assert.False(blocked.CanCommit); Assert.Contains(blocked.Issues, x => x.Contains("import Items", StringComparison.Ordinal));
        var move = new TreeMutationRequest("MOVE_ASSETS", "GIẢ", DestinationCategoryId: r.F.Other, AssetIds: [r.Asset.AssetId]); Assert.True((await r.Tree.PreviewAsync("ADMIN", move)).CanCommit);
    }
    [Theory]
    [InlineData("partial")]
    [InlineData("unknown")]
    [InlineData("itemHeader")]
    [InlineData("mediaAudit")]
    [InlineData("receipt")]
    [InlineData("owner")]
    public async Task Unsupported_partial_tampered_registry_or_reference_never_makes_a_write(string fault)
    {
        var r = await TreeRegistryRig.Start();
        if (fault == "partial") r.F.Handler.Tables.Remove("ItemImportPlan");
        else if (fault == "unknown") { var t = new GoogleCategoryCatalogStoreTests.Table(8000, 1); t.Rows.Add(["ID"]); r.F.Handler.Tables["UnreconciledObjects"] = t; }
        else if (fault == "itemHeader") r.F.Handler.Tables["Items"].Rows[0][0] = "unknown";
        else if (fault == "mediaAudit") r.F.Handler.Tables["MediaAuditEvents"].Rows[1][2] = "unknown";
        else if (fault == "owner") { r.F.Handler.Tables["Assets"].Rows[1][0] = "ORPHAN"; }
        else { var req = r.Archive(); var p = await r.Tree.PreviewAsync("ADMIN", req); await r.Commit(req, p); r.F.Handler.Tables["TreeRegistryDetails"].Rows.RemoveAt(1); }
        var posts = r.F.Handler.Posts; await Assert.ThrowsAsync<ExternalStorageException>(() => r.Tree.PreviewAsync("ADMIN", r.Archive())); Assert.Equal(posts, r.F.Handler.Posts);
    }
    [Fact]
    public async Task Missing_module_permission_refuses_whole_preview_before_hidden_registry_names_or_counts()
    {
        var r = await TreeRegistryRig.Start(); await r.Grant(AuthRole.CatalogManager, "ALL", true); await r.Grant(AuthRole.AssetEditor, "ALL", true);
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => r.Tree.PreviewAsync("USER", r.Archive()));
        await r.Grant(AuthRole.DocumentManager, "ALL", true); Assert.True((await r.Tree.PreviewAsync("USER", r.Archive())).CanCommit);
    }
    [Fact]
    public async Task Split_source_destination_document_assignments_do_not_expand_move_authority()
    {
        var r = await TreeRegistryRig.Start(); await r.Grant(AuthRole.CatalogManager, "ALL", true); await r.Grant(AuthRole.AssetEditor, "ALL", true);
        await r.Grant(AuthRole.DocumentManager, r.F.Root, true); await r.Grant(AuthRole.DocumentManager, r.F.Other, true);
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => r.Tree.PreviewAsync("USER", r.Archive()));
    }
    [Fact]
    public async Task Replay_rechecks_registry_permission_after_assignment_revocation()
    {
        var r = await TreeRegistryRig.Start(); foreach (var role in new[] { AuthRole.CatalogManager, AuthRole.AssetEditor, AuthRole.DocumentManager }) await r.Grant(role, "ALL", true);
        var req = r.Archive(); var p = await r.Tree.PreviewAsync("USER", req); await r.Tree.CommitAsync("USER", req, p.TreeRevision, p.AssetRevision, p.PreviewHash, "user-tree", true);
        await r.F.AuthFixture.Auth.MutateAsync(s => { s.RoleAssignments.RemoveAll(a => a.UserId == "USER" && a.RoleCode == AuthRole.DocumentManager); return true; });
        await Assert.ThrowsAsync<CategoryNotFoundException>(() => r.NewTree().CommitAsync("USER", req, p.TreeRevision, p.AssetRevision, p.PreviewHash, "user-tree", true));
    }
}
internal sealed class TreeRegistryRig
{
    public GoogleTreeMutationTests.Fixture F { get; }
    private readonly HttpClient http;
    public GoogleApiRequestExecutor Api { get; }
    public GoogleCategoryCatalogStore Categories => new(F.Options, Api);
    public GoogleAssetCatalogStore Assets => new(F.Options, Api);
    public GoogleInventoryStore Inventory => new(F.Options, Api);
    public GoogleMediaCatalogStore Media => new(F.Options, Api);
    public ManagedMediaMemoryContent Content { get; } = new();
    public AssetRecord Asset { get; private set; } = null!;
    public string DocumentId { get; private set; } = "";
    private TreeRegistryRig(GoogleTreeMutationTests.Fixture f) { F = f; http = new(f.Handler); Api = new(f.Options, new SyntheticToken(), http); }
    public GoogleTreeMutationStore Joint() => new(F.Options, Api, Categories, Assets, Inventory, Media);
    public TreeMutationService Tree => NewTree();
    public TreeMutationService NewTree() => new(Joint(), F.AuthFixture.Auth, F.Writer, F.AuthFixture.Clock);
    public InventoryService InventoryService => new(Inventory, Categories, Assets, F.AuthFixture.Auth, F.Writer, F.AuthFixture.Clock);
    public MediaLibraryService MediaService => new(Media, Content, Assets, Categories, F.AuthFixture.Auth, F.Writer, F.AuthFixture.Clock, new());
    public AssetCrudService AssetService => new(Assets, Categories, F.AuthFixture.Auth, F.Writer, F.AuthFixture.Clock);
    public CategoryCatalogService Catalog => new(Categories, F.AuthFixture.Auth, F.Writer, F.AuthFixture.Clock, Assets, Inventory, Joint());
    public async Task Grant(string role, string category, bool descendants)
    {
        await F.AuthFixture.Grant("USER", role, category, descendants);
        await F.AuthFixture.Auth.MutateAsync(s =>
        {
            var assignment = s.RoleAssignments.Last(a => a.UserId == "USER" && a.RoleCode == role);
            foreach (var type in new[] { AuthScopeType.OrgUnit, AuthScopeType.Location }) s.RoleAssignmentScopes.Add(new(Guid.NewGuid().ToString("N"), assignment.AssignmentId, type, "ALL", true, 1));
            return true;
        });
    }
    public TreeMutationRequest Archive() => new("ARCHIVE_CATEGORY", "GIẢ TASK014", F.Root, F.Other, DeletePlan: "RELOCATE");
    public Task<TreeMutationResult> Commit(TreeMutationRequest req, TreeMutationPreview p, string key = "joint-fake") => Tree.CommitAsync("ADMIN", req, p.TreeRevision, p.AssetRevision, p.PreviewHash, key, true);
    public async Task<ItemImportRequest> ItemRequest()
    { var f = await InventoryFixture.Start(); var request = f.Request(); return request with { Groups = request.Groups.Select(g => g with { CategoryId = F.Child }).ToArray() }; }
    public static async Task<TreeRegistryRig> Start(bool withItems = true)
    {
        var f = await GoogleTreeMutationTests.Fixture.Start(); var r = new TreeRegistryRig(f); r.Asset = await f.Create();
        if (withItems)
        {
            var req = await r.ItemRequest(); var p = await r.InventoryService.PreviewAsync("ADMIN", req); var job = await r.InventoryService.StartAsync("ADMIN", req, p.Revision, p.PreviewHash, "items", true);
            await r.InventoryService.CommitChunkAsync("ADMIN", job.JobId, 0, "chunk"); var item = (await r.Inventory.ReadAsync()).Items.Single();
            await r.InventoryService.WriteStockAsync("ADMIN", new(item.ItemId, "LOC-A", item.PrimaryUnit, null, null, f.AuthFixture.Clock.GetUtcNow(), "GIẢ", 0, "GIẢ snapshot"), "stock");
        }
        var doc = await r.MediaService.CreateDocumentAsync("ADMIN", new(new("Hồ sơ GIẢ TASK014", "FAKE.WEB", f.Child), 0), "document"); r.DocumentId = doc.EntityId;
        var version = await r.MediaService.CreateVersionAsync("ADMIN", doc.EntityId, new("v1 GIẢ", 1), "version");
        await r.MediaService.UploadDocumentFileAsync("ADMIN", version.EntityId, "DOCX", MediaBytes.Docx(), 1, "docx");
        await r.MediaService.UploadDocumentFileAsync("ADMIN", version.EntityId, "PDF", MediaBytes.Pdf(), 2, "pdf");
        await r.MediaService.PublishVersionAsync("ADMIN", version.EntityId, new(3, 2, DateOnly.FromDateTime(f.AuthFixture.Clock.GetUtcNow().UtcDateTime), "GIẢ", true), "publish");
        await r.MediaService.UploadPhotoAsync("ADMIN", r.Asset.AssetId, MediaBytes.Png(), r.Asset.Version, "GIẢ", true, "photo"); return r;
    }
    private sealed class SyntheticToken : IGoogleAccessTokenProvider
    { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fixture-token"); public void Invalidate() { } }
}
