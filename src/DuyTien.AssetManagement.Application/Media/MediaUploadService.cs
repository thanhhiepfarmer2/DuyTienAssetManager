using System.Security.Cryptography;
using DuyTien.AssetManagement.Application.ExternalStorage;
namespace DuyTien.AssetManagement.Application.Media;

public sealed partial class MediaLibraryService
{
    public Task<MediaUploadResult> UploadPhotoAsync(string actor, string assetId, MediaFileContent input, long expectedAssetVersion, string description, bool isPrimary, string key, CancellationToken ct = default) => Upload(actor, "ASSET", assetId, "PHOTO", input, expectedAssetVersion, description, isPrimary, key, ct);
    public Task<MediaUploadResult> UploadDocumentFileAsync(string actor, string versionId, string kind, MediaFileContent input, long expectedVersion, string key, CancellationToken ct = default) => Upload(actor, "DOCUMENT_VERSION", versionId, kind, input, expectedVersion, "", false, key, ct);
    private Task<MediaUploadResult> Upload(string actor, string ownerType, string ownerId, string kind, MediaFileContent input, long expectedVersion, string description, bool primary, string key, CancellationToken ct) => writer.ExecuteAsync(async token =>
    {
        Key(key); if (ownerType == "DOCUMENT_VERSION" && kind is not ("DOCX" or "PDF")) throw new MediaValidationException("Slot phải là DOCX hoặc PDF."); var mime = MediaFileValidation.Validate(kind, input, limits); description = Optional(description, 1000, "Mô tả ảnh"); var checksum = Convert.ToHexString(SHA256.HashData(input.Bytes));
        var c = await Load(actor, ownerType == "ASSET" ? "ASSET_FILES_UPLOAD" : "DOCUMENTS_MANAGE", ownerType == "ASSET", token); LibraryDocumentVersion? version = null; long currentVersion;
        if (ownerType == "ASSET") currentVersion = RequireAsset(c, actor, ownerId, "ASSET_FILES_UPLOAD").Version;
        else { version = FindVersion(c, actor, ownerId, "DOCUMENTS_MANAGE"); currentVersion = version.Version; }
        var hash = Hash(new { actor, ownerType, ownerId, kind, input.FileName, Mime = mime, Size = input.Bytes.Length, checksum, expectedVersion, description, primary }); var replay = Replay(c, actor, key, hash); ManagedFile file;
        if (replay is not null)
        { var id = replay.FilesAfter.Single().FileId; file = c.Media.Files.Single(f => f.FileId == id); if (file.IsDeleted) throw new MediaConflictException("Upload đã hủy/ngừng dùng; không tự khôi phục file."); if (file.State == "AVAILABLE") return new MediaUploadResult(View(file), true); if (version?.State == "PUBLISHED_DEMO") throw new MediaConflictException("Không thay file bản đã phát hành."); }
        else
        {
            if (currentVersion != expectedVersion) throw new MediaConflictException("Owner/version đã đổi, tải lại trước upload.");
            if (version is not null) { if (version.State != "DRAFT") throw new MediaConflictException("Bản đã phát hành immutable, tạo version mới."); var slot = kind == "DOCX" ? version.DocxFileId : version.PdfFileId; if (slot is not null && c.Media.Files.Any(f => f.FileId == slot && !f.IsDeleted)) throw new MediaConflictException("Slot đã có file/operation, không ghi đè; retry cùng key hoặc tạo version mới."); }
            var now = clock.GetUtcNow(); var operationId = "UPLOAD-" + Guid.NewGuid().ToString("N"); var driveId = await content.AllocateIdAsync(token);
            file = new("FILE-" + Guid.NewGuid().ToString("N"), ownerType, ownerId, kind, input.FileName, mime, input.Bytes.Length, checksum, driveId, "PENDING", false, primary, description, 1, operationId, now, actor, now, actor);
            var after = version is null ? null : kind == "DOCX" ? version with { DocxFileId = file.FileId, Version = version.Version + 1 } : version with { PdfFileId = file.FileId, Version = version.Version + 1 };
            await Persist(c, actor, key, hash, "UPLOAD_PENDING", "Đăng ký upload trước Drive", [file], null, after, token, operationId);
        }
        try { await content.EnsureUploadedAsync(file, input.Bytes, token); }
        catch (ExternalStorageException)
        {
            var failed = file with { State = "FAILED", Version = file.Version + 1, UpdatedAt = clock.GetUtcNow(), UpdatedBy = actor };
            try { await Persist(c, actor, "SYS:" + file.UploadOperationId + ":FAILED:" + file.Version, hash, "UPLOAD_FAILED", "Drive chưa xác nhận, giữ ID để đối soát", [failed], null, null, token); }
            catch (ExternalStorageException) { throw new MediaPendingException(file.FileId); }
            throw new MediaPendingException(file.FileId);
        }
        try { return new MediaUploadResult(View(await Complete(c, actor, file, hash, token)), replay is not null); }
        catch (ExternalStorageException) { throw new MediaPendingException(file.FileId); }
    }, ct);
    public Task<MediaUploadResult> ReconcileAsync(string actor, string fileId, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        var snapshot = await store.ReadAsync(token); var file = snapshot.Files.SingleOrDefault(f => f.FileId == fileId && !f.IsDeleted) ?? throw new MediaNotFoundException(); var c = await Load(actor, file.OwnerType == "ASSET" ? "ASSET_FILES_UPLOAD" : "DOCUMENTS_MANAGE", file.OwnerType == "ASSET", token); RequireFileOwner(c, actor, file, true); file = c.Media.Files.Single(f => f.FileId == fileId && !f.IsDeleted);
        if (file.State == "AVAILABLE") return new MediaUploadResult(View(file), true);
        try { _ = await content.DownloadVerifiedAsync(file, token); return new MediaUploadResult(View(await Complete(c, actor, file, Hash(new { fileId, Action = "RECONCILE" }), token))); }
        catch (ExternalStorageException) { throw new MediaPendingException(file.FileId); }
    }, ct);
    private async Task<ManagedFile> Complete(Context c, string actor, ManagedFile file, string hash, CancellationToken ct)
    {
        var saved = file with { State = "AVAILABLE", Version = file.Version + 1, UpdatedAt = clock.GetUtcNow(), UpdatedBy = actor }; var changed = new List<ManagedFile> { saved };
        if (saved.OwnerType == "ASSET" && saved.IsPrimary) changed.AddRange(c.Media.Files.Where(f => f.FileId != saved.FileId && f.OwnerType == "ASSET" && f.OwnerId == saved.OwnerId && f.State == "AVAILABLE" && f.IsPrimary && !f.IsDeleted).Select(f => f with { IsPrimary = false, Version = f.Version + 1, UpdatedAt = clock.GetUtcNow(), UpdatedBy = actor }));
        await Persist(c, actor, "SYS:" + file.UploadOperationId + ":AVAILABLE", hash, "UPLOAD_AVAILABLE", "Drive xác minh; commit metadata AVAILABLE", changed.ToArray(), null, null, ct); return saved;
    }
    public Task<MediaDownload> DownloadAsync(string actor, string fileId, CancellationToken ct = default) => writer.ReadAsync(async token =>
    {
        var snapshot = await store.ReadAsync(token); var file = snapshot.Files.SingleOrDefault(f => f.FileId == fileId && !f.IsDeleted) ?? throw new MediaNotFoundException(); var c = await Load(actor, file.OwnerType == "ASSET" ? "ASSET_FILES_DOWNLOAD" : "DOCUMENTS_DOWNLOAD", file.OwnerType == "ASSET", token); file = c.Media.Files.Single(f => f.FileId == fileId && !f.IsDeleted); RequireFileOwner(c, actor, file, false);
        if (file.State != "AVAILABLE") throw new MediaConflictException("File chưa AVAILABLE; chưa tải được, cần đối soát."); var data = await content.DownloadVerifiedAsync(file, token); return new MediaDownload(data, file.MimeType, file.FileName, file.Kind);
    }, ct);
    public Task<MediaUploadResult> UpdatePhotoAsync(string actor, string fileId, PhotoUpdateRequest input, string key, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        Key(key); if (input is null) throw new MediaValidationException("Thiếu yêu cầu."); Text(input.Reason, 500, "Lý do"); var description = Optional(input.Description, 1000, "Mô tả ảnh"); var c = await Load(actor, "ASSET_FILES_UPLOAD", true, token); var file = c.Media.Files.SingleOrDefault(f => f.FileId == fileId && f.OwnerType == "ASSET" && !f.IsDeleted) ?? throw new MediaNotFoundException(); RequireAsset(c, actor, file.OwnerId, "ASSET_FILES_UPLOAD"); var hash = Hash(new { actor, fileId, input }); var replay = Replay(c, actor, key, hash); if (replay is not null) return new MediaUploadResult(View(file), true);
        if (file.Version != input.ExpectedVersion || file.State != "AVAILABLE") throw new MediaConflictException("Ảnh chưa AVAILABLE hoặc version đã đổi."); var now = clock.GetUtcNow(); var saved = file with { Description = description, IsPrimary = input.IsPrimary, Version = file.Version + 1, UpdatedAt = now, UpdatedBy = actor }; var changes = new List<ManagedFile> { saved };
        if (input.IsPrimary) changes.AddRange(c.Media.Files.Where(f => f.FileId != fileId && f.OwnerType == "ASSET" && f.OwnerId == file.OwnerId && f.IsPrimary && f.State == "AVAILABLE" && !f.IsDeleted).Select(f => f with { IsPrimary = false, Version = f.Version + 1, UpdatedAt = now, UpdatedBy = actor }));
        await Persist(c, actor, key, hash, "PHOTO_UPDATE", input.Reason, changes.ToArray(), null, null, token); return new MediaUploadResult(View(saved));
    }, ct);
    public Task<MediaUploadResult> DeletePhotoAsync(string actor, string fileId, MediaLifecycleRequest input, string key, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        Key(key); if (input is null) throw new MediaValidationException("Thiếu yêu cầu."); Text(input.Reason, 500, "Lý do"); var c = await Load(actor, "ASSET_FILES_DELETE", true, token); var file = c.Media.Files.SingleOrDefault(f => f.FileId == fileId && f.OwnerType == "ASSET") ?? throw new MediaNotFoundException(); RequireAsset(c, actor, file.OwnerId, "ASSET_FILES_DELETE"); var hash = Hash(new { actor, fileId, input, Action = "PHOTO_DELETE" }); var replay = Replay(c, actor, key, hash); if (replay is not null) return new MediaUploadResult(View(file), true);
        if (file.IsDeleted || file.Version != input.ExpectedVersion) throw new MediaConflictException("Ảnh đã ngừng dùng hoặc version đổi."); var saved = file with { IsDeleted = true, IsPrimary = false, Version = file.Version + 1, UpdatedAt = clock.GetUtcNow(), UpdatedBy = actor }; await Persist(c, actor, key, hash, "PHOTO_DELETE", input.Reason, [saved], null, null, token); return new MediaUploadResult(View(saved));
    }, ct);
    public Task<MediaUploadResult> CancelUploadAsync(string actor, string fileId, MediaLifecycleRequest input, string key, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        Key(key); if (input is null) throw new MediaValidationException("Thiếu yêu cầu."); Text(input.Reason, 500, "Lý do"); var snapshot = await store.ReadAsync(token); var file = snapshot.Files.SingleOrDefault(f => f.FileId == fileId) ?? throw new MediaNotFoundException(); var c = await Load(actor, file.OwnerType == "ASSET" ? "ASSET_FILES_UPLOAD" : "DOCUMENTS_MANAGE", file.OwnerType == "ASSET", token); RequireFileOwner(c, actor, file, true); file = c.Media.Files.Single(f => f.FileId == fileId); var hash = Hash(new { actor, fileId, input, Action = "CANCEL_UPLOAD" }); if (Replay(c, actor, key, hash) is not null) return new MediaUploadResult(View(file), true);
        if (file.Version != input.ExpectedVersion || file.IsDeleted || file.State == "AVAILABLE") throw new MediaConflictException("Chỉ hủy operation PENDING/FAILED chưa AVAILABLE."); var saved = file with { IsDeleted = true, IsPrimary = false, Version = file.Version + 1, UpdatedAt = clock.GetUtcNow(), UpdatedBy = actor }; await Persist(c, actor, key, hash, "UPLOAD_CANCEL", input.Reason, [saved], null, null, token); return new MediaUploadResult(View(saved));
    }, ct);
    private void RequireFileOwner(Context c, string actor, ManagedFile f, bool manage)
    {
        if (f.OwnerType == "ASSET") { RequireAsset(c, actor, f.OwnerId, manage ? "ASSET_FILES_UPLOAD" : "ASSET_FILES_DOWNLOAD"); return; }
        if (f.OwnerType != "DOCUMENT_VERSION") throw new MediaNotFoundException(); var v = FindVersion(c, actor, f.OwnerId, manage ? "DOCUMENTS_MANAGE" : "DOCUMENTS_DOWNLOAD"); var d = FindDocument(c, actor, v.DocumentId, manage ? "DOCUMENTS_MANAGE" : "DOCUMENTS_DOWNLOAD"); var manager = CanDocument(c, actor, d, "DOCUMENTS_MANAGE");
        if (manage && v.State != "DRAFT" || !manage && !manager && !Effective(v) || f.FileId != (f.Kind == "DOCX" ? v.DocxFileId : v.PdfFileId)) throw new MediaNotFoundException();
    }
}
