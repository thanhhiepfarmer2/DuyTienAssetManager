namespace DuyTien.AssetManagement.Application.ProofOfConcept;

public sealed record PocCounterState(
    string CounterKey,
    long LastAllocated,
    string Prefix,
    int Version);

public sealed record PocOperationState(
    string IdempotencyKey,
    string OperationId,
    string EntityId,
    string ObjectCode,
    string RequestHash,
    string State,
    int CounterVersion);

public sealed record PocAtomicWrite(
    string IdempotencyKey,
    string OperationId,
    string EntityId,
    string ObjectCode,
    string CounterKey,
    long NextCounterValue,
    int ExpectedCounterVersion,
    string RequestHash,
    string Payload,
    string Actor,
    DateTimeOffset Timestamp);

public sealed record PocWriteCommand(
    string IdempotencyKey,
    string CounterKey,
    string Prefix,
    string Payload,
    string Actor,
    int? ExpectedCounterVersion = null);

public enum PocWriteOutcome
{
    Committed,
    Reconciled
}

public sealed record PocWriteResult(
    string OperationId,
    string EntityId,
    string ObjectCode,
    int CounterVersion,
    PocWriteOutcome Outcome);

public enum PocFileStatus
{
    Pending,
    Available,
    Failed
}

public sealed record PocFileState(
    string OperationId,
    string AttachmentId,
    string FileName,
    string ContentType,
    string Sha256,
    PocFileStatus Status,
    string? DriveFileId,
    string? FailureCode,
    int Version);

public sealed record PocFileResult(
    string OperationId,
    string AttachmentId,
    string DriveFileId,
    string Sha256,
    PocFileStatus Status,
    bool Reconciled);
