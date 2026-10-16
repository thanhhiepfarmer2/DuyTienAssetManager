using DuyTien.AssetManagement.Application.Categories;
namespace DuyTien.AssetManagement.Application.Assets;

// Optional read capability: unfiltered source snapshots, scoped by the caller under its read lease.
public interface IAssetCategorySnapshotReader
{
    bool CanReadTogether(ICategoryCatalogStore categories);
    Task<(AssetCatalogState Assets, CategoryCatalogState Categories)> ReadAssetCategoriesAsync(CancellationToken ct = default);
}
