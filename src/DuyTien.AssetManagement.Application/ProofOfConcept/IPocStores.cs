namespace DuyTien.AssetManagement.Application.ProofOfConcept;

public interface IPocAtomicDataStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<PocCounterState> GetCounterAsync(
        string counterKey,
        CancellationToken cancellationToken = default);

    Task<PocOperationState?> FindOperationAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task CommitAsync(
        PocAtomicWrite write,
        CancellationToken cancellationToken = default);
}

public interface IPocFileStateStore
{
    Task<PocFileState> BeginFileAsync(
        PocFileState pendingFile,
        CancellationToken cancellationToken = default);

    Task<PocFileState?> FindFileAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<PocFileState> UpdateFileAsync(
        string operationId,
        PocFileStatus status,
        string? driveFileId,
        string? failureCode,
        int expectedVersion,
        CancellationToken cancellationToken = default);
}
