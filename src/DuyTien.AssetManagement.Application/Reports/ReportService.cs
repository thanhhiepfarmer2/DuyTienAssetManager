using System.Globalization;
using System.Text;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.Inventory;
namespace DuyTien.AssetManagement.Application.Reports;

public sealed partial class ReportService(IAssetCatalogStore assetStore, ICategoryCatalogStore categoryStore,
    IInventoryStore inventoryStore, IAuthSystemStore authStore, ISingleWriterCoordinator writer, TimeProvider clock)
{
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    { ["R01"] = "Toàn bộ tài sản trong phạm vi", ["R02"] = "Tài sản theo danh mục và cây con", ["R03"] = "Vật tư hết số lượng", ["R04"] = "Vật tư sắp hết" };
    public static readonly string[] Sections = ["MATCHES", "UNKNOWN", "MISSING_MINIMUM", "NOT_TRACKED", "TRACKING_UNCONFIRMED", "SOURCE_MISSING", "INVALID_SNAPSHOT"];
    private static readonly string[] Warnings = ["DRAFT/demo và cây DRAFT; nguồn/rule/đơn vị/địa điểm/ngưỡng chưa được nghiệm thu. Không coi là dữ liệu công ty đã phê duyệt.", "Báo cáo chỉ dữ liệu đã commit trong phạm vi; không chứng nhận nguồn Items đã nhập đầy đủ hoặc dữ liệu ngoài quyền.", "asOf là timestamp snapshot hiện có, không sổ giao dịch hoặc báo cáo lịch sử. Không tự đặt ngưỡng stale hoặc minimum."];
    private readonly ScopeAuthorizationService authorization = new();
    private sealed class Context(AuthSystemState auth, AssetCatalogState assets, CategoryCatalogState tree,
        InventoryState inventory, ScopeHierarchySet hierarchies, string actor, string permission, DateTimeOffset now)
    {
        public AuthSystemState Auth { get; } = auth; public AssetCatalogState Assets { get; } = assets; public CategoryCatalogState Tree { get; } = tree;
        public InventoryState Inventory { get; } = inventory; public ScopeHierarchySet Hierarchies { get; } = hierarchies;
        public string Actor { get; } = actor; public string Permission { get; } = permission; public DateTimeOffset Now { get; } = now;
        public Dictionary<string, CategoryNode> Nodes { get; } = tree.ActiveNodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
        public Dictionary<string, AssetOrgUnit> Orgs { get; } = assets.OrgUnits.ToDictionary(o => o.Id, StringComparer.Ordinal);
        public Dictionary<string, AssetLocation> Locations { get; } = assets.Locations.ToDictionary(l => l.Id, StringComparer.Ordinal);
        public Dictionary<string, AuthSystemState> AllowedAuth { get; } = new(StringComparer.Ordinal);
    }
    public Task<ReportResult> ReadAsync(string actor, string code, ReportQuery query, CancellationToken ct = default) =>
        writer.ExecuteAsync(t => Build(actor, code, query, false, t), ct);
    private async Task<ReportResult> Build(string actor, string code, ReportQuery query, bool export, CancellationToken ct)
    {
        Validate(code, query); var permission = export ? "REPORTS_EXPORT" : "REPORTS_READ";
        var now = clock.GetUtcNow(); var auth = await authStore.ReadAsync(s => s.Clone(), ct); Require(auth, actor, permission, now);
        var tree = await categoryStore.ReadAsync(ct); var assets = await assetStore.ReadAsync(ct);
        var inventory = code is "R03" or "R04" ? await inventoryStore.ReadAsync(ct) : new InventoryState();
        Context c;
        try
        {
            var h = new ScopeHierarchySet(ScopeHierarchy.Create(AuthScopeType.OrgUnit, assets.OrgUnits.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.Location, assets.Locations.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.CategorySubtree, tree.ActiveNodes.Select(n => new ScopeNode(n.CategoryId, n.ParentId))));
            c = new(auth, assets, tree, inventory, h, actor, permission, now);
        }
        catch (Exception e) when (e is AuthValidationException or ArgumentException) { throw new ExternalStorageException("Nguồn cây/đơn vị/địa điểm báo cáo cần đối soát."); }
        ValidateReferences(c, code, query);
        return code is "R01" or "R02" ? Assets(c, code, query, export) : Stock(c, code, query, export);
    }
    private static void Require(AuthSystemState a, string actor, string permission, DateTimeOffset now)
    { if (!a.Users.Any(u => u.UserId == actor && u.State == AuthUserState.Active && !u.MustChangePassword) || !a.RoleAssignments.Any(r => r.UserId == actor && AuthAuthorizationRules.IsEffective(r, now) && AuthRoleCatalog.HasPermission(r.RoleCode, permission))) throw new ReportForbiddenException(); }
    private bool Allowed(Context c, string dataPermissions, ResourceScope scope)
    {
        if (!c.AllowedAuth.TryGetValue(dataPermissions, out var a))
        { a = c.Auth.Clone(); var permissions = dataPermissions.Split('|'); a.RoleAssignments.RemoveAll(r => !AuthRoleCatalog.HasPermission(r.RoleCode, c.Permission) || permissions.Any(p => !AuthRoleCatalog.HasPermission(r.RoleCode, p))); c.AllowedAuth[dataPermissions] = a; }
        return authorization.IsAllowed(a, c.Actor, c.Permission, scope, c.Hierarchies, c.Now);
    }
    private static ResourceScope Cat(string id) => new(new(false, null), new(false, null), new(true, id));
    private bool ActiveCategory(Context c, string id) => c.Nodes.TryGetValue(id, out var n) && n.State == "DRAFT";
    private bool VisibleCategory(Context c, string id) => ActiveCategory(c, id) && Allowed(c, "CATEGORIES_READ", Cat(id));
    private bool ValidLocation(Context c, string org, string loc) => c.Orgs.TryGetValue(org, out var o) && o.IsActive && c.Locations.TryGetValue(loc, out var l) && l.IsActive && l.OrgUnitId == org;
    private void ValidateReferences(Context c, string code, ReportQuery q)
    {
        var data = code is "R01" or "R02" ? "ASSETS_READ" : "STOCK_READ|ITEMS_READ";
        if (q.CategoryId is not null && !VisibleCategory(c, q.CategoryId)) throw new ReportNotFoundException();
        if (q.OrgUnitId is not null && (!c.Orgs.TryGetValue(q.OrgUnitId, out var o) || !o.IsActive || !Allowed(c, data, new(new(true, o.Id), new(false, null), new(false, null))))) throw new ReportNotFoundException();
        if (q.LocationId is not null && (!c.Locations.TryGetValue(q.LocationId, out var l) || !ValidLocation(c, l.OrgUnitId, l.Id) || !Allowed(c, data, new(new(true, l.OrgUnitId), new(true, l.Id), new(false, null))))) throw new ReportNotFoundException();
        if (q.OrgUnitId is not null && q.LocationId is not null && c.Locations[q.LocationId].OrgUnitId != q.OrgUnitId) throw new ReportValidationException("Địa điểm không thuộc đơn vị đã chọn.");
    }
    private static void Validate(string code, ReportQuery q)
    {
        if (string.IsNullOrWhiteSpace(code) || !Names.ContainsKey(code)) throw new ReportNotFoundException();
        if (q is null || q.Page is < 1 or > 100000 || q.PageSize is < 1 or > 100 || q.Q?.Length > 200 || new[] { q.CategoryId, q.OrgUnitId, q.LocationId, q.Type, q.Condition, q.Unit }.Any(v => v is not null && (string.IsNullOrWhiteSpace(v) || v.Length > 200 || v.Any(char.IsControl)))) throw new ReportValidationException("Bộ lọc không hợp lệ; page>=1/pageSize1–100/từ khóa<=200.");
        if (q.GroupLevel is < 1 or > 10 || !Sections.Contains(q.Section, StringComparer.Ordinal) || q.AsOfFrom?.Offset != null && q.AsOfFrom.Value.Offset != TimeSpan.Zero || q.AsOfTo?.Offset != null && q.AsOfTo.Value.Offset != TimeSpan.Zero || q.AsOfFrom > q.AsOfTo) throw new ReportValidationException("CấpF1–10; phân đoạn hợp lệ; khoảng asOf UTC phải đúng thứ tự.");
        if (code is "R01" or "R02")
        { if (q.Unit is not null || q.AsOfFrom is not null || q.AsOfTo is not null || q.Section != "MATCHES" || code == "R01" && q.GroupLevel is not null) throw new ReportValidationException("Bộ lọc không áp dụng cho báo cáo tài sản này."); }
        else if (q.Type is not null || q.Condition is not null || q.GroupLevel is not null) throw new ReportValidationException("Bộ lọc loại/tình trạng/cấpF chỉ áp dụng báo cáo tài sản.");
    }
    private static bool CategoryMatches(Context c, ReportQuery q, string id) => q.CategoryId is null || c.Hierarchies.Categories.IsWithin(q.CategoryId, id, q.IncludeDescendants);
    private static bool TextMatches(string? q, params string?[] fields) => q is null || Normalize(string.Join(' ', fields)).Contains(Normalize(q), StringComparison.Ordinal);
    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD).Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)).Replace('đ', 'd').Replace('Đ', 'D').ToUpperInvariant();
    private (string Path, bool Partial, int Depth, string? Parent) Path(Context c, CategoryNode n)
    {
        var names = new List<string> { n.Name }; var parent = n.ParentId; var visibleParent = parent is not null && VisibleCategory(c, parent) ? parent : null;
        while (parent is not null && VisibleCategory(c, parent)) { var p = c.Nodes[parent]; names.Insert(0, p.Name); parent = p.ParentId; }
        return (string.Join(" / ", names), parent is not null, names.Count - 1, visibleParent);
    }
    private ReportChoices Choices(Context c, string code, IReadOnlyList<string>? units = null)
    {
        var data = code is "R01" or "R02" ? "ASSETS_READ" : "STOCK_READ|ITEMS_READ";
        var eligibleAssets = c.Assets.Assets.Where(a => !a.IsDeleted && ActiveCategory(c, a.CategoryId) && ValidLocation(c, a.OrgUnitId, a.LocationId) && Allowed(c, "ASSETS_READ", ResourceScope.Required(a.OrgUnitId, a.LocationId, a.CategoryId))).ToArray();
        return new(c.Nodes.Values.Where(n => VisibleCategory(c, n.CategoryId)).OrderBy(n => n.Name, StringComparer.Ordinal).Select(n => new ReportChoice(n.CategoryId, n.Name + " [DRAFT]")).ToArray(),
            c.Orgs.Values.Where(o => o.IsActive && Allowed(c, data, new(new(true, o.Id), new(false, null), new(false, null)))).Select(o => new ReportChoice(o.Id, o.Name)).OrderBy(o => o.Name, StringComparer.Ordinal).ToArray(),
            c.Locations.Values.Where(l => ValidLocation(c, l.OrgUnitId, l.Id) && Allowed(c, data, new(new(true, l.OrgUnitId), new(true, l.Id), new(false, null)))).Select(l => new ReportChoice(l.Id, l.Name)).OrderBy(l => l.Name, StringComparer.Ordinal).ToArray(),
            code is "R01" or "R02" ? eligibleAssets.Select(a => a.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : [],
            code is "R01" or "R02" ? eligibleAssets.Select(a => a.Condition).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : [], units ?? []);
    }
    private static bool CanExport(Context c) => c.Auth.RoleAssignments.Any(r => r.UserId == c.Actor && AuthAuthorizationRules.IsEffective(r, c.Now) && AuthRoleCatalog.HasPermission(r.RoleCode, "REPORTS_EXPORT"));
    private ReportResult Result(Context c, string code, ReportQuery q, ReportSummary summary, AssetReportRow[] assets, CategoryReportRow[] cats, StockReportRow[] stock, ReportChoices choices, int total) =>
        new(code, Names[code], q, c.Now, c.Tree.ActiveTreeVersionId, "DRAFT_DEMO", Warnings, summary, assets, cats, stock, choices, total, q.Page, q.PageSize, CanExport(c));
}
