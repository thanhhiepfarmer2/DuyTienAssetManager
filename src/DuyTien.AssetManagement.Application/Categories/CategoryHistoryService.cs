namespace DuyTien.AssetManagement.Application.Categories;
public sealed partial class CategoryCatalogService
{
    public Task<IReadOnlyList<CategoryHistoryEntry>> HistoryAsync(string actor, string id, CancellationToken ct = default) => writer.ExecuteAsync<IReadOnlyList<CategoryHistoryEntry>>(async token =>
    {
        var auth = await authStore.ReadAsync(s => s.Clone(), token); RequirePermission(auth, actor, "CATEGORIES_READ");
        var state = await store.ReadAsync(token); var current = state.ActiveNodes;
        if (!current.Any(n => n.CategoryId == id && n.State == "DRAFT") || !Allowed(auth, actor, "CATEGORIES_READ", id, Hierarchies(current))) throw new CategoryNotFoundException();
        var result = new List<CategoryHistoryEntry>();
        foreach (var version in state.Versions)
        {
            var nodes = state.Categories.Where(n => n.TreeVersionId == version.TreeVersionId).ToArray(); var h = Hierarchies(nodes);
            var visible = nodes.Where(n => Allowed(auth, actor, "CATEGORIES_READ", n.CategoryId, h)).ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
            if (visible.TryGetValue(id, out var node)) { var v = View(node, visible); result.Add(new(version.TreeVersionId, v.Name, v.FullCode, v.Path, v.State, v.PathIsPartial)); }
        }
        return result;
    }, ct);
}
