using System.Text.Json.Serialization;
namespace DuyTien.AssetManagement.Application.Inventory;

public sealed record ItemSourceRow(int SourceRow, string[] Cells);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ItemDocument(string Namespace, string[] Headers, ItemSourceRow[] Rows, string SourceHash = "");
public sealed record ItemGroupMapping(string SourceGroup, string? CategoryId, bool? StockTracked = null);
public sealed record ItemDuplicateDecision(string SourceCode, string Mode, Dictionary<int, string>? Identities, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ItemImportRequest(ItemDocument Document, ItemGroupMapping[] Groups, ItemDuplicateDecision[] Decisions, string Reason);
public sealed record ItemRecord(string ItemId, string Namespace, string SourceIdentity, string CategoryId, bool? StockTracked, string[] Cells, long Version, string State = "DRAFT_DEMO")
{
    public string SourceCode => Cells[0]; public string Name => Cells[1]; public string PrimaryUnit => Cells[5];
}
public sealed record ItemPlannedRow(int SourceRow, ItemRecord Item, long ExpectedVersion, string Action);
public sealed record ItemImportJob(string JobId, string Actor, string RequestHash, string TreeVersionId, string Reason, string DecisionsJson, int TotalSourceRows, int PlannedCount, int Processed, string State, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ItemImportPlanRow(string JobId, ItemPlannedRow Row);
public sealed record StockBalance(string BalanceId, string ItemId, string LocationId, string Unit, decimal? Quantity, decimal? Minimum, DateTimeOffset AsOf, string Source, string UpdatedBy, long Version, string State = "DRAFT_DEMO")
{ public string StockStatus => Quantity is null ? "UNKNOWN" : Quantity == 0 ? "OUT" : Minimum is not null && Quantity <= Minimum ? "LOW" : "AVAILABLE"; }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StockWriteRequest(string ItemId, string LocationId, string Unit, decimal? Quantity, decimal? Minimum, DateTimeOffset AsOf, string Source, long ExpectedVersion, string Reason);
public sealed record InventoryOperation(string OperationId, string Key, string Actor, string RequestHash, string Action, string Reason, DateTimeOffset At, ItemImportJob? Job, ItemImportPlanRow[] Plan, ItemRecord[] Items, StockBalance? Balance);
public sealed class InventoryState
{
    public long Revision { get; set; }
    public List<ItemRecord> Items { get; } = []; public List<StockBalance> Balances { get; } = [];
    public List<ItemImportJob> Jobs { get; } = []; public List<ItemImportPlanRow> Plan { get; } = []; public List<InventoryOperation> Operations { get; } = [];
    public InventoryState Clone() { var s = new InventoryState { Revision = Revision }; s.Items.AddRange(Items); s.Balances.AddRange(Balances); s.Jobs.AddRange(Jobs); s.Plan.AddRange(Plan); s.Operations.AddRange(Operations); return s; }
}
public interface IInventoryStore
{ Task<InventoryState> ReadAsync(CancellationToken ct = default); Task CommitAsync(InventoryState candidate, long expectedRevision, CancellationToken ct = default); }
public sealed record InventoryIssue(int? SourceRow, string Message);
public sealed record ItemDiff(int SourceRow, string SourceCode, string Name, string Action, long ExpectedVersion, string CategoryId);
public sealed record ItemPreview(string RequestHash, string PreviewHash, long Revision, string TreeVersionId, int TotalSourceRows, int PlannedCount, int Added, int Updated, int Skipped, int Quarantined, IReadOnlyList<InventoryIssue> Errors, IReadOnlyList<string> Warnings, IReadOnlyList<ItemDiff> Diff, int Page, int PageSize);
public sealed record InventoryQuery(string? Q = null, string? CategoryId = null, int Page = 1, int PageSize = 20);
public sealed record ItemList(IReadOnlyList<ItemRecord> Items, int Total, int Page, int PageSize, DateTimeOffset SnapshotAsOf);
public sealed record StockList(IReadOnlyList<StockBalance> Items, int Total, int Page, int PageSize, DateTimeOffset SnapshotAsOf);
public sealed record InventoryChoice(string Id, string Name);
public sealed record InventoryChoices(IReadOnlyList<InventoryChoice> Categories, IReadOnlyList<InventoryChoice> Locations);
public sealed class InventoryValidationException(string message) : Exception(message);
public sealed class InventoryConflictException(string message) : Exception(message);
public sealed class InventoryForbiddenException() : Exception("Không có quyền Items/snapshot theo phạm vi.");
public sealed class InventoryNotFoundException() : Exception("Không tìm thấy dữ liệu hoặc ngoài phạm vi.");
