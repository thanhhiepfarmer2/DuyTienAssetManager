using DuyTien.AssetManagement.Application.Authentication;
namespace DuyTien.AssetManagement.Application.Categories;

public sealed partial class CategoryCatalogService
{
    public Task<IReadOnlyList<CategoryHistoryEntry>> HistoryAsync(string actor, string id, CancellationToken ct = default) => writer.ReadAsync<IReadOnlyList<CategoryHistoryEntry>>(async token =>
    {
        var auth = await authStore.ReadAsync(s => s.Clone(), token); RequirePermission(auth, actor, "CATEGORIES_READ");
        var state = await store.ReadAsync(token); return HistoryFromSnapshot(auth, actor, id, state);
    }, ct);
    public Task<(CategoryView Category, IReadOnlyList<CategoryHistoryEntry> History)> DetailWithHistoryAsync(string actor, string id, CancellationToken ct = default) => writer.ReadAsync(async token =>
    {
        var auth = await authStore.ReadAsync(s => s.Clone(), token); RequirePermission(auth, actor, "CATEGORIES_READ");
        var state = await store.ReadAsync(token);
        var category = ListSnapshot(auth, actor, "CATEGORIES_READ", state, null).Nodes.SingleOrDefault(n => n.CategoryId == id) ?? throw new CategoryNotFoundException();
        return (category, HistoryFromSnapshot(auth, actor, id, state));
    }, ct);
    private IReadOnlyList<CategoryHistoryEntry> HistoryFromSnapshot(AuthSystemState auth, string actor, string id, CategoryCatalogState state)
    {
        var current = state.ActiveNodes;
        if (!current.Any(n => n.CategoryId == id && n.State == "DRAFT") || !Allowed(auth, actor, "CATEGORIES_READ", id, Hierarchies(current))) throw new CategoryNotFoundException();
        var result = new List<CategoryHistoryEntry>();
        foreach (var version in state.Versions)
        {
            var nodes = state.Categories.Where(n => n.TreeVersionId == version.TreeVersionId).ToArray(); var h = Hierarchies(nodes);
            var visible = nodes.Where(n => Allowed(auth, actor, "CATEGORIES_READ", n.CategoryId, h)).ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
            if (visible.TryGetValue(id, out var node)) { var v = View(node, visible); result.Add(new(version.TreeVersionId, v.Name, v.FullCode, v.Path, v.State, v.PathIsPartial)); }
        }
        return result;
    }
}
