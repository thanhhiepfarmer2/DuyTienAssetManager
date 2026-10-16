using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;

namespace DuyTien.AssetManagement.Server.ConnectedCache;

// Internal dependencies, never returned as global revision counters to clients.
public static class BusinessReadProof
{
    private sealed class Frame { public readonly Dictionary<string, string> Values = new(); public bool Mixed; }
    private static readonly AsyncLocal<Frame?> Current = new();
    public static IDisposable Begin(bool read) { var old = Current.Value; Current.Value = read ? new() : null; return new Reset(() => Current.Value = old); }
    public static IReadOnlyDictionary<string, string> Values => Current.Value?.Values.ToDictionary(p => p.Key, p => p.Value) ?? new();
    public static bool Mixed => Current.Value?.Mixed == true;
    private static void Add(string key, string value)
    {
        if (Current.Value is not { } frame) return;
        if (frame.Values.TryGetValue(key, out var previous) && previous != value) frame.Mixed = true;
        frame.Values[key] = value;
    }
    public static void Track(CategoryCatalogState s) => Add("Categories", s.Revision + "|" + s.ActiveTreeVersionId);
    public static void Track(AssetCatalogState s) => Add("Assets", s.Revision + "|" + ProtectedQueryCache.Hash(JsonSerializer.Serialize(new { rules = s.Rules.OrderBy(x => x.RuleId), orgs = s.OrgUnits.OrderBy(x => x.Id), locations = s.Locations.OrderBy(x => x.Id) })));
    public static void Track(InventoryState s) => Add("Inventory", s.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static void Track(MediaCatalogState s) => Add("Media", s.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private sealed class Reset(Action reset) : IDisposable { public void Dispose() => reset(); }
}
public sealed class ObservedBusinessStores(IAssetCatalogStore assets, ICategoryCatalogStore categories, IInventoryStore inventory, IMediaCatalogStore media, ITreeMutationStore tree, IAssetCategorySnapshotReader? combined = null)
    : IAssetCategorySnapshotReader, IAssetCatalogStore, ICategoryCatalogStore, IInventoryStore, IMediaCatalogStore, ITreeMutationStore
{
    public bool CanReadTogether(ICategoryCatalogStore other) => ReferenceEquals(this, other);
    public async Task<(AssetCatalogState Assets, CategoryCatalogState Categories)> ReadAssetCategoriesAsync(CancellationToken ct = default)
    {
        var result = combined is null ? (await assets.ReadAsync(ct), await categories.ReadAsync(ct)) : await combined.ReadAssetCategoriesAsync(ct);
        BusinessReadProof.Track(result.Item1); BusinessReadProof.Track(result.Item2); return result;
    }
    async Task<AssetCatalogState> IAssetCatalogStore.ReadAsync(CancellationToken ct) { var s = await assets.ReadAsync(ct); BusinessReadProof.Track(s); return s; }
    async Task<CategoryCatalogState> ICategoryCatalogStore.ReadAsync(CancellationToken ct) { var s = await categories.ReadAsync(ct); BusinessReadProof.Track(s); return s; }
    async Task<InventoryState> IInventoryStore.ReadAsync(CancellationToken ct) { var s = await inventory.ReadAsync(ct); BusinessReadProof.Track(s); return s; }
    async Task<MediaCatalogState> IMediaCatalogStore.ReadAsync(CancellationToken ct) { var s = await media.ReadAsync(ct); BusinessReadProof.Track(s); return s; }
    async Task<TreeMutationSnapshot> ITreeMutationStore.ReadAsync(CancellationToken ct) { var s = await tree.ReadAsync(ct); BusinessReadProof.Track(s.Categories); BusinessReadProof.Track(s.Assets); if (s.Inventory is not null) BusinessReadProof.Track(s.Inventory); if (s.Media is not null) BusinessReadProof.Track(s.Media); return s; }
    Task IAssetCatalogStore.CommitAsync(AssetCatalogState s, long expected, CancellationToken ct) => assets.CommitAsync(s, expected, ct);
    Task ICategoryCatalogStore.CommitAsync(CategoryCatalogState s, long expected, CancellationToken ct) => categories.CommitAsync(s, expected, ct);
    Task IInventoryStore.CommitAsync(InventoryState s, long expected, CancellationToken ct) => inventory.CommitAsync(s, expected, ct);
    Task IMediaCatalogStore.CommitAsync(MediaCatalogState s, long expected, CancellationToken ct) => media.CommitAsync(s, expected, ct);
    Task ITreeMutationStore.CommitAsync(TreeMutationSnapshot s, long treeExpected, long assetExpected, CancellationToken ct) => tree.CommitAsync(s, treeExpected, assetExpected, ct);
}
