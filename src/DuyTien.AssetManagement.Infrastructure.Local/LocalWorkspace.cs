using System.Security.AccessControl;
using System.Security.Principal;
using DuyTien.AssetManagement.Application.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
namespace DuyTien.AssetManagement.Infrastructure.Local;

public sealed record LocalBusiness(CategoryCatalogState Categories, AssetCatalogState Assets, InventoryState Inventory, MediaCatalogState Media);
public sealed record LocalFileReference(string Name, string Hash);
public sealed record LocalBusinessFiles(LocalFileReference Categories, LocalFileReference Assets, LocalFileReference Inventory, LocalFileReference Media);
public sealed record LocalManifest(int Schema, string WorkspaceId, long Generation, string Business, string BusinessHash, string Auth, string AuthHash);
// No Google dependency and no HTTP clients. The process lease covers every reader/writer/CLI.
public sealed class LocalWorkspace : ICategoryCatalogStore, IAssetCatalogStore, IInventoryStore, IMediaCatalogStore,
    ITreeMutationStore, IAuthSystemStore, IAuthSnapshotValidation, IManagedMediaContentStore, ISingleWriterCoordinator, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        IgnoreReadOnlyProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly string root;
    private readonly FileStream lease;
    private readonly SemaphoreSlim writer = new(1, 1), authWriter = new(1, 1), manifestGate = new(1, 1);
    private readonly AsyncLocal<LocalBusiness?> pinned = new();
    private readonly AsyncLocal<bool> writing = new();
    private LocalBusiness business = new(new(), new(), new(), new());
    private AuthSystemState auth = new();
    private LocalManifest manifest = null!;
    private LocalBusinessFiles businessFiles = null!;
    public bool CanValidateReadOnly => true;
    public string WorkspaceId => manifest.WorkspaceId;
    public string Root => root;
    public long Generation => Volatile.Read(ref manifest).Generation;
    public double StartupMilliseconds { get; }
    public IReadOnlyDictionary<string, double> StartupPhases { get; }
    internal Action<string>? FailureProbe { get; set; } // fault injection for filesystem transaction tests only
    public LocalWorkspace(string directory)
    {
        using var startupTrace = PerformanceTrace.Begin(); var clock = System.Diagnostics.Stopwatch.StartNew(); root = Path.GetFullPath(directory);
        if (!Path.IsPathFullyQualified(directory) || Directory.Exists(root) && new DirectoryInfo(root).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("Workspace cần đường dẫn local tuyệt đối, không reparse point.");
        if (OperatingSystem.IsWindows() && new DriveInfo(Path.GetPathRoot(root)!).DriveFormat != "NTFS") throw new InvalidOperationException("Workspace cần NTFS local cho giao thức commit đã kiểm.");
        Directory.CreateDirectory(root);
        for (var ancestor = new DirectoryInfo(root); ancestor is not null; ancestor = ancestor.Parent) if (ancestor.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("Workspace path không được đi qua reparse point.");
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent(); var acl = new DirectorySecurity(); acl.SetOwner(identity.User!); acl.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { identity.User!, new SecurityIdentifier("S-1-5-18") }) acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(root).SetAccessControl(acl);
        }
        else File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateDirectory(Path.Combine(root, "generations")); Directory.CreateDirectory(Path.Combine(root, "blobs")); Directory.CreateDirectory(Path.Combine(root, "keys"));
        foreach (var part in new[] { "generations", "blobs", "keys" }) if (new DirectoryInfo(Path.Combine(root, part)).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("Workspace subdirectory không được reparse.");
        lease = new FileStream(Path.Combine(root, "workspace.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var path = Path.Combine(root, "manifest.json");
            if (File.Exists(path))
            {
                manifest = Deserialize<LocalManifest>(File.ReadAllBytes(path));
                if (manifest.Schema != 1 || !Guid.TryParseExact(manifest.WorkspaceId, "N", out _) || manifest.Generation < 1) throw new ExternalStorageException("Workspace schema/identity chưa được hỗ trợ.");
                businessFiles = Deserialize<LocalBusinessFiles>(ReadVerified(manifest.Business, manifest.BusinessHash));
                business = new(ReadPart<CategoryCatalogState>(businessFiles.Categories), ReadPart<AssetCatalogState>(businessFiles.Assets), ReadPart<InventoryState>(businessFiles.Inventory), ReadPart<MediaCatalogState>(businessFiles.Media));
                auth = Deserialize<AuthSystemState>(ReadVerified(manifest.Auth, manifest.AuthHash));
                Validate(business); ValidateAuth(auth);
                foreach (var file in business.Media.Files.Where(f => f.State == "AVAILABLE")) VerifyBytes(file, File.ReadAllBytes(BlobPath(file.DriveFileId)));
            }
            else
            {
                if (Directory.EnumerateFiles(Path.Combine(root, "generations")).Any() || File.Exists(Path.Combine(root, "manifest.previous.json"))) throw new ExternalStorageException("Workspace có dữ liệu nhưng thiếu manifest; không reset.");
                businessFiles = new(WritePart("categories", business.Categories), WritePart("assets", business.Assets), WritePart("inventory", business.Inventory), WritePart("media", business.Media));
                var b = WriteGeneration("business", businessFiles); var a = WriteGeneration("auth", auth);
                manifest = new(1, Guid.NewGuid().ToString("N"), 1, b.Name, b.Hash, a.Name, a.Hash);
                AtomicManifest(manifest);
            }
            using (var metric = PerformanceTrace.Measure("index-build")) _ = business.Inventory.SearchIndex;
            StartupMilliseconds = clock.Elapsed.TotalMilliseconds; StartupPhases = startupTrace.Values.ToDictionary(p => p.Key, p => p.Value.Milliseconds);
        }
        catch { lease.Dispose(); throw; }
    }
    private LocalBusiness Current => pinned.Value ?? Volatile.Read(ref business);
    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); var old = pinned.Value; pinned.Value = Volatile.Read(ref business); try { return await operation(ct); } finally { pinned.Value = old; } }
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    { using (var metric = PerformanceTrace.Measure("coordinator-wait")) await writer.WaitAsync(ct); var old = pinned.Value; pinned.Value = null; writing.Value = true; try { return await operation(ct); } finally { writing.Value = false; pinned.Value = old; writer.Release(); } }
    Task<CategoryCatalogState> ICategoryCatalogStore.ReadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(CloneCategories(Current.Categories)); }
    Task<AssetCatalogState> IAssetCatalogStore.ReadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(CloneAssets(Current.Assets)); }
    Task<InventoryState> IInventoryStore.ReadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(CloneInventory(Current.Inventory)); }
    Task<MediaCatalogState> IMediaCatalogStore.ReadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(CloneMedia(Current.Media)); }
    Task<TreeMutationSnapshot> ITreeMutationStore.ReadAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); var b = Current; return Task.FromResult(new TreeMutationSnapshot(CloneCategories(b.Categories), CloneAssets(b.Assets), CloneInventory(b.Inventory), CloneMedia(b.Media))); }
    async Task ICategoryCatalogStore.CommitAsync(CategoryCatalogState candidate, long expected, CancellationToken ct)
    { var b = Volatile.Read(ref business); if (b.Categories.Revision != expected) throw new CategoryConflictException("Workspace tree revision đã đổi."); await Commit(b with { Categories = candidate }, ct); }
    async Task IAssetCatalogStore.CommitAsync(AssetCatalogState candidate, long expected, CancellationToken ct)
    {
        var b = Volatile.Read(ref business); if (b.Assets.Revision != expected) throw new AssetConflictException("Workspace asset revision đã đổi.");
        if (!candidate.Rules.SequenceEqual(b.Assets.Rules) || !candidate.OrgUnits.SequenceEqual(b.Assets.OrgUnits) || !candidate.Locations.SequenceEqual(b.Assets.Locations)) throw new ExternalStorageException("Không đổi reference/rule qua commit tài sản.");
        await Commit(b with { Assets = candidate }, ct);
    }
    async Task IInventoryStore.CommitAsync(InventoryState candidate, long expected, CancellationToken ct)
    { var b = Volatile.Read(ref business); if (b.Inventory.Revision != expected) throw new InventoryConflictException("Workspace inventory revision đã đổi."); await Commit(b with { Inventory = candidate }, ct); }
    async Task IMediaCatalogStore.CommitAsync(MediaCatalogState candidate, long expected, CancellationToken ct)
    { var b = Volatile.Read(ref business); if (b.Media.Revision != expected) throw new MediaConflictException("Workspace media revision đã đổi."); await Commit(b with { Media = candidate }, ct); }
    async Task ITreeMutationStore.CommitAsync(TreeMutationSnapshot candidate, long expectedTree, long expectedAsset, CancellationToken ct)
    {
        var b = Volatile.Read(ref business);
        if (b.Categories.Revision != expectedTree || b.Assets.Revision != expectedAsset || candidate.Inventory is null || candidate.Media is null) throw new CategoryConflictException("Joint revision/registry đã đổi hoặc thiếu.");
        await Commit(new(candidate.Categories, candidate.Assets, candidate.Inventory, candidate.Media), ct);
    }
    private async Task Commit(LocalBusiness candidate, CancellationToken ct)
    { try { await CommitCore(candidate, ct); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new ExternalStorageTransientException("Workspace local chưa xác nhận commit. Giữ payload/key để đối soát/retry.", true, e); } }
    private async Task CommitCore(LocalBusiness candidate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (!writing.Value) throw new InvalidOperationException("Local commit cần single-writer coordinator.");
        var previous = Volatile.Read(ref business);
        Prefix(previous.Categories.Operations, candidate.Categories.Operations); Prefix(previous.Categories.TreeChanges, candidate.Categories.TreeChanges);
        Prefix(previous.Assets.Operations, candidate.Assets.Operations); Prefix(previous.Inventory.Operations, candidate.Inventory.Operations); Prefix(previous.Media.Operations, candidate.Media.Operations);
        if (candidate.Categories.Revision < previous.Categories.Revision || candidate.Assets.Revision < previous.Assets.Revision || candidate.Inventory.Revision < previous.Inventory.Revision || candidate.Media.Revision < previous.Media.Revision) throw new ExternalStorageException("Không rollback revision.");
        Validate(candidate);
        // Only changed modules get a new file. One small business descriptor atomically references all four.
        // An unrelated photo/asset write never rewrites the 20k-Item JSON. Journals remain materialized and validated.
        T Freeze<T>(T value) => Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, Json));
        var frozen = new LocalBusiness(ReferenceEquals(candidate.Categories, previous.Categories) ? previous.Categories : Freeze(candidate.Categories),
            ReferenceEquals(candidate.Assets, previous.Assets) ? previous.Assets : Freeze(candidate.Assets),
            ReferenceEquals(candidate.Inventory, previous.Inventory) ? previous.Inventory : Freeze(candidate.Inventory),
            ReferenceEquals(candidate.Media, previous.Media) ? previous.Media : Freeze(candidate.Media));
        var parts = new LocalBusinessFiles(ReferenceEquals(frozen.Categories, previous.Categories) ? businessFiles.Categories : WritePart("categories", frozen.Categories),
            ReferenceEquals(frozen.Assets, previous.Assets) ? businessFiles.Assets : WritePart("assets", frozen.Assets),
            ReferenceEquals(frozen.Inventory, previous.Inventory) ? businessFiles.Inventory : WritePart("inventory", frozen.Inventory),
            ReferenceEquals(frozen.Media, previous.Media) ? businessFiles.Media : WritePart("media", frozen.Media));
        _ = frozen.Inventory.SearchIndex;
        var generation = WriteGeneration("business", parts); FailureProbe?.Invoke("before-manifest");
        using (var metric = PerformanceTrace.Measure("manifest-wait")) await manifestGate.WaitAsync(ct);
        try
        {
            var next = manifest with { Generation = manifest.Generation + 1, Business = generation.Name, BusinessHash = generation.Hash };
            if (!ReferenceEquals(Volatile.Read(ref business), previous)) throw new ExternalStorageException("Business snapshot đã đổi trước manifest; không overwrite.");
            AtomicManifest(next); businessFiles = parts; Volatile.Write(ref business, frozen); Volatile.Write(ref manifest, next); FailureProbe?.Invoke("after-manifest");
        }
        finally { manifestGate.Release(); }
    }
    Task<T> IAuthSystemStore.ReadAsync<T>(Func<AuthSystemState, T> reader, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(reader(Volatile.Read(ref auth).Clone())); }
    public async Task<T> MutateAsync<T>(Func<AuthSystemState, T> mutation, CancellationToken ct = default)
    { try { return await MutateCore(mutation, ct); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new ExternalStorageTransientException("Workspace auth chưa xác nhận lưu; không báo thành công.", true, e); } }
    private async Task<T> MutateCore<T>(Func<AuthSystemState, T> mutation, CancellationToken ct)
    {
        using (var metric = PerformanceTrace.Measure("auth-writer-wait")) await authWriter.WaitAsync(ct);
        try
        {
            var original = Volatile.Read(ref auth); var candidate = original.Clone(); var result = mutation(candidate); ValidateAuth(candidate);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(candidate, Json);
            if (bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(original, Json))) return result;
            var frozen = Deserialize<AuthSystemState>(bytes); var a = WriteGenerationBytes("auth", bytes);
            FailureProbe?.Invoke("before-auth-manifest"); using (var metric = PerformanceTrace.Measure("manifest-wait")) await manifestGate.WaitAsync(ct);
            try { var next = manifest with { Generation = manifest.Generation + 1, Auth = a.Name, AuthHash = a.Hash }; AtomicManifest(next); Volatile.Write(ref auth, frozen); Volatile.Write(ref manifest, next); } finally { manifestGate.Release(); }
            return result;
        }
        finally { authWriter.Release(); }
    }
    public Task<AuthSystemInitializationResult> InitializeAsync(string actor, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(new AuthSystemInitializationResult(false, AuthSystemSchema.CurrentVersion)); }
    public Task<string> AllocateIdAsync(CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); return Task.FromResult("LOCAL-" + Guid.NewGuid().ToString("N")); }
    public async Task EnsureUploadedAsync(ManagedFile file, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    { try { await UploadCore(file, bytes, ct); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new ExternalStorageTransientException("Chưa xác nhận bytes local; giữ operation/key.", true, e); } }
    private Task UploadCore(ManagedFile file, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); VerifyBytes(file, bytes.Span); var path = BlobPath(file.DriveFileId);
        if (File.Exists(path)) { VerifyBytes(file, File.ReadAllBytes(path)); return Task.CompletedTask; }
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try { DurableWrite(temp, bytes.Span); File.Move(temp, path); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return Task.CompletedTask;
    }
    public Task<byte[]> DownloadVerifiedAsync(ManagedFile file, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); try { var bytes = File.ReadAllBytes(BlobPath(file.DriveFileId)); VerifyBytes(file, bytes); return Task.FromResult(bytes); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new ExternalStorageException("Bytes local bị thiếu hoặc không đọc được; không giả file đã có.", e); } }
    private string BlobPath(string id)
    { if (!id.StartsWith("LOCAL-", StringComparison.Ordinal) || !Guid.TryParseExact(id[6..], "N", out _)) throw new ExternalStorageException("Blob reference không thuộc workspace local hoặc thiếu bytes."); return Path.Combine(root, "blobs", id); }
    private static void VerifyBytes(ManagedFile f, ReadOnlySpan<byte> bytes)
    { if (bytes.Length != f.Size || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(f.Sha256, StringComparison.OrdinalIgnoreCase)) throw new ExternalStorageException("Bytes file thiếu hoặc không khớp size/hash."); }
    private static void Prefix<T>(IReadOnlyList<T> before, IReadOnlyList<T> after)
    { if (after.Count < before.Count || !JsonSerializer.Serialize(before, Json).Equals(JsonSerializer.Serialize(after.Take(before.Count), Json), StringComparison.Ordinal)) throw new ExternalStorageException("Journal lịch sử không append-only."); }
    public Task InitializeDemoReferencesAsync(string[] roots, CancellationToken ct = default) => ExecuteAsync(async token =>
    {
        var b = Volatile.Read(ref business); if (b.Assets.OrgUnits.Count > 0 || b.Assets.Locations.Count > 0 || b.Assets.Rules.Count > 0)
        { if (b.Assets.OrgUnits.Count == 1 && b.Assets.OrgUnits[0].Id == "OU-FAKE" && b.Assets.Locations.Count == 1 && b.Assets.Locations[0].Id == "LOC-FAKE" && b.Assets.Rules.Count == roots.Length && b.Assets.Rules.All(r => roots.Contains(r.CategoryRootId) && r.Prefix == "LOCAL" && r.ApprovedForDemo && r.Status == "DEMO")) return true; throw new InvalidOperationException("Reference đã có; không overwrite."); }
        if (roots.Length == 0 || roots.Distinct().Count() != roots.Length || roots.Any(id => !b.Categories.ActiveNodes.Any(n => n.CategoryId == id && n.ParentId is null && n.Name.StartsWith("GIẢ ", StringComparison.Ordinal)))) throw new InvalidOperationException("Chỉ seed reference GIẢ cho root GIẢ explicit.");
        var a = b.Assets.Clone(); a.OrgUnits.Add(new("OU-FAKE", null, "GIẢ đơn vị local", true)); a.Locations.Add(new("LOC-FAKE", null, "OU-FAKE", "GIẢ địa điểm local", true));
        foreach (var id in roots) { var node = b.Categories.ActiveNodes.Single(n => n.CategoryId == id); a.Rules.Add(new("RULE-" + Guid.NewGuid().ToString("N"), id, "LOCAL", node.SegmentCode, "DEMO", true, 1)); }
        await Commit(b with { Assets = a }, token); return true;
    }, ct);
    private static CategoryCatalogState CloneCategories(CategoryCatalogState source)
    { var s = source.Clone(); for (var i = 0; i < s.TreeChanges.Count; i++) { var o = s.TreeChanges[i]; s.TreeChanges[i] = o with { CategoryIds = o.CategoryIds.ToArray(), AssetIds = o.AssetIds.ToArray(), Diff = o.Diff.ToArray(), RegistryImpacts = o.RegistryImpacts?.ToArray(), Request = o.Request with { AssetIds = o.Request.AssetIds?.ToArray() } }; } return s; }
    private static AssetCatalogState CloneAssets(AssetCatalogState source)
    { var s = source.Clone(); for (var i = 0; i < s.Operations.Count; i++) s.Operations[i] = s.Operations[i] with { AliasesAfter = s.Operations[i].AliasesAfter.ToArray() }; return s; }
    private static MediaCatalogState CloneMedia(MediaCatalogState source)
    { var s = source.Clone(); for (var i = 0; i < s.Operations.Count; i++) s.Operations[i] = s.Operations[i] with { FilesAfter = s.Operations[i].FilesAfter.ToArray() }; return s; }
    private static ItemRecord CloneItem(ItemRecord i) => i with { Cells = i.Cells.ToArray() };
    private static ItemImportPlanRow ClonePlan(ItemImportPlanRow p) => p with { Row = p.Row with { Item = CloneItem(p.Row.Item) } };
    private static InventoryState CloneInventory(InventoryState source)
    {
        using var metric = PerformanceTrace.Measure("snapshot-clone");
        var s = source.Clone(); for (var i = 0; i < s.Items.Count; i++) s.Items[i] = CloneItem(s.Items[i]);
        for (var i = 0; i < s.Plan.Count; i++) s.Plan[i] = ClonePlan(s.Plan[i]);
        for (var i = 0; i < s.Operations.Count; i++) { var o = s.Operations[i]; s.Operations[i] = o with { Plan = o.Plan.Select(ClonePlan).ToArray(), Items = o.Items.Select(CloneItem).ToArray() }; }
        return s;
    }
    private (string Name, string Hash) WriteGeneration<T>(string kind, T data) => WriteGenerationBytes(kind, JsonSerializer.SerializeToUtf8Bytes(data, Json));
    private (string Name, string Hash) WriteGenerationBytes(string kind, byte[] bytes)
    { var name = kind + "-" + Guid.NewGuid().ToString("N") + ".json"; var path = Path.Combine(root, "generations", name); DurableWrite(path, bytes); return (name, Convert.ToHexString(SHA256.HashData(bytes))); }
    private LocalFileReference WritePart<T>(string kind, T value) { var p = WriteGeneration(kind, value); return new(p.Name, p.Hash); }
    private T ReadPart<T>(LocalFileReference reference) => Deserialize<T>(ReadVerified(reference.Name, reference.Hash));
    private byte[] ReadVerified(string name, string hash)
    { using var metric = PerformanceTrace.Measure("snapshot-load"); if (Path.GetFileName(name) != name || !System.Text.RegularExpressions.Regex.IsMatch(name, "^(business|auth|categories|assets|inventory|media)-[a-f0-9]{32}\\.json$")) throw new ExternalStorageException("Generation reference không hợp lệ."); var bytes = File.ReadAllBytes(Path.Combine(root, "generations", name)); if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.Ordinal)) throw new ExternalStorageException("Workspace checksum không khớp; không fallback/reset."); return bytes; }
    private static T Deserialize<T>(byte[] bytes) { using var metric = PerformanceTrace.Measure("deserialize"); return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new ExternalStorageException("Workspace JSON rỗng."); }
    private static void DurableWrite(string path, ReadOnlySpan<byte> bytes)
    { using var metric = PerformanceTrace.Measure("durable-write"); using var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough); f.Write(bytes); f.Flush(true); }
    private void AtomicManifest(LocalManifest next)
    {
        var temp = Path.Combine(root, "manifest-" + Guid.NewGuid().ToString("N") + ".pending");
        try { DurableWrite(temp, JsonSerializer.SerializeToUtf8Bytes(next, Json)); var target = Path.Combine(root, "manifest.json"); if (File.Exists(target)) File.Replace(temp, target, Path.Combine(root, "manifest.previous.json")); else File.Move(temp, target); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static void Validate(LocalBusiness b)
    {
        using var metric = PerformanceTrace.Measure("reconcile");
        foreach (var version in b.Categories.Versions) if (CategoryTreeShape.Validate(b.Categories.Categories.Where(n => n.TreeVersionId == version.TreeVersionId).ToArray()).Count > 0) throw new ExternalStorageException("Cây lưu trữ không hợp lệ.");
        if (b.Categories.ActiveTreeVersionId is { } id && !b.Categories.Versions.Any(v => v.TreeVersionId == id) || b.Categories.Categories.Any(n => !b.Categories.Versions.Any(v => v.TreeVersionId == n.TreeVersionId))) throw new ExternalStorageException("Tree version reference không hợp lệ.");
        if (b.Categories.Revision != b.Categories.Operations.Count + b.Categories.TreeChanges.Count) throw new ExternalStorageException("Category journal/revision mismatch.");
        if (b.Assets.Assets.Any(a => !b.Categories.ActiveNodes.Any(n => n.CategoryId == a.CategoryId && (n.State == "DRAFT" || a.IsDeleted && n.State == "DRAFT_INACTIVE")) || !b.Assets.OrgUnits.Any(o => o.Id == a.OrgUnitId) || !b.Assets.Locations.Any(l => l.Id == a.LocationId && l.OrgUnitId == a.OrgUnitId))) throw new ExternalStorageException("Asset owner/reference chưa đối soát.");
        AssetCatalogIntegrity.ValidateReferences(b.Assets); AssetCatalogIntegrity.Validate(b.Assets);
        TreeRegistryIntegrity.Validate(new(b.Categories, b.Assets, b.Inventory, b.Media));
    }
    private static void ValidateAuth(AuthSystemState a)
    {
        if (a.Users.GroupBy(u => u.UserId).Any(g => g.Count() != 1) || a.Users.GroupBy(u => u.NormalizedUsername).Any(g => g.Count() != 1) || a.Users.Any(u => string.IsNullOrWhiteSpace(u.PasswordHash)) || a.RoleAssignments.Any(r => !a.Users.Any(u => u.UserId == r.UserId) || !AuthRoleCatalog.RoleExists(r.RoleCode)) || a.RoleAssignmentScopes.Any(s => !a.RoleAssignments.Any(r => r.AssignmentId == s.AssignmentId)) || a.Sessions.Any(s => !a.Users.Any(u => u.UserId == s.UserId))) throw new ExternalStorageException("Workspace auth không hợp lệ; không reset.");
    }
    public void Dispose() { lease.Dispose(); writer.Dispose(); authWriter.Dispose(); manifestGate.Dispose(); }
}
