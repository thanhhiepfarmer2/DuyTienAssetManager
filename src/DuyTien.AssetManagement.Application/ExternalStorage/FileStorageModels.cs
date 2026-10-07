namespace DuyTien.AssetManagement.Application.ExternalStorage;

public sealed record FileUploadRequest(
    string OperationId,
    string FileName,
    string ContentType,
    string Sha256);

public sealed record StoredFile(
    string FileId,
    string FileName,
    string ContentType,
    long? Size,
    string? Md5Checksum,
    string OperationId,
    bool Reconciled);
