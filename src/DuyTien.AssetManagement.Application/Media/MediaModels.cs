using System.Text.Json.Serialization;
namespace DuyTien.AssetManagement.Application.Media;

public sealed class MediaUploadOptions
{
    public const string SectionName = "MediaUpload";
    public int MaxPhotoBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxDocumentBytes { get; init; } = 25 * 1024 * 1024;
    public void Validate() { if (MaxPhotoBytes is < 1 or > 25*1024*1024 || MaxDocumentBytes is < 1 or > 25*1024*1024) throw new MediaValidationException("Upload cap phải từ1byte tới25MiB cho local subset."); }
}
public sealed record ManagedFile(string FileId, string OwnerType, string OwnerId, string Kind,
    string FileName, string MimeType, long Size, string Sha256, string DriveFileId, string State,
    bool IsDeleted, bool IsPrimary, string Description, long Version, string UploadOperationId,
    DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset UpdatedAt, string UpdatedBy);
public sealed record LibraryDocument(string DocumentId, string Name, string BusinessCode, string CategoryId,
    string Description, string DataState, bool IsDeleted, long Version, string? CurrentPublishedVersionId,
    DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset UpdatedAt, string UpdatedBy);
public sealed record LibraryDocumentVersion(string VersionId, string DocumentId, string Label, string State,
    DateOnly? EffectiveOn, string? DocxFileId, string? PdfFileId, long Version, DateTimeOffset CreatedAt,
    string CreatedBy, DateTimeOffset? PublishedAt);
public sealed record MediaOperation(string OperationId, string IdempotencyKey, string Actor, string RequestHash,
    string Action, string Reason, DateTimeOffset CompletedAt, ManagedFile[] FilesAfter,
    LibraryDocument? DocumentAfter = null, LibraryDocumentVersion? VersionAfter = null);
public sealed class MediaCatalogState
{
    public long Revision { get; set; }
    public List<ManagedFile> Files { get; } = [];
    public List<LibraryDocument> Documents { get; } = [];
    public List<LibraryDocumentVersion> Versions { get; } = [];
    public List<MediaOperation> Operations { get; } = [];
    public MediaCatalogState Clone() { var s=new MediaCatalogState {Revision=Revision};s.Files.AddRange(Files);s.Documents.AddRange(Documents);s.Versions.AddRange(Versions);s.Operations.AddRange(Operations);return s; }
    public void Apply(MediaOperation operation)
    {
        foreach(var f in operation.FilesAfter){Files.RemoveAll(x=>x.FileId==f.FileId);Files.Add(f);}
        if(operation.DocumentAfter is {} d){Documents.RemoveAll(x=>x.DocumentId==d.DocumentId);Documents.Add(d);}
        if(operation.VersionAfter is {} v){Versions.RemoveAll(x=>x.VersionId==v.VersionId);Versions.Add(v);}
        Operations.Add(operation);Revision++;
    }
}
public interface IMediaCatalogStore
{
    Task<MediaCatalogState> ReadAsync(CancellationToken ct=default);
    Task CommitAsync(MediaCatalogState candidate,long expectedRevision,CancellationToken ct=default);
}
public interface IManagedMediaContentStore
{
    Task<string> AllocateIdAsync(CancellationToken ct=default);
    Task EnsureUploadedAsync(ManagedFile file,ReadOnlyMemory<byte> bytes,CancellationToken ct=default);
    Task<byte[]> DownloadVerifiedAsync(ManagedFile file,CancellationToken ct=default);
}
public sealed record MediaFileContent(string FileName,string MimeType,byte[] Bytes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentInput(string Name,string BusinessCode,string CategoryId,string Description="");
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentWriteRequest(DocumentInput Document,long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentVersionRequest(string Label,long ExpectedDocumentVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentPublishRequest(long ExpectedVersion,long ExpectedDocumentVersion,DateOnly EffectiveOn,string Reason,bool ConfirmDemo);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PhotoUpdateRequest(long ExpectedVersion,string Description,bool IsPrimary,string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MediaLifecycleRequest(long ExpectedVersion,string Reason);
public sealed record MediaFileView(string FileId,string OwnerType,string OwnerId,string Kind,string FileName,string MimeType,
    long Size,string State,bool IsDeleted,bool IsPrimary,string Description,long Version,string UploadOperationId);
public sealed record DocumentView(string DocumentId,string Name,string BusinessCode,string CategoryId,string CategoryName,
    string Description,string DataState,long Version,string? CurrentPublishedVersionId,IReadOnlyList<DocumentVersionView> Versions);
public sealed record DocumentVersionView(string VersionId,string Label,string State,DateOnly? EffectiveOn,long Version,
    MediaFileView? Docx,MediaFileView? Pdf,bool IsEffective);
public sealed record MediaMutationResult(string EntityId,long Version,bool Replayed=false);
public sealed record MediaUploadResult(MediaFileView File,bool Replayed=false);
public sealed record MediaDownload(byte[] Bytes,string MimeType,string FileName,string Kind);
public sealed class MediaValidationException(string message):Exception(message);
public sealed class MediaTooLargeException():Exception("File vượt giới hạn upload demo.");
public sealed class MediaConflictException(string message):Exception(message);
public sealed class MediaForbiddenException():Exception("Không có quyền thực hiện thao tác file/tài liệu.");
public sealed class MediaNotFoundException():Exception("Không tìm thấy file/tài liệu hoặc ngoài phạm vi.");
public sealed class MediaPendingException(string fileId):Exception("Upload chưa hoàn tất; giữ operation và dùng cùng yêu cầu để retry/đối soát, chưa báo lưu thành công.") {public string FileId{get;}=fileId;}
