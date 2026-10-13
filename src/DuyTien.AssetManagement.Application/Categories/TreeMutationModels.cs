using System.Text.Json.Serialization;
using DuyTien.AssetManagement.Application.Assets;
namespace DuyTien.AssetManagement.Application.Categories;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TreeMutationRequest(string Kind, string Reason, string? CategoryId = null,
    string? DestinationCategoryId = null, string? Name = null, string? SegmentCode = null,
    string? Definition = null, string DeletePlan = "EMPTY_ONLY", string[]? AssetIds = null,
    string? DestinationOrgUnitId = null, string? DestinationLocationId = null);
public sealed record TreeNodeImpact(string CategoryId, string Name, string BeforePath,
    string AfterPath, int BeforeLevel, int AfterLevel, string BeforeState, string AfterState);
public sealed record TreeAssetImpact(string AssetId, string ObjectCode, string Name, long ExpectedVersion,
    string BeforeCategory, string AfterCategory, string BeforeLocation, string AfterLocation, bool WillBeDeleted);
public sealed record TreeMutationPreview(long TreeRevision, long AssetRevision, string PreviewHash,
    string TreeVersionId, IReadOnlyList<TreeNodeImpact> Categories, IReadOnlyList<TreeAssetImpact> Assets,
    int ChangedAssetCount, IReadOnlyList<string> Issues, int AffectedCategoryCount = 0, int AffectedAssetCount = 0, string State = "DRAFT")
{ public bool CanCommit => Issues.Count == 0; }
public sealed record TreeMutationResult(string TreeVersionId, long TreeRevision, long AssetRevision,
    int ChangedCategoryCount, int ChangedAssetCount, bool Replayed = false);
public sealed record TreeChangeOperation(string OperationId, string IdempotencyKey, string Actor,
    string RequestHash, string Kind, string BeforeTreeVersionId, string TreeVersionId,
    long ExpectedTreeRevision, long ExpectedAssetRevision, string Reason, DateTimeOffset CompletedAt,
    string PreviewHash, TreeMutationRequest Request, string[] CategoryIds, string[] AssetIds,
    TreeNodeImpact[] Diff, TreeMutationResult Result);
public sealed record TreeMutationSnapshot(CategoryCatalogState Categories, AssetCatalogState Assets);
public interface ITreeMutationStore
{
    // Caller holds shared writer; normal readers also use it to avoid mixed snapshots across two reads.
    Task<TreeMutationSnapshot> ReadAsync(CancellationToken ct = default);
    // One atomic commit in the SAME business spreadsheet; never two sequential store commits.
    Task CommitAsync(TreeMutationSnapshot candidate, long expectedTreeRevision, long expectedAssetRevision, CancellationToken ct = default);
}
public sealed record CategoryHistoryEntry(string TreeVersionId, string Name, string FullCode, string Path, string State, bool PathIsPartial);
