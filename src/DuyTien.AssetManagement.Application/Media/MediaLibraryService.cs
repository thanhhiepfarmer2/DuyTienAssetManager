using System.Security.Cryptography;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
namespace DuyTien.AssetManagement.Application.Media;

public sealed partial class MediaLibraryService(IMediaCatalogStore store, IManagedMediaContentStore content,
    IAssetCatalogStore assets, ICategoryCatalogStore categories, IAuthSystemStore authStore,
    ISingleWriterCoordinator writer, TimeProvider clock, MediaUploadOptions limits)
{
    private readonly ScopeAuthorizationService policy = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<IReadOnlyList<DocumentView>> DocumentsAsync(string actor, string? query = null, CancellationToken ct = default) => writer.ReadAsync<IReadOnlyList<DocumentView>>(async token =>
    {
        if (query?.Length > 200) throw new MediaValidationException("Từ khóa tối đa200 ký tự."); var c = await Load(actor, "DOCUMENTS_READ", false, token);
        return c.Media.Documents.Where(d => CanDocument(c, actor, d, "DOCUMENTS_READ") && DocumentVisible(c, actor, d))
            .Where(d => string.IsNullOrWhiteSpace(query) || (d.Name + " " + d.BusinessCode).Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(d => d.Name, StringComparer.Ordinal).Select(d => ViewDocument(c, actor, d)).ToArray();
    }, ct);
    public Task<DocumentView> DocumentAsync(string actor, string id, CancellationToken ct = default) => writer.ReadAsync(async token =>
    { var c = await Load(actor, "DOCUMENTS_READ", false, token); var d = FindDocument(c, actor, id, "DOCUMENTS_READ"); if (!DocumentVisible(c, actor, d)) throw new MediaNotFoundException(); return ViewDocument(c, actor, d); }, ct);
    public Task<IReadOnlyList<MediaFileView>> PhotosAsync(string actor, string assetId, CancellationToken ct = default) => writer.ReadAsync<IReadOnlyList<MediaFileView>>(async token =>
    {
        var c = await Load(actor, "ASSET_FILES_DOWNLOAD", true, token); RequireAsset(c, actor, assetId, "ASSET_FILES_DOWNLOAD"); var manager = Allowed(c, actor, "ASSET_FILES_UPLOAD", AssetScope(c.AssetState.Assets.Single(x => x.AssetId == assetId)));
        return c.Media.Files.Where(f => f.OwnerType == "ASSET" && f.OwnerId == assetId && (!f.IsDeleted || manager)).OrderByDescending(f => f.IsPrimary).ThenBy(f => f.CreatedAt).Select(View).ToArray();
    }, ct);
    public Task<MediaMutationResult> CreateDocumentAsync(string actor, DocumentWriteRequest input, string key, CancellationToken ct = default) => DocumentWrite(actor, null, input, key, ct);
    public Task<MediaMutationResult> UpdateDocumentAsync(string actor, string id, DocumentWriteRequest input, string key, CancellationToken ct = default) => DocumentWrite(actor, id, input, key, ct);
    private Task<MediaMutationResult> DocumentWrite(string actor, string? id, DocumentWriteRequest input, string key, CancellationToken ct) => writer.ExecuteAsync(async token =>
    {
        Key(key); if (input?.Document is null) throw new MediaValidationException("Thiếu metadata tài liệu."); var form = input.Document with { Name = Text(input.Document.Name, 300, "Tên"), BusinessCode = Text(input.Document.BusinessCode, 100, "Mã nghiệp vụ"), Description = Optional(input.Document.Description, 2000, "Mô tả") };
        var c = await Load(actor, "DOCUMENTS_MANAGE", false, token); RequireCategory(c, actor, form.CategoryId, "DOCUMENTS_MANAGE"); var hash = Hash(new { actor, id, input = form, input.ExpectedVersion }); var replay = Replay(c, actor, key, hash);
        if (replay?.DocumentAfter is { } previous) { FindDocument(c, actor, previous.DocumentId, "DOCUMENTS_MANAGE"); return new MediaMutationResult(previous.DocumentId, previous.Version, true); }
        var old = id is null ? null : FindDocument(c, actor, id, "DOCUMENTS_MANAGE"); if (input.ExpectedVersion != (old?.Version ?? 0)) throw new MediaConflictException("Metadata tài liệu đã đổi; tải lại.");
        if (old is not null && old.CategoryId != form.CategoryId) throw new MediaValidationException("Không đổi scope category qua form metadata tài liệu; chưa có plan chuyển tài liệu."); var now = clock.GetUtcNow();
        var d = old is null ? new LibraryDocument("DOC-" + Guid.NewGuid().ToString("N"), form.Name, form.BusinessCode, form.CategoryId, form.Description, "DRAFT_DEMO", false, 1, null, now, actor, now, actor) : old with { Name = form.Name, BusinessCode = form.BusinessCode, Description = form.Description, Version = old.Version + 1, UpdatedAt = now, UpdatedBy = actor };
        await Persist(c, actor, key, hash, old is null ? "DOCUMENT_CREATE" : "DOCUMENT_UPDATE", "Metadata nháp", [], d, null, token); return new MediaMutationResult(d.DocumentId, d.Version);
    }, ct);
    public Task<MediaMutationResult> CreateVersionAsync(string actor, string documentId, DocumentVersionRequest input, string key, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        Key(key); var label = Text(input?.Label, 80, "Nhãn phiên bản"); var c = await Load(actor, "DOCUMENTS_MANAGE", false, token); var d = FindDocument(c, actor, documentId, "DOCUMENTS_MANAGE"); var hash = Hash(new { actor, documentId, input }); var replay = Replay(c, actor, key, hash);
        if (replay?.VersionAfter is { } old) return new MediaMutationResult(old.VersionId, old.Version, true);
        if (input!.ExpectedDocumentVersion != d.Version) throw new MediaConflictException("Metadata tài liệu đã đổi; tải lại."); if (c.Media.Versions.Any(v => v.DocumentId == documentId && v.Label == label)) throw new MediaConflictException("Nhãn phiên bản đã có; không ghi đè.");
        var v = new LibraryDocumentVersion("DOCVER-" + Guid.NewGuid().ToString("N"), documentId, label, "DRAFT", null, null, null, 1, clock.GetUtcNow(), actor, null);
        await Persist(c, actor, key, hash, "VERSION_CREATE", "Tạo phiên bản nháp", [], d with { Version = d.Version + 1, UpdatedAt = clock.GetUtcNow(), UpdatedBy = actor }, v, token); return new MediaMutationResult(v.VersionId, v.Version);
    }, ct);
    public Task<MediaMutationResult> PublishVersionAsync(string actor, string versionId, DocumentPublishRequest input, string key, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        Key(key); if (input is null || !input.ConfirmDemo) throw new MediaValidationException("Cần xác nhận phát hành mẫu DEMO, không phải ban hành chính thức."); Text(input.Reason, 500, "Lý do"); var c = await Load(actor, "DOCUMENTS_MANAGE", false, token); var v = FindVersion(c, actor, versionId, "DOCUMENTS_MANAGE"); var d = FindDocument(c, actor, v.DocumentId, "DOCUMENTS_MANAGE"); var hash = Hash(new { actor, versionId, input }); var replay = Replay(c, actor, key, hash);
        if (replay?.VersionAfter is { } old) return new MediaMutationResult(old.VersionId, old.Version, true);
        if (v.Version != input.ExpectedVersion || d.Version != input.ExpectedDocumentVersion) throw new MediaConflictException("Version đã đổi, tải lại trước phát hành."); if (v.State != "DRAFT") throw new MediaConflictException("Bản đã phát hành immutable; tạo version mới.");
        bool Ready(string? id, string kind) => c.Media.Files.Any(f => f.FileId == id && f.OwnerType == "DOCUMENT_VERSION" && f.OwnerId == versionId && f.Kind == kind && f.State == "AVAILABLE" && !f.IsDeleted);
        if (!Ready(v.DocxFileId, "DOCX") || !Ready(v.PdfFileId, "PDF")) throw new MediaConflictException("Thiếu DOCX hoặc PDF AVAILABLE cùng phiên bản; chưa phát hành.");
        var now = clock.GetUtcNow(); var saved = v with { State = "PUBLISHED_DEMO", EffectiveOn = input.EffectiveOn, Version = v.Version + 1, PublishedAt = now };
        await Persist(c, actor, key, hash, "VERSION_PUBLISH", input.Reason, [], d with { CurrentPublishedVersionId = versionId, Version = d.Version + 1, UpdatedAt = now, UpdatedBy = actor }, saved, token); return new MediaMutationResult(saved.VersionId, saved.Version);
    }, ct);
    public Task<MediaMutationResult> ArchiveDocumentAsync(string actor, string id, MediaLifecycleRequest input, string key, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        Key(key); if (input is null) throw new MediaValidationException("Thiếu yêu cầu."); Text(input.Reason, 500, "Lý do"); var c = await Load(actor, "DOCUMENTS_MANAGE", false, token); var hash = Hash(new { actor, id, input, Action = "ARCHIVE_DOCUMENT" }); var replay = Replay(c, actor, key, hash); if (replay?.DocumentAfter is { } previous) { RequireCategory(c, actor, previous.CategoryId, "DOCUMENTS_MANAGE"); return new MediaMutationResult(previous.DocumentId, previous.Version, true); }
        var d = FindDocument(c, actor, id, "DOCUMENTS_MANAGE");
        if (d.Version != input.ExpectedVersion) throw new MediaConflictException("Version tài liệu đã đổi."); var saved = d with { IsDeleted = true, Version = d.Version + 1, UpdatedAt = clock.GetUtcNow(), UpdatedBy = actor }; await Persist(c, actor, key, hash, "DOCUMENT_ARCHIVE", input.Reason, [], saved, null, token); return new MediaMutationResult(saved.DocumentId, saved.Version);
    }, ct);
    private async Task<Context> Load(string actor, string permission, bool assetScope, CancellationToken ct)
    {
        var auth = await authStore.ReadAsync(s => s.Clone(), ct); if (!auth.Users.Any(u => u.UserId == actor && u.State == AuthUserState.Active && !u.MustChangePassword) || !auth.RoleAssignments.Any(a => a.UserId == actor && AuthAuthorizationRules.IsEffective(a, clock.GetUtcNow()) && AuthRoleCatalog.HasPermission(a.RoleCode, permission))) throw new MediaForbiddenException();
        var tree = await categories.ReadAsync(ct); var assetState = assetScope ? await assets.ReadAsync(ct) : new AssetCatalogState(); var media = await store.ReadAsync(ct);
        try { return new(auth, tree, assetState, media, new(ScopeHierarchy.Create(AuthScopeType.OrgUnit, assetState.OrgUnits.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.Location, assetState.Locations.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.CategorySubtree, tree.ActiveNodes.Select(n => new ScopeNode(n.CategoryId, n.ParentId))))); }
        catch (AuthValidationException) { throw new MediaValidationException("Hierarchy scope cần đối soát; không phục vụ file ngoài scope."); }
    }
    private bool Allowed(Context c, string actor, string permission, ResourceScope scope) => policy.IsAllowed(c.Auth, actor, permission, scope, c.Hierarchy, clock.GetUtcNow());
    private static ResourceScope CategoryScope(string id) => new(new(false, null), new(false, null), new(true, id));
    private static ResourceScope AssetScope(AssetRecord a) => ResourceScope.Required(a.OrgUnitId, a.LocationId, a.CategoryId);
    private void RequireCategory(Context c, string actor, string id, string permission)
    { if (!c.Tree.ActiveNodes.Any(n => n.CategoryId == id && n.State == "DRAFT") || !Allowed(c, actor, "CATEGORIES_READ", CategoryScope(id)) || !Allowed(c, actor, permission, CategoryScope(id))) throw new MediaNotFoundException(); }
    private AssetRecord RequireAsset(Context c, string actor, string id, string permission)
    {
        var a = c.AssetState.Assets.SingleOrDefault(a => a.AssetId == id); if (a is null || a.IsDeleted || !c.Tree.ActiveNodes.Any(n => n.CategoryId == a.CategoryId && n.State == "DRAFT") || !c.AssetState.OrgUnits.Any(o => o.Id == a.OrgUnitId && o.IsActive) || !c.AssetState.Locations.Any(l => l.Id == a.LocationId && l.OrgUnitId == a.OrgUnitId && l.IsActive) || !Allowed(c, actor, "ASSETS_READ", AssetScope(a)) || !Allowed(c, actor, permission, AssetScope(a))) throw new MediaNotFoundException(); return a;
    }
    private bool CanDocument(Context c, string actor, LibraryDocument d, string permission) => !d.IsDeleted && c.Tree.ActiveNodes.Any(n => n.CategoryId == d.CategoryId && n.State == "DRAFT") && Allowed(c, actor, "CATEGORIES_READ", CategoryScope(d.CategoryId)) && Allowed(c, actor, permission, CategoryScope(d.CategoryId));
    private LibraryDocument FindDocument(Context c, string actor, string id, string permission) => c.Media.Documents.SingleOrDefault(d => d.DocumentId == id && CanDocument(c, actor, d, permission)) ?? throw new MediaNotFoundException();
    private LibraryDocumentVersion FindVersion(Context c, string actor, string id, string permission)
    { var v = c.Media.Versions.SingleOrDefault(v => v.VersionId == id) ?? throw new MediaNotFoundException(); FindDocument(c, actor, v.DocumentId, permission); return v; }
    private bool Effective(LibraryDocumentVersion v) => v.State == "PUBLISHED_DEMO" && v.EffectiveOn is { } day && day <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private bool DocumentVisible(Context c, string actor, LibraryDocument d) => CanDocument(c, actor, d, "DOCUMENTS_MANAGE") || c.Media.Versions.Any(v => v.DocumentId == d.DocumentId && Effective(v));
    private DocumentView ViewDocument(Context c, string actor, LibraryDocument d)
    {
        var manager = CanDocument(c, actor, d, "DOCUMENTS_MANAGE"); var versions = c.Media.Versions.Where(v => v.DocumentId == d.DocumentId && (manager || Effective(v))).OrderByDescending(v => v.CreatedAt).ToArray(); var effective = c.Media.Operations.Where(o => o.Action == "VERSION_PUBLISH" && o.VersionAfter is { } v && v.DocumentId == d.DocumentId && Effective(v)).LastOrDefault()?.VersionAfter;
        MediaFileView? File(string? id) => c.Media.Files.SingleOrDefault(f => f.FileId == id) is { } f ? View(f) : null;
        return new(d.DocumentId, d.Name, d.BusinessCode, d.CategoryId, c.Tree.ActiveNodes.Single(n => n.CategoryId == d.CategoryId).Name, d.Description, d.DataState, d.Version, effective?.VersionId, versions.Select(v => new DocumentVersionView(v.VersionId, v.Label, v.State, v.EffectiveOn, v.Version, File(v.DocxFileId), File(v.PdfFileId), Effective(v))).ToArray());
    }
    private MediaOperation? Replay(Context c, string actor, string key, string hash) { var op = c.Media.Operations.SingleOrDefault(o => o.Actor == actor && o.IdempotencyKey == key); if (op is not null && op.RequestHash != hash) throw new MediaConflictException("Idempotency key đã dùng cho yêu cầu khác."); return op; }
    private async Task Persist(Context c, string actor, string key, string hash, string action, string reason, ManagedFile[] files, LibraryDocument? document, LibraryDocumentVersion? version, CancellationToken ct, string? operationId = null)
    { var candidate = c.Media.Clone(); candidate.Apply(new(operationId ?? "MEDIAOP-" + Guid.NewGuid().ToString("N"), key, actor, hash, action, reason, clock.GetUtcNow(), files, document, version)); await store.CommitAsync(candidate, c.Media.Revision, ct); c.Media = candidate; }
    private static MediaFileView View(ManagedFile f) => new(f.FileId, f.OwnerType, f.OwnerId, f.Kind, f.FileName, f.MimeType, f.Size, f.State, f.IsDeleted, f.IsPrimary, f.Description, f.Version, f.UploadOperationId);
    private static string Text(string? value, int max, string label) { if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl)) throw new MediaValidationException(label + ": bắt buộc/tối đa" + max + " ký tự."); return value.Trim(); }
    private static string Optional(string? value, int max, string label) { value ??= ""; if (value.Length > max || value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw new MediaValidationException(label + " không hợp lệ."); return value.Trim(); }
    private static void Key(string key) { Text(key, 100, "Idempotency key"); if (key.StartsWith("SYS:", StringComparison.Ordinal)) throw new MediaValidationException("Key prefix dành cho operation nội bộ."); }
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    private sealed class Context(AuthSystemState auth, CategoryCatalogState tree, AssetCatalogState assetState, MediaCatalogState media, ScopeHierarchySet hierarchy)
    { public AuthSystemState Auth { get; } = auth; public CategoryCatalogState Tree { get; } = tree; public AssetCatalogState AssetState { get; } = assetState; public MediaCatalogState Media { get; set; } = media; public ScopeHierarchySet Hierarchy { get; } = hierarchy; }
}
