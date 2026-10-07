using DuyTien.AssetManagement.Application.ExternalStorage;

namespace DuyTien.AssetManagement.ArchitectureTests.Fakes;

internal sealed class FakeAssetDataStore : IAssetDataStore
{
    public Task<StorageReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new StorageReadiness(
            "Fake Google Sheets",
            ConfigurationPresent: true,
            LiveConnectionVerified: false,
            MissingSettings: []));
}

internal sealed class FakeAssetFileStore : IAssetFileStore
{
    public Task<StorageReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new StorageReadiness(
            "Fake Google Drive",
            ConfigurationPresent: true,
            LiveConnectionVerified: false,
            MissingSettings: []));

    public Task<StoredFile> UploadAsync(
        FileUploadRequest request,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Readiness fake does not implement uploads.");

    public Task<StoredFile?> FindByOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<StoredFile?>(null);

    public Task<byte[]> DownloadAsync(
        string fileId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Readiness fake does not implement downloads.");
}
