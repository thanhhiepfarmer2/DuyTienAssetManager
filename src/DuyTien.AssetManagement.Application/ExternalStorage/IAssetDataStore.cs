namespace DuyTien.AssetManagement.Application.ExternalStorage;

public interface IAssetDataStore
{
    Task<StorageReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken = default);
}
