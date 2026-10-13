using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
namespace DuyTien.AssetManagement.Application.Reports;

public sealed partial class ReportService
{
    private ReportResult Assets(Context c, string code, ReportQuery q, bool export)
    {
        var allowed = c.Assets.Assets.Where(a => !a.IsDeleted && ActiveCategory(c, a.CategoryId) && ValidLocation(c, a.OrgUnitId, a.LocationId) && Allowed(c, "ASSETS_READ", ResourceScope.Required(a.OrgUnitId, a.LocationId, a.CategoryId)));
        var rows = allowed.Where(a => CategoryMatches(c, q, a.CategoryId) && (q.OrgUnitId is null || a.OrgUnitId == q.OrgUnitId) && (q.LocationId is null || a.LocationId == q.LocationId) && (q.Type is null || a.Type == q.Type) && (q.Condition is null || a.Condition == q.Condition))
            .Where(a => TextMatches(q.Q, a.ObjectCode, a.Name, a.Serial, a.Model, c.Nodes[a.CategoryId].Name, Path(c, c.Nodes[a.CategoryId]).Path)).OrderBy(a => a.ObjectCode, StringComparer.Ordinal).ThenBy(a => a.AssetId, StringComparer.Ordinal).ToArray();
        var summary = new ReportSummary(rows.Length, null, null, [], [], null, null);
        if (code == "R01")
        {
            var page = export ? rows : rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize);
            var result = page.Select(a => { var n = c.Nodes[a.CategoryId]; var p = Path(c, n); return new AssetReportRow(a.AssetId, a.ObjectCode, a.Name, a.Type, a.Condition, a.CategoryId, n.Name, p.Path, p.Partial, a.OrgUnitId, c.Orgs[a.OrgUnitId].Name, a.LocationId, c.Locations[a.LocationId].Name, a.DataState, a.UpdatedAt); }).ToArray();
            return Result(c, code, q, summary, result, [], [], Choices(c, code), rows.Length);
        }
        var direct = rows.GroupBy(a => a.CategoryId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var subtree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var a in rows)
        {
            var id = a.CategoryId;
            while (id is not null) { if (VisibleCategory(c, id)) subtree[id] = subtree.GetValueOrDefault(id) + 1; id = c.Nodes[id].ParentId; }
        }
        var categories = Ordered(c).Where(n => VisibleCategory(c, n.CategoryId) && CategoryMatches(c, q, n.CategoryId) && (q.GroupLevel is null || n.FLevel == q.GroupLevel))
            .Select(n => { var p = Path(c, n); return new CategoryReportRow(n.CategoryId, p.Parent, n.Name, p.Path, p.Partial, n.FLevel, p.Depth, direct.GetValueOrDefault(n.CategoryId), subtree.GetValueOrDefault(n.CategoryId), n.State); })
            .Where(n => q.Q is null || n.SubtreeAssetCount > 0 || TextMatches(q.Q, n.Name, n.VisiblePath)).ToArray();
        return Result(c, code, q, summary, [], export ? categories : categories.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArray(), [], Choices(c, code), categories.Length);
    }
    private static IEnumerable<CategoryNode> Ordered(Context c)
    {
        var children = c.Nodes.Values.ToLookup(n => n.ParentId ?? "", StringComparer.Ordinal);
        foreach (var n in Walk("")) yield return n;
        IEnumerable<CategoryNode> Walk(string parent)
        { foreach (var n in children[parent].OrderBy(n => n.DisplayOrder).ThenBy(n => n.CategoryId, StringComparer.Ordinal)) { yield return n; foreach (var child in Walk(n.CategoryId)) yield return child; } }
    }
}
