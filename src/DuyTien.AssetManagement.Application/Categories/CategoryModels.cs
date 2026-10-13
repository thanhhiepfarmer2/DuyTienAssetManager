namespace DuyTien.AssetManagement.Application.Categories;

public sealed record CategoryInput(string SourceSheet, int SourceRow, string SourceStt,
    string? ParentSourceStt, int Level, string Name, string SegmentCode,
    string Definition = "", string? CategoryId = null);
public sealed record CategoryDocument(string SourceHash, string MappingVersion,
    IReadOnlyList<CategoryInput> Rows, IReadOnlyList<string> Warnings);
public sealed record CategoryIssue(string Code, string SourceSheet, int SourceRow, string Message);
public sealed record CategoryDuplicateName(string Name, IReadOnlyList<string> Codes);
public sealed record CategoryNode(string CategoryId, string TreeVersionId, string? ParentId,
    int FLevel, int DisplayOrder, string Name, string SegmentCode, string FullCode,
    string Definition, string State, int Version, string SourceKey,
    string SourceSheet, int SourceRow, string SourceStt);
public sealed record CategoryTreeVersion(string TreeVersionId, string State, string SourceHash,
    string MappingVersion, string ContentHash, DateTimeOffset CreatedAt, string ActorUserId);
public sealed record CategoryImportOperation(string OperationId, string IdempotencyKey,
    string ActorUserId, string RequestHash, string TreeVersionId, int TotalRows,
    DateTimeOffset CompletedAt, string SourceHash = "", string MappingVersion = "", bool AlreadyImported = false);
public sealed class CategoryCatalogState
{
    public long Revision { get; set; }
    public string? ActiveTreeVersionId { get; set; }
    public List<CategoryNode> Categories { get; } = [];
    public List<CategoryTreeVersion> Versions { get; } = [];
    public List<CategoryImportOperation> Operations { get; } = [];
    public List<TreeChangeOperation> TreeChanges { get; } = [];
    public CategoryNode[] ActiveNodes => Categories.Where(n => n.TreeVersionId == ActiveTreeVersionId).ToArray();
    public CategoryCatalogState Clone()
    {
        var result = new CategoryCatalogState { Revision = Revision, ActiveTreeVersionId = ActiveTreeVersionId };
        result.Categories.AddRange(Categories); result.Versions.AddRange(Versions);
        result.Operations.AddRange(Operations); result.TreeChanges.AddRange(TreeChanges); return result;
    }
}
public interface ICategoryCatalogStore
{
    Task<CategoryCatalogState> ReadAsync(CancellationToken cancellationToken = default);
    // Caller holds the shared single-writer coordinator and backend process lease.
    Task CommitAsync(CategoryCatalogState state, long expectedRevision, CancellationToken cancellationToken = default);
}
public sealed record CategoryDiff(string Kind, string CategoryId, string Name,
    string? BeforePath, string? AfterPath, string? BeforeDefinition, string? AfterDefinition);
public sealed record CategoryPreview(long Revision, string PreviewHash, string SourceHash,
    IReadOnlyList<CategoryInput> Rows, IReadOnlyList<CategoryIssue> Issues,
    IReadOnlyList<CategoryDuplicateName> DuplicateNames, IReadOnlyList<string> Warnings,
    IReadOnlyList<CategoryDiff> Diff, int Total, int F1, int F2, int F3)
{
    public bool CanCommit => Issues.Count == 0;
}
public sealed record CategoryImportResult(string TreeVersionId, long Revision, bool AlreadyImported, int Total);
public sealed record CategoryView(string CategoryId, string? ParentId, int FLevel, int VisibleDepth,
    string Name, string SegmentCode, string FullCode, string Path, bool PathIsPartial,
    string Definition, string State, int Version, string SourceSheet, int SourceRow, string SourceStt);
public sealed record CategoryList(long Revision, string? TreeVersionId, int Total, IReadOnlyList<CategoryView> Nodes);
public sealed class CategoryValidationException(string message) : Exception(message);
public sealed class CategoryConflictException(string message) : Exception(message);
public sealed class CategoryForbiddenException() : Exception("Không có quyền thực hiện thao tác danh mục.");
public sealed class CategoryNotFoundException() : Exception("Không tìm thấy danh mục trong phạm vi được giao.");
