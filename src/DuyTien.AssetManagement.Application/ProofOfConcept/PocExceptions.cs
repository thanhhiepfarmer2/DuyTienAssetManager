namespace DuyTien.AssetManagement.Application.ProofOfConcept;

public sealed class IdempotencyConflictException(string message) : Exception(message);

public sealed class VersionConflictException(string message) : Exception(message);

public sealed class ReconciliationRequiredException(string message, Exception? innerException = null)
    : Exception(message, innerException);
