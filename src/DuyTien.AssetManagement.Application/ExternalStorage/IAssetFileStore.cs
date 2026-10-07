namespace DuyTien.AssetManagement.Application.ExternalStorage;

public interface IAssetFileStore
{
    Task<StorageReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken = default);

    Task<StoredFile> UploadAsync(
        FileUploadRequest request,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    Task<StoredFile?> FindByOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<byte[]> DownloadAsync(
        string fileId,
        CancellationToken cancellationToken = default);
}
