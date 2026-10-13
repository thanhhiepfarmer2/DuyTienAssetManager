using System.Text.Json.Serialization;
namespace DuyTien.AssetManagement.Application.Assets;

// TASK-007 is a draft demo slice, not an approved company asset/code policy.
public sealed record AssetRecord(string AssetId, string ObjectCode, string IssuedCcc,
    string CodeRuleId, string CategoryId, string Name, string Type, string OrgUnitId,
    string LocationId, string CustodianRef, string Serial, string Model, string Manufacturer,
    DateOnly? RecordedOn, string Condition, string Notes, string DataState, bool IsDeleted,
    DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset UpdatedAt, string UpdatedBy, long Version);
public sealed record AssetOrgUnit(string Id, string? ParentId, string Name, bool IsActive);
public sealed record AssetLocation(string Id, string? ParentId, string OrgUnitId, string Name, bool IsActive);
public sealed record AssetCodeRule(string RuleId, string CategoryRootId, string Prefix,
    string Ccc, string Status, bool ApprovedForDemo, long Version);
public sealed record AssetCounter(string Key, long LastAllocated);
public sealed record AssetAlias(string AliasId, string Namespace, string OldCode, string AssetId, bool IsActive);
public sealed record AssetOperation(string OperationId, string IdempotencyKey, string Actor,
    string RequestHash, string Action, AssetRecord? Before, AssetRecord Result,
    AssetAlias[] AliasesAfter, string CounterKey, long CounterValue, string Reason, DateTimeOffset CompletedAt);
public sealed class AssetCatalogState
{
    public long Revision { get; set; }
    public List<AssetRecord> Assets { get; } = [];
    public List<AssetCodeRule> Rules { get; } = [];
    public List<AssetOrgUnit> OrgUnits { get; } = [];
    public List<AssetLocation> Locations { get; } = [];
    public List<AssetCounter> Counters { get; } = [];
    public List<AssetAlias> Aliases { get; } = [];
    public List<AssetOperation> Operations { get; } = [];
    public AssetCatalogState Clone()
    {
        var c = new AssetCatalogState { Revision = Revision };
        c.Assets.AddRange(Assets); c.Rules.AddRange(Rules); c.OrgUnits.AddRange(OrgUnits);
        c.Locations.AddRange(Locations); c.Counters.AddRange(Counters); c.Aliases.AddRange(Aliases);
        c.Operations.AddRange(Operations); return c;
    }
}
public interface IAssetCatalogStore
{
    Task<AssetCatalogState> ReadAsync(CancellationToken ct = default);
    // Shared writer and backend process lease must be held by the caller.
    Task CommitAsync(AssetCatalogState candidate, long expectedRevision, CancellationToken ct = default);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssetInput(string Name, string CategoryId, string OrgUnitId, string LocationId,
    string Type = "MACHINE", string CustodianRef = "", string Serial = "", string Model = "",
    string Manufacturer = "", DateOnly? RecordedOn = null, string Condition = "UNKNOWN",
    string Notes = "", string[]? Aliases = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssetWriteRequest(AssetInput Asset, long ExpectedVersion, string TreeVersionId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssetLifecycleRequest(long ExpectedVersion, string Reason);
public sealed record AssetWriteResult(AssetRecord Asset, bool Replayed);
public sealed record AssetView(AssetRecord Asset, string CategoryName, string CategoryState,
    string OrgUnitName, string LocationName, IReadOnlyList<AssetAlias> Aliases);
public sealed record AssetQuery(string? Q = null, string? CategoryId = null, bool IncludeDescendants = true,
    string? Condition = null, string? OrgUnitId = null, string? LocationId = null,
    string? CustodianRef = null, bool IncludeDeleted = false, int Page = 1, int PageSize = 20);
public sealed record AssetList(IReadOnlyList<AssetView> Items, int Total, int Page, int PageSize, DateTimeOffset SnapshotAsOf);
public sealed record AssetChoice(string Id, string Name);
public sealed record AssetFormChoices(string? TreeVersionId, IReadOnlyList<AssetChoice> Categories,
    IReadOnlyList<AssetChoice> OrgUnits, IReadOnlyList<AssetChoice> Locations, string DataState = "DRAFT_DEMO");
public sealed class AssetValidationException(string message, string? field = null) : Exception(message)
{ public string? Field { get; } = field; }
public sealed class AssetConflictException(string message) : Exception(message);
public sealed class AssetForbiddenException() : Exception("Không có quyền thực hiện thao tác tài sản.");
public sealed class AssetNotFoundException() : Exception("Không tìm thấy tài sản hoặc phạm vi được giao.");
