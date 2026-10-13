using System.Text.Json;
namespace DuyTien.AssetManagement.Application.Media;

public static class MediaCatalogIntegrity
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Actions = ["DOCUMENT_CREATE", "DOCUMENT_UPDATE", "DOCUMENT_ARCHIVE", "DOCUMENT_RELOCATE", "VERSION_CREATE", "VERSION_PUBLISH", "UPLOAD_PENDING", "UPLOAD_AVAILABLE", "UPLOAD_FAILED", "UPLOAD_CANCEL", "PHOTO_UPDATE", "PHOTO_DELETE"];
    public static void Validate(MediaCatalogState s)
    {
        if (s.Revision != s.Operations.Count || s.Operations.Any(o => o is null) || s.Operations.Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count() != s.Operations.Count || s.Operations.Select(o => o.Actor + "\0" + o.IdempotencyKey).Distinct(StringComparer.Ordinal).Count() != s.Operations.Count) Bad();
        var rebuilt = new MediaCatalogState();
        foreach (var o in s.Operations)
        {
            if (string.IsNullOrWhiteSpace(o.OperationId) || string.IsNullOrWhiteSpace(o.IdempotencyKey) || string.IsNullOrWhiteSpace(o.Actor) || !Digest(o.RequestHash) || !Actions.Contains(o.Action) || o.FilesAfter is null || o.FilesAfter.Length > 2 || o.Reason is null || o.Reason.Length > 500 || o.FilesAfter.Length == 0 && o.DocumentAfter is null && o.VersionAfter is null) Bad();
            if (o.Action == "DOCUMENT_RELOCATE" && (o.DocumentAfter is null || o.VersionAfter is not null || o.FilesAfter.Length != 0)) Bad();
            foreach (var f in o.FilesAfter)
            {
                if (f is null || string.IsNullOrWhiteSpace(f.FileId) || string.IsNullOrWhiteSpace(f.OwnerId) || string.IsNullOrWhiteSpace(f.DriveFileId) || f.Size is < 1 or > 25 * 1024 * 1024 || !Digest(f.Sha256) || string.IsNullOrWhiteSpace(f.FileName) || f.FileName.Length > 180 || f.Description is null || f.Description.Length > 1000 || f.State is not ("PENDING" or "AVAILABLE" or "FAILED") || f.OwnerType == "ASSET" && f.Kind != "PHOTO" || f.OwnerType == "DOCUMENT_VERSION" && f.Kind is not ("DOCX" or "PDF") || f.OwnerType is not ("ASSET" or "DOCUMENT_VERSION") || f.OwnerType != "ASSET" && f.IsPrimary || f.Kind == "PHOTO" && f.MimeType is not ("image/png" or "image/jpeg") || f.Kind == "DOCX" && f.MimeType != MediaFileValidation.DocxMime || f.Kind == "PDF" && f.MimeType != "application/pdf") Bad();
                var old = rebuilt.Files.SingleOrDefault(x => x.FileId == f.FileId);
                if (old is null) { if (o.Action != "UPLOAD_PENDING" || f.Version != 1 || f.State != "PENDING" || f.IsDeleted || f.UploadOperationId != o.OperationId) Bad(); }
                else
                {
                    if (f.Version != old.Version + 1 || old.IsDeleted && !f.IsDeleted || old.State == "AVAILABLE" && f.State != "AVAILABLE" || old with { State = f.State, IsDeleted = f.IsDeleted, IsPrimary = f.IsPrimary, Description = f.Description, Version = f.Version, UpdatedAt = f.UpdatedAt, UpdatedBy = f.UpdatedBy } != f) Bad();
                    if (f.OwnerType == "DOCUMENT_VERSION" && rebuilt.Versions.Any(v => v.VersionId == f.OwnerId && v.State == "PUBLISHED_DEMO")) Bad();
                }
            }
            if (o.DocumentAfter is { } d)
            {
                if (string.IsNullOrWhiteSpace(d.DocumentId) || string.IsNullOrWhiteSpace(d.CategoryId) || string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > 300 || string.IsNullOrWhiteSpace(d.BusinessCode) || d.BusinessCode.Length > 100 || d.Description is null || d.Description.Length > 2000 || d.DataState != "DRAFT_DEMO") Bad(); var old = rebuilt.Documents.SingleOrDefault(x => x.DocumentId == d.DocumentId);
                if (old is null) { if (o.Action != "DOCUMENT_CREATE" || d.Version != 1 || d.IsDeleted || d.CurrentPublishedVersionId is not null) Bad(); }
                else if (o.Action == "DOCUMENT_RELOCATE" && (old.CategoryId == d.CategoryId || old with { CategoryId = d.CategoryId, Version = d.Version, UpdatedAt = d.UpdatedAt, UpdatedBy = d.UpdatedBy } != d)) Bad();
                if (old is not null && (d.Version != old.Version + 1 || old.CategoryId != d.CategoryId && o.Action != "DOCUMENT_RELOCATE" || old.CreatedAt != d.CreatedAt || old.CreatedBy != d.CreatedBy || old.IsDeleted && !d.IsDeleted)) Bad();
            }
            if (o.VersionAfter is { } v)
            {
                if (string.IsNullOrWhiteSpace(v.VersionId) || string.IsNullOrWhiteSpace(v.DocumentId) || string.IsNullOrWhiteSpace(v.Label) || v.Label.Length > 80 || v.State is not ("DRAFT" or "PUBLISHED_DEMO")) Bad(); var old = rebuilt.Versions.SingleOrDefault(x => x.VersionId == v.VersionId);
                if (old is null) { if (o.Action != "VERSION_CREATE" || v.Version != 1 || v.State != "DRAFT" || v.DocxFileId is not null || v.PdfFileId is not null) Bad(); }
                else if (old.State == "PUBLISHED_DEMO" || v.Version != old.Version + 1 || v.DocumentId != old.DocumentId || v.Label != old.Label || v.CreatedAt != old.CreatedAt || v.CreatedBy != old.CreatedBy) Bad();
            }
            rebuilt.Apply(o); References(rebuilt);
        }
        if (JsonSerializer.Serialize(rebuilt.Files.OrderBy(f => f.FileId), Json) != JsonSerializer.Serialize(s.Files.OrderBy(f => f.FileId), Json) || JsonSerializer.Serialize(rebuilt.Documents.OrderBy(d => d.DocumentId), Json) != JsonSerializer.Serialize(s.Documents.OrderBy(d => d.DocumentId), Json) || JsonSerializer.Serialize(rebuilt.Versions.OrderBy(v => v.VersionId), Json) != JsonSerializer.Serialize(s.Versions.OrderBy(v => v.VersionId), Json)) Bad();
    }
    private static void References(MediaCatalogState s)
    {
        if (s.Files.Select(f => f.DriveFileId).Distinct(StringComparer.Ordinal).Count() != s.Files.Count || s.Files.Where(f => f.OwnerType == "ASSET" && f.State == "AVAILABLE" && !f.IsDeleted && f.IsPrimary).GroupBy(f => f.OwnerId).Any(g => g.Count() > 1) || s.Versions.GroupBy(v => v.DocumentId + "\0" + v.Label).Any(g => g.Count() > 1)) Bad();
        foreach (var v in s.Versions)
        {
            if (!s.Documents.Any(d => d.DocumentId == v.DocumentId)) Bad();
            bool Slot(string? id, string kind) => id is null || s.Files.Any(f => f.FileId == id && f.OwnerType == "DOCUMENT_VERSION" && f.OwnerId == v.VersionId && f.Kind == kind);
            if (!Slot(v.DocxFileId, "DOCX") || !Slot(v.PdfFileId, "PDF")) Bad();
            if (v.State == "PUBLISHED_DEMO" && (v.EffectiveOn is null || v.PublishedAt is null || !s.Files.Any(f => f.FileId == v.DocxFileId && f.State == "AVAILABLE" && !f.IsDeleted) || !s.Files.Any(f => f.FileId == v.PdfFileId && f.State == "AVAILABLE" && !f.IsDeleted))) Bad();
        }
        foreach (var f in s.Files.Where(f => f.OwnerType == "DOCUMENT_VERSION")) if (!s.Versions.Any(v => v.VersionId == f.OwnerId)) Bad();
        foreach (var d in s.Documents) if (d.CurrentPublishedVersionId is { } id && !s.Versions.Any(v => v.VersionId == id && v.DocumentId == d.DocumentId && v.State == "PUBLISHED_DEMO")) Bad();
    }
    private static bool Digest(string? value) => value?.Length == 64 && value.All(char.IsAsciiHexDigit);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Bad() => throw new MediaValidationException("Registry/journal file hoặc cặp phiên bản không nhất quán; cần đối soát, không overwrite.");
}
