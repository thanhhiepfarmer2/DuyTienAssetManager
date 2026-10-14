using System.Security.Cryptography;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Local;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Server.Authentication;
namespace DuyTien.AssetManagement.ArchitectureTests;

[Collection("LocalOffline")]
public sealed class LocalOfflineTests
{
    [Fact]
    public async Task Auth_is_durable_and_workspace_identity_is_stable_after_restart()
    {
        var root = Path.Combine(Path.GetTempPath(), "DuyTienOfflineTest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string id; using (var w = new LocalWorkspace(root)) { id = w.WorkspaceId; await new AuthApplicationService(w, new FrameworkPasswordHashService(), new(), TimeProvider.System).BootstrapAdminAsync("fixture-only-password", "FAKE"); }
            using (var w = new LocalWorkspace(root)) { Assert.Equal(id, w.WorkspaceId); Assert.Single(await ((IAuthSystemStore)w).ReadAsync(s => s.Users.ToArray())); }
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Process_lease_rejects_second_writer_and_corrupt_workspace_does_not_reset()
    {
        using var f = await LocalOfflineFixture.Start(); Assert.Throws<IOException>(() => new LocalWorkspace(f.Root)); f.W.Dispose();
        var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Root, "manifest.json"))); var file = manifest.RootElement.GetProperty("Business").GetString()!;
        File.AppendAllText(Path.Combine(f.Root, "generations", file), " "); Assert.Throws<ExternalStorageException>(() => new LocalWorkspace(f.Root));
    }
    [Theory]
    [InlineData("before-manifest", false)]
    [InlineData("after-manifest", true)]
    public async Task Failed_or_uncertain_commit_retries_same_key_without_duplicate(string phase, bool committed)
    {
        using var f = await LocalOfflineFixture.Start(); var req = await f.Request(); f.W.FailureProbe = p => { if (p == phase) throw new IOException("Injected fixture disk/response failure"); };
        await Assert.ThrowsAsync<ExternalStorageTransientException>(() => f.Assets.CreateAsync(f.Admin, req, "same-key")); f.W.FailureProbe = null; f.Restart();
        Assert.Equal(committed ? 1 : 0, (await f.Assets.ListAsync(f.Admin, new())).Total);
        var result = await f.Assets.CreateAsync(f.Admin, req, "same-key"); Assert.Equal(committed, result.Replayed); Assert.Equal(1, (await f.Assets.ListAsync(f.Admin, new())).Total);
        var stale = new AssetWriteRequest(req.Asset with { Name = "GIẢ khác" }, 0, req.TreeVersionId); await Assert.ThrowsAsync<AssetConflictException>(() => f.Assets.UpdateAsync(f.Admin, result.Asset.AssetId, stale, "stale"));
    }
    [Fact]
    public async Task Reads_do_not_wait_for_unrelated_writer_and_return_detached_data()
    {
        using var f = await LocalOfflineFixture.Start(); var entered = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var write = f.W.ExecuteAsync(async ct => { entered.SetResult(); await release.Task; return true; }); await entered.Task;
        try { Assert.Equal(3, (await f.Inventory.ListAsync(f.Admin, new()).WaitAsync(TimeSpan.FromSeconds(1))).Total); } finally { release.SetResult(); await write; }
        var state = await ((IInventoryStore)f.W).ReadAsync(); state.Items[0].Cells[1] = "TAMPER"; state.Operations.Last().Items[0].Cells[1] = "TAMPER";
        Assert.DoesNotContain((await ((IInventoryStore)f.W).ReadAsync()).Items, i => i.Name == "TAMPER");
    }
    [Fact]
    public async Task Media_bytes_retry_restart_and_full_registry_tree_relocate_are_preserved()
    {
        using var f = await LocalOfflineFixture.Start(); var asset = (await f.Assets.CreateAsync(f.Admin, await f.Request(), "create")).Asset;
        var upload = await f.Media.UploadPhotoAsync(f.Admin, asset.AssetId, MediaBytes.Png(), 1, "GIẢ", true, "photo");
        var again = await f.Media.UploadPhotoAsync(f.Admin, asset.AssetId, MediaBytes.Png(), 1, "GIẢ", true, "photo"); Assert.True(again.Replayed); Assert.Equal(upload.File.FileId, again.File.FileId);
        var d = await f.Media.CreateDocumentAsync(f.Admin, new(new("GIẢ hồ sơ", "FAKE-DOC", f.Child), 0), "doc");
        var version = await f.Media.CreateVersionAsync(f.Admin, d.EntityId, new("GIẢ v1", 1), "ver");
        var dx = await f.Media.UploadDocumentFileAsync(f.Admin, version.EntityId, "DOCX", MediaBytes.Docx(), 1, "docx");
        var pdf = await f.Media.UploadDocumentFileAsync(f.Admin, version.EntityId, "PDF", MediaBytes.Pdf(), 2, "pdf");
        await f.Media.PublishVersionAsync(f.Admin, version.EntityId, new(3, 2, DateOnly.FromDateTime(DateTime.UtcNow), "GIẢ publish", true), "publish");
        var request = new TreeMutationRequest("ARCHIVE_CATEGORY", "GIẢ relocate", f.Roots[0], f.Roots[1], DeletePlan: "RELOCATE"); var p = await f.Tree.PreviewAsync(f.Admin, request); Assert.True(p.CanCommit, string.Join(" ", p.Issues));
        var result = await f.Tree.CommitAsync(f.Admin, request, p.TreeRevision, p.AssetRevision, p.PreviewHash, "tree", true); Assert.True(result.RegistryReconciled); f.Restart();
        var moved = (await f.Assets.ListAsync(f.Admin, new())).Items.Single().Asset; Assert.Equal(asset.AssetId, moved.AssetId); Assert.Equal(asset.ObjectCode, moved.ObjectCode); Assert.Equal(f.Roots[1], moved.CategoryId);
        Assert.All((await f.Inventory.ListAsync(f.Admin, new())).Items, i => Assert.Equal(f.Roots[1], i.CategoryId)); Assert.Equal(f.Roots[1], (await f.Media.DocumentAsync(f.Admin, d.EntityId)).CategoryId);
        Assert.Equal(MediaBytes.Png().Bytes, (await f.Media.DownloadAsync(f.Admin, upload.File.FileId)).Bytes);
        Assert.Equal(MediaBytes.Docx().Bytes, (await f.Media.DownloadAsync(f.Admin, dx.File.FileId)).Bytes);
        Assert.Equal(MediaBytes.Pdf().Bytes, (await f.Media.DownloadAsync(f.Admin, pdf.File.FileId)).Bytes);
        Assert.Equal("PUBLISHED_DEMO", (await f.Media.DocumentAsync(f.Admin, d.EntityId)).Versions.Single().State);
        Assert.True((await f.Tree.CommitAsync(f.Admin, request, p.TreeRevision, p.AssetRevision, p.PreviewHash, "tree", true)).Replayed);
    }
    [Fact]
    public async Task Scoped_viewer_cannot_read_other_branch_or_admin_and_revoke_is_immediate()
    {
        using var f = await LocalOfflineFixture.Start(); await f.Assets.CreateAsync(f.Admin, await f.Request(), "a"); var user = await f.Auth.CreateUserAsync(f.Admin, "fake-viewer", "GIẢ Viewer");
        Assert.True(await f.Auth.ChangePasswordAsync(user.UserId, user.TemporaryPassword, "cccccccc"));
        await f.Auth.ReplaceRoleAssignmentsAsync(f.Admin, user.UserId, [new(AuthRole.Viewer, DateTimeOffset.UtcNow.AddMinutes(-1), null, [new(AuthScopeType.OrgUnit, "ALL", true), new(AuthScopeType.Location, "ALL", true), new(AuthScopeType.CategorySubtree, f.Roots[1], true)])]);
        var login = await f.Auth.LoginAsync(new("fake-viewer", "cccccccc", AuthClientType.Web, false, "fixture")); Assert.Equal(LoginStatus.Success, login.Status); Assert.NotNull(await f.Auth.ValidateAccessTokenAsync(login.AccessToken));
        Assert.Equal(0, (await f.Assets.ListAsync(user.UserId, new())).Total); Assert.Equal(0, (await f.Reports.ReadAsync(user.UserId, "R01", new())).Summary.AssetCount); Assert.Equal(0, (await f.Inventory.ListAsync(user.UserId, new())).Total);
        await Assert.ThrowsAsync<AssetForbiddenException>(() => f.Assets.CreateAsync(user.UserId, awaitableRequest(), "forbidden"));
        AssetWriteRequest awaitableRequest() => new(new("GIẢ", f.Child, "OU-FAKE", "LOC-FAKE"), 0, "unused");
        await f.Auth.SetUserLockedAsync(f.Admin, user.UserId, true); Assert.Null(await f.Auth.ValidateAccessTokenAsync(login.AccessToken));
        await Assert.ThrowsAsync<LastActiveAdminException>(() => f.Auth.SetUserLockedAsync(f.Admin, f.Admin, true));
    }
    [Fact]
    public async Task Pending_upload_reuses_blob_identity_and_reports_missing_bytes_after_corruption()
    {
        using var f = await LocalOfflineFixture.Start(); var asset = (await f.Assets.CreateAsync(f.Admin, await f.Request(), "owner")).Asset; var commits = 0;
        f.W.FailureProbe = phase => { if (phase == "before-manifest" && ++commits == 2) throw new IOException("Injected after bytes before AVAILABLE"); };
        await Assert.ThrowsAsync<MediaPendingException>(() => f.Media.UploadPhotoAsync(f.Admin, asset.AssetId, MediaBytes.Png(), 1, "GIẢ", true, "same-upload"));
        f.W.FailureProbe = null; var pending = (await f.Media.PhotosAsync(f.Admin, asset.AssetId)).Single(); Assert.Equal("PENDING", pending.State); f.Restart();
        var done = await f.Media.UploadPhotoAsync(f.Admin, asset.AssetId, MediaBytes.Png(), 1, "GIẢ", true, "same-upload"); Assert.Equal(pending.FileId, done.File.FileId); Assert.Equal("AVAILABLE", done.File.State); Assert.Single(Directory.EnumerateFiles(Path.Combine(f.Root, "blobs")));
        var metadata = (await ((IMediaCatalogStore)f.W).ReadAsync()).Files.Single(); File.WriteAllBytes(Path.Combine(f.Root, "blobs", metadata.DriveFileId), [0]);
        await Assert.ThrowsAsync<ExternalStorageException>(() => f.Media.DownloadAsync(f.Admin, done.File.FileId)); f.W.Dispose(); Assert.Throws<ExternalStorageException>(() => new LocalWorkspace(f.Root));
    }
    [Fact]
    public async Task Read_scope_pins_all_module_references_while_writer_publishes_next_generation()
    {
        using var f = await LocalOfflineFixture.Start(); var a = (await f.Assets.CreateAsync(f.Admin, await f.Request(), "old")).Asset; var readStarted = new TaskCompletionSource(); var readContinue = new TaskCompletionSource();
        var read = f.W.ReadAsync(async ct => { var before = await ((IAssetCatalogStore)f.W).ReadAsync(ct); readStarted.SetResult(); await readContinue.Task; var after = await ((IAssetCatalogStore)f.W).ReadAsync(ct); return (before.Revision, after.Revision); });
        await readStarted.Task; await f.Assets.CreateAsync(f.Admin, await f.Request("GIẢ mới"), "new"); readContinue.SetResult(); var versions = await read; Assert.Equal(versions.Item1, versions.Item2); Assert.Equal(2, (await f.Assets.ListAsync(f.Admin, new())).Total);
    }

    [Fact]
    public async Task Actual_import_preview_chunks_stock_export_and_request_auth_cache_work_without_Google()
    {
        using var f = await LocalOfflineFixture.Start(0); var cells = Enumerable.Repeat("", 20).ToArray(); cells[0] = "FAKE-IMPORT"; cells[1] = "GIẢ nhập qua service"; cells[2] = "Vật tư hàng hóa"; cells[3] = "GIẢ"; cells[5] = "cái";
        var req = new ItemImportRequest(new("MISA_VTHH", ItemImportReader.Headers, [new(3, cells)], ""), [new("GIẢ", f.Child, true)], [], "GIẢ local import"); var preview = await f.Inventory.PreviewAsync(f.Admin, req); Assert.Empty(preview.Errors);
        var job = await f.Inventory.StartAsync(f.Admin, req, preview.Revision, preview.PreviewHash, "import", true); Assert.Equal("VALIDATED", job.State); job = await f.Inventory.CommitChunkAsync(f.Admin, job.JobId, 0, "chunk"); Assert.Equal("COMPLETE", job.State); Assert.Equal("COMPLETE", (await f.Inventory.CommitChunkAsync(f.Admin, job.JobId, 0, "chunk")).State);
        var item = (await f.Inventory.ListAsync(f.Admin, new())).Items.Single(); await f.Inventory.WriteStockAsync(f.Admin, new(item.ItemId, "LOC-FAKE", "cái", 0, 2, DateTimeOffset.UtcNow, "GIẢ", 0, "GIẢ snapshot"), "stock");
        Assert.Contains((await f.Reports.ReadAsync(f.Admin, "R03", new())).Stock, r => r.ItemId == item.ItemId && r.Quantity == 0); Assert.NotEmpty((await f.Reports.ExportAsync(f.Admin, "R03", new())).Bytes); Assert.NotEmpty(await f.Categories.ExportAsync(f.Admin)); f.Restart(); Assert.Equal(0, (await f.Inventory.StockAsync(f.Admin, new())).Items.Single().Quantity);
        using var trace = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Begin(); var wrapped = new DuyTien.AssetManagement.Server.LocalOffline.RequestAuthStore(f.W);
        using (DuyTien.AssetManagement.Server.LocalOffline.RequestAuthStore.Begin()) { await wrapped.ReadAsync(s => s.Users.Count); await wrapped.ReadAsync(s => s.Users.Count); await wrapped.MutateAsync(s => s.Users.Count); await wrapped.ReadAsync(s => s.Users.Count); }
        Assert.Equal(1, trace.Values["auth-store-read"].Count);
        using (DuyTien.AssetManagement.Server.LocalOffline.RequestAuthStore.Begin()) await wrapped.ReadAsync(s => s.Users.Count); Assert.Equal(2, trace.Values["auth-store-read"].Count);
    }

    [Fact]
    public async Task Concurrent_creates_keep_unique_codes_and_asset_commit_cannot_modify_reference_rules()
    {
        using var f = await LocalOfflineFixture.Start(0); var req = await f.Request(); var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(n => f.Assets.CreateAsync(f.Admin, req, "parallel-" + n)));
        Assert.Equal(8, created.Select(r => r.Asset.AssetId).Distinct().Count()); Assert.Equal(8, created.Select(r => r.Asset.ObjectCode).Distinct().Count());
        var state = await ((IAssetCatalogStore)f.W).ReadAsync(); state.Rules[0] = state.Rules[0] with { Prefix = "BAD" };
        await Assert.ThrowsAsync<ExternalStorageException>(() => f.W.ExecuteAsync(async ct => { await ((IAssetCatalogStore)f.W).CommitAsync(state, state.Revision, ct); return true; }));
        f.Restart(); Assert.Equal(8, (await f.Assets.ListAsync(f.Admin, new())).Total);
    }

}
