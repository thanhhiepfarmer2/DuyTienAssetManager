using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
namespace DuyTien.AssetManagement.Application.Categories;

// Full read/proven-absence is the joint adapter's contract, never a client-supplied empty registry.
public static class TreeRegistryIntegrity
{
    public static void Validate(TreeMutationSnapshot snapshot)
    {
        try
        {
            if (snapshot.Inventory is { } inventory) InventoryIntegrity.Validate(inventory);
            if (snapshot.Media is { } media) MediaCatalogIntegrity.Validate(media);
            var nodes = snapshot.Categories.ActiveNodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
            var assets = snapshot.Assets.Assets.ToDictionary(a => a.AssetId, StringComparer.Ordinal);
            var items = (snapshot.Inventory?.Items ?? []).ToDictionary(i => i.ItemId, StringComparer.Ordinal);
            bool Category(string id, bool inactiveAllowed = false) => nodes.TryGetValue(id, out var n) && (n.State == "DRAFT" || inactiveAllowed && n.State == "DRAFT_INACTIVE");
            foreach (var item in items.Values)
                if (item.State != "QUARANTINED" && !Category(item.CategoryId)) Fail();
            foreach (var balance in snapshot.Inventory?.Balances ?? [])
                if (!items.TryGetValue(balance.ItemId, out var item) || !Category(item.CategoryId) ||
                    !snapshot.Assets.Locations.Any(l => l.Id == balance.LocationId && l.IsActive && snapshot.Assets.OrgUnits.Any(o => o.Id == l.OrgUnitId && o.IsActive))) Fail();
            if (snapshot.Media is { } files)
            {
                foreach (var document in files.Documents) if (!Category(document.CategoryId, document.IsDeleted)) Fail();
                foreach (var file in files.Files.Where(f => f.OwnerType == "ASSET"))
                    if (!assets.TryGetValue(file.OwnerId, out var owner) || !Category(owner.CategoryId, owner.IsDeleted) ||
                        !snapshot.Assets.Locations.Any(l => l.Id == owner.LocationId && l.OrgUnitId == owner.OrgUnitId) ||
                        !snapshot.Assets.OrgUnits.Any(o => o.Id == owner.OrgUnitId)) Fail();
            }
        }
        catch (Exception e) when (e is InventoryValidationException or MediaValidationException or ArgumentException or InvalidOperationException)
        { throw new ExternalStorageException("Registry/owner/reference chưa đối soát đầy đủ; không preview/ghi cây."); }
    }
    private static void Fail() => throw new ExternalStorageException("Registry/owner/reference mồ côi hoặc không hợp lệ; không ghi cây.");
}
