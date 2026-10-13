using System.Globalization;
using System.Text;
namespace DuyTien.AssetManagement.Application.Reports;

public sealed partial class ReportService
{
    public Task<ReportExport> ExportAsync(string actor, string code, ReportQuery query, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    {
        // Fresh authority/source snapshot; export scope can be narrower than read scope.
        var report = await Build(actor, code, query, true, t); var csv = new StringBuilder(); long csvBytes = 3;
        void Row(params string[] cells)
        {
            var line = string.Join(',', cells.Select(Cell)) + "\r\n";
            csvBytes += Encoding.UTF8.GetByteCount(line);
            if (csvBytes > 32 * 1024 * 1024) throw new ReportValidationException("CSV vượt32MiB; thu hẹp bộ lọc, không xuất một phần im lặng.");
            csv.Append(line);
        }
        string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        string Decimal(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
        Row("Report", report.ReportCode, report.Name); Row("ReadAtUTC", report.ReadAtUtc.ToString("O")); Row("DataState", report.DataState); Row("Scope", "Chỉ các dòng được phép REPORTS_EXPORT và quyền nguồn trong cùng assignment");
        Row("Filters", "Q=" + (query.Q ?? ""), "Category=" + (query.CategoryId ?? ""), "IncludeDescendants=" + query.IncludeDescendants, "Org=" + (query.OrgUnitId ?? ""), "Location=" + (query.LocationId ?? ""), "Type=" + (query.Type ?? ""), "Condition=" + (query.Condition ?? ""), "F=" + (query.GroupLevel?.ToString(CultureInfo.InvariantCulture) ?? ""), "Unit=" + (query.Unit ?? ""), "From=" + (query.AsOfFrom?.ToString("O") ?? ""), "To=" + (query.AsOfTo?.ToString("O") ?? ""), "Section=" + query.Section);
        Row("SelectedRows", N(report.Total)); foreach (var warning in report.Warnings) Row("Warning", warning);
        if (code == "R01")
        {
            Row("AssetId", "ObjectCode", "Name", "Type", "Condition", "CategoryPathAllowed", "PathPartial", "OrgUnit", "Location", "DataState", "UpdatedAtUTC");
            foreach (var r in report.Assets) Row(r.AssetId, r.ObjectCode, r.Name, r.Type, r.Condition, r.CategoryPath, r.PathIsPartial.ToString(), r.OrgUnitName, r.LocationName, r.DataState, r.UpdatedAt.ToString("O"));
        }
        else if (code == "R02")
        {
            Row("UniqueAssetCount", N(report.Summary.AssetCount ?? 0)); Row("Note", "Không cộng SubtreeAssetCount giữa các cấpF; một Asset chỉ tính một lần trong UniqueAssetCount");
            Row("CategoryId", "CategoryPathAllowed", "PathPartial", "FLevel", "VisibleDepth", "DirectAssetCount", "SubtreeAssetCount", "State");
            foreach (var r in report.Categories) Row(r.CategoryId, r.VisiblePath, r.PathIsPartial.ToString(), N(r.FLevel), N(r.VisibleDepth), N(r.DirectAssetCount), N(r.SubtreeAssetCount), r.State);
        }
        else
        {
            Row("Note", "Thiếu ngưỡng có thể trùng UNKNOWN/hết; không cộng các counts thành tổng. Quantity chỉ tổng riêng exact unit.");
            foreach (var count in report.Summary.StatusCounts) Row("StatusCount", count.Status, N(count.Count));
            foreach (var unit in report.Summary.Units) Row("UnitSummary", unit.Unit, N(unit.RowCount), N(unit.KnownQuantityCount), N(unit.UnknownQuantityCount), Decimal(unit.QuantitySum));
            Row("RowKind", "ItemId", "SourceCode", "Name", "CategoryPathAllowed", "OrgUnit", "Location", "Unit", "StockTracked", "Quantity", "Minimum", "AsOfUTC", "Source", "Status", "Issues", "DataState");
            foreach (var r in report.Stock) Row(r.RowKind, r.ItemId, r.SourceCode, r.Name, r.CategoryPath, r.OrgUnitName ?? "", r.LocationName ?? "", r.Unit, r.StockTracked?.ToString() ?? "UNCONFIRMED", Decimal(r.Quantity), Decimal(r.Minimum), r.AsOf?.ToString("O") ?? "", r.Source ?? "", r.Status, string.Join("; ", r.Issues), r.DataState);
        }
        var encoding = new UTF8Encoding(true); var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(csv.ToString())).ToArray();
        if (bytes.Length > 32 * 1024 * 1024) throw new ReportValidationException("CSV vượt32MiB; thu hẹp bộ lọc, không xuất một phần im lặng.");
        return new ReportExport(bytes, code + "-DRAFT-" + report.ReadAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv", "text/csv; charset=utf-8");
    }, ct);
    private static string Cell(string value)
    {
        var first = value.TrimStart(); while (first.Length > 0 && first[0] == '\uFEFF') first = first[1..].TrimStart();
        if (first.Length > 0 && first[0] is '=' or '+' or '-' or '@' || value.Length > 0 && value[0] is '\t' or '\r' or '\n') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
