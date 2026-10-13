namespace DuyTien.AssetManagement.Application.Reports;

public sealed record ReportQuery(string? Q = null, string? CategoryId = null, bool IncludeDescendants = true,
    string? OrgUnitId = null, string? LocationId = null, string? Type = null, string? Condition = null, int? GroupLevel = null,
    string? Unit = null, DateTimeOffset? AsOfFrom = null, DateTimeOffset? AsOfTo = null, string Section = "MATCHES", int Page = 1, int PageSize = 20);
public sealed record ReportChoice(string Id, string Name);
public sealed record ReportChoices(IReadOnlyList<ReportChoice> Categories, IReadOnlyList<ReportChoice> OrgUnits,
    IReadOnlyList<ReportChoice> Locations, IReadOnlyList<string> Types, IReadOnlyList<string> Conditions, IReadOnlyList<string> Units);
public sealed record AssetReportRow(string AssetId, string ObjectCode, string Name, string Type, string Condition,
    string CategoryId, string CategoryName, string CategoryPath, bool PathIsPartial, string OrgUnitId, string OrgUnitName,
    string LocationId, string LocationName, string DataState, DateTimeOffset UpdatedAt);
public sealed record CategoryReportRow(string CategoryId, string? VisibleParentId, string Name, string VisiblePath,
    bool PathIsPartial, int FLevel, int VisibleDepth, int DirectAssetCount, int SubtreeAssetCount, string State);
public sealed record StockReportRow(string RowKind, string? BalanceId, string ItemId, string SourceCode, string Name,
    string CategoryId, string CategoryName, string CategoryPath, bool PathIsPartial, string? OrgUnitId, string? OrgUnitName,
    string? LocationId, string? LocationName, string Unit, bool? StockTracked, decimal? Quantity, decimal? Minimum,
    DateTimeOffset? AsOf, string? Source, string Status, IReadOnlyList<string> Issues, string DataState);
public sealed record ReportStatusCount(string Status, int Count);
public sealed record ReportUnitSummary(string Unit, int RowCount, int KnownQuantityCount, int UnknownQuantityCount, decimal? QuantitySum);
public sealed record ReportSummary(int? AssetCount, int? ItemCount, int? SnapshotCount,
    IReadOnlyList<ReportStatusCount> StatusCounts, IReadOnlyList<ReportUnitSummary> Units,
    DateTimeOffset? EarliestSnapshotAsOf, DateTimeOffset? LatestSnapshotAsOf);
public sealed record ReportResult(string ReportCode, string Name, ReportQuery Filter, DateTimeOffset ReadAtUtc,
    string? TreeVersionId, string DataState, IReadOnlyList<string> Warnings, ReportSummary Summary,
    IReadOnlyList<AssetReportRow> Assets, IReadOnlyList<CategoryReportRow> Categories, IReadOnlyList<StockReportRow> Stock,
    ReportChoices Choices, int Total, int Page, int PageSize, bool CanExport);
public sealed record ReportExport(byte[] Bytes, string FileName, string ContentType);
public sealed class ReportValidationException(string message) : Exception(message);
public sealed class ReportForbiddenException() : Exception("Không có quyền xem/xuất báo cáo theo phạm vi.");
public sealed class ReportNotFoundException() : Exception("Không tìm thấy báo cáo hoặc bộ lọc trong phạm vi được phép.");
