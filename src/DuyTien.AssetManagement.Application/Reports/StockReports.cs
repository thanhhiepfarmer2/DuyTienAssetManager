using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Inventory;
namespace DuyTien.AssetManagement.Application.Reports;

public sealed partial class ReportService
{
    private ReportResult Stock(Context c, string code, ReportQuery q, bool export)
    {
        var items = c.Inventory.Items.Where(i => i.State == "DRAFT_DEMO" && ActiveCategory(c, i.CategoryId) && Allowed(c, "ITEMS_READ", Cat(i.CategoryId))).ToDictionary(i => i.ItemId, StringComparer.Ordinal);
        var rows = new List<StockReportRow>(); var visibleSnapshotItems = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in c.Inventory.Balances)
        {
            if (!items.TryGetValue(b.ItemId, out var item) || !c.Locations.TryGetValue(b.LocationId, out var loc) || !ValidLocation(c, loc.OrgUnitId, loc.Id) || !Allowed(c, "STOCK_READ|ITEMS_READ", ResourceScope.Required(loc.OrgUnitId, loc.Id, item.CategoryId))) continue;
            visibleSnapshotItems.Add(item.ItemId);
            var category = c.Nodes[item.CategoryId]; var path = Path(c, category); var (status, issues) = Classify(item, b, c.Now);
            rows.Add(new("SNAPSHOT", b.BalanceId, item.ItemId, item.SourceCode, item.Name, item.CategoryId, category.Name, path.Path, path.Partial, loc.OrgUnitId, c.Orgs[loc.OrgUnitId].Name, loc.Id, loc.Name, b.Unit, item.StockTracked, b.Quantity, b.Minimum, b.AsOf, b.Source, status, issues, b.State));
        }
        if (q.OrgUnitId is null && q.LocationId is null && q.AsOfFrom is null && q.AsOfTo is null)
        {
            foreach (var item in items.Values.Where(i => !visibleSnapshotItems.Contains(i.ItemId)))
            {
                var category = c.Nodes[item.CategoryId]; var p = Path(c, category);
                var status = item.StockTracked == false || item.Cells[2] == "Dịch vụ" ? "NOT_TRACKED" : item.StockTracked is null ? "TRACKING_UNCONFIRMED" : "UNKNOWN";
                rows.Add(new("ITEM_WITHOUT_VISIBLE_SNAPSHOT", null, item.ItemId, item.SourceCode, item.Name, item.CategoryId, category.Name, p.Path, p.Partial, null, null, null, null, item.PrimaryUnit, item.StockTracked, null, null, null, null, status, ["Không có snapshot trong phạm vi được phép; không suy ra địa điểm/tồn từ BOM hoặc kho ngầm định."], item.State));
            }
        }
        var all = rows.Where(r => CategoryMatches(c, q, r.CategoryId) && (q.OrgUnitId is null || r.OrgUnitId == q.OrgUnitId) && (q.LocationId is null || r.LocationId == q.LocationId) && (q.Unit is null || r.Unit == q.Unit) && (q.AsOfFrom is null || r.AsOf >= q.AsOfFrom) && (q.AsOfTo is null || r.AsOf <= q.AsOfTo))
            .Where(r => TextMatches(q.Q, r.SourceCode, r.Name, r.CategoryPath, r.LocationName, r.Source)).OrderBy(r => r.SourceCode, StringComparer.Ordinal).ThenBy(r => r.ItemId, StringComparer.Ordinal).ThenBy(r => r.LocationId, StringComparer.Ordinal).ThenBy(r => r.BalanceId, StringComparer.Ordinal).ToArray();
        var selected = all.Where(r => q.Section == "MATCHES" ? r.Status == (code == "R03" ? "OUT" : "LOW") : q.Section == "MISSING_MINIMUM" ? MissingMinimum(r) : r.Status == q.Section).ToArray();
        var statusCodes = new[] { "OUT", "LOW", "AVAILABLE", "UNKNOWN", "MISSING_MINIMUM", "NOT_TRACKED", "TRACKING_UNCONFIRMED", "SOURCE_MISSING", "INVALID_SNAPSHOT" };
        var units = selected.GroupBy(r => r.Unit, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var eligible = g.Where(r => r.Quantity is not null && r.Status is "OUT" or "LOW" or "AVAILABLE" or "MISSING_MINIMUM").ToArray();
            decimal? sum = null;
            if (eligible.Length > 0) { try { sum = eligible.Sum(r => r.Quantity!.Value); } catch (OverflowException) { throw new ReportValidationException("Tổng cùng đơn vị vượt giới hạn số; không trả tổng sai."); } }
            return new ReportUnitSummary(g.Key, g.Count(), eligible.Length, g.Count(r => r.Quantity is null), sum);
        }).ToArray();
        var times = all.Where(r => r.RowKind == "SNAPSHOT" && r.AsOf is not null && r.AsOf != default(DateTimeOffset) && r.AsOf.Value.Offset == TimeSpan.Zero && r.AsOf <= c.Now).Select(r => r.AsOf!.Value).ToArray();
        var summary = new ReportSummary(null, all.Select(r => r.ItemId).Distinct(StringComparer.Ordinal).Count(), all.Count(r => r.RowKind == "SNAPSHOT"), statusCodes.Select(s => new ReportStatusCount(s, all.Count(r => s == "MISSING_MINIMUM" ? MissingMinimum(r) : r.Status == s))).ToArray(), units, times.Length == 0 ? null : times.Min(), times.Length == 0 ? null : times.Max());
        var choices = Choices(c, code, rows.Select(r => r.Unit).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        return Result(c, code, q, summary, [], [], export ? selected : selected.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArray(), choices, selected.Length);
    }
    private static bool MissingMinimum(StockReportRow r) => r.RowKind == "SNAPSHOT" && r.StockTracked == true && r.Minimum is null && r.Status is "OUT" or "LOW" or "AVAILABLE" or "UNKNOWN" or "MISSING_MINIMUM";
    private static (string Status, string[] Issues) Classify(ItemRecord item, StockBalance b, DateTimeOffset now)
    {
        var issues = new List<string>();
        if (b.Quantity is null) issues.Add("Số lượng UNKNOWN, không thay bằng0.");
        if (b.Minimum is null) issues.Add("Chưa có ngưỡng minimum; không tự đặt ngưỡng.");
        if (string.IsNullOrWhiteSpace(b.Source)) issues.Add("Thiếu nguồn snapshot.");
        if (b.AsOf == default || b.AsOf.Offset != TimeSpan.Zero || b.AsOf > now) issues.Add("Thời điểm snapshot thiếu/không hợp lệ.");
        if (string.IsNullOrWhiteSpace(b.Unit) || b.Unit != item.PrimaryUnit) issues.Add("Đơn vị snapshot không khớp đơn vị chính; không quy đổi ngầm.");
        if (b.Quantity < 0 || b.Minimum < 0) issues.Add("Số lượng/ngưỡng âm cần đối soát.");
        var status = item.StockTracked == false || item.Cells[2] == "Dịch vụ" ? "NOT_TRACKED" : item.StockTracked is null ? "TRACKING_UNCONFIRMED" :
            b.AsOf == default || b.AsOf.Offset != TimeSpan.Zero || b.AsOf > now || string.IsNullOrWhiteSpace(b.Unit) || b.Unit != item.PrimaryUnit || b.Quantity < 0 || b.Minimum < 0 ? "INVALID_SNAPSHOT" :
            string.IsNullOrWhiteSpace(b.Source) ? "SOURCE_MISSING" : b.Quantity is null ? "UNKNOWN" : b.Quantity == 0 ? "OUT" : b.Minimum is null ? "MISSING_MINIMUM" : b.Quantity <= b.Minimum ? "LOW" : "AVAILABLE";
        return (status, issues.ToArray());
    }
}
