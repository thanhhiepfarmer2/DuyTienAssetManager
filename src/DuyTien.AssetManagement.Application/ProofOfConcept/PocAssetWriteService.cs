using System.Security.Cryptography;
using System.Text;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.ExternalStorage;

namespace DuyTien.AssetManagement.Application.ProofOfConcept;

public sealed class PocAssetWriteService(
    ISingleWriterCoordinator singleWriter,
    IPocAtomicDataStore dataStore,
    int maxAttempts = 3,
    Func<int, CancellationToken, Task>? delayAsync = null)
{
    private readonly int maxAttempts = maxAttempts > 0
        ? maxAttempts
        : throw new ArgumentOutOfRangeException(nameof(maxAttempts));

    private readonly Func<int, CancellationToken, Task> delayAsync =
        delayAsync ?? ((attempt, cancellationToken) =>
            Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)), cancellationToken));

    public Task<PocWriteResult> ExecuteAsync(
        PocWriteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        return singleWriter.ExecuteAsync(
            innerCancellationToken => ExecuteInsideWriterAsync(command, innerCancellationToken),
            cancellationToken);
    }

    private async Task<PocWriteResult> ExecuteInsideWriterAsync(
        PocWriteCommand command,
        CancellationToken cancellationToken)
    {
        var requestHash = ComputeRequestHash(command);
        var existing = await dataStore.FindOperationAsync(
            command.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return FromExisting(existing, requestHash);
        }

        var counter = await dataStore.GetCounterAsync(
            command.CounterKey,
            cancellationToken).ConfigureAwait(false);

        if (command.ExpectedCounterVersion is { } expectedVersion &&
            counter.Version != expectedVersion)
        {
            throw new VersionConflictException(
                $"Counter version conflict. Expected {expectedVersion}, actual {counter.Version}.");
        }

        if (!string.Equals(counter.Prefix, command.Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Prefix does not match the configured PoC counter.");
        }

        var write = new PocAtomicWrite(
            command.IdempotencyKey,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            $"{command.Prefix}-{counter.LastAllocated + 1:D6}",
            command.CounterKey,
            counter.LastAllocated + 1,
            counter.Version,
            requestHash,
            command.Payload,
            command.Actor,
            DateTimeOffset.UtcNow);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await dataStore.CommitAsync(write, cancellationToken).ConfigureAwait(false);
                return FromWrite(write, PocWriteOutcome.Committed);
            }
            catch (ExternalStorageTransientException exception)
            {
                var reconciled = await dataStore.FindOperationAsync(
                    command.IdempotencyKey,
                    cancellationToken).ConfigureAwait(false);

                if (reconciled is not null)
                {
                    return FromExisting(reconciled, requestHash);
                }

                if (exception.CommitOutcomeUnknown)
                {
                    throw new ReconciliationRequiredException(
                        "The Sheets write outcome is unknown and no committed operation is visible yet.",
                        exception);
                }

                if (attempt == maxAttempts)
                {
                    throw;
                }

                await delayAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Retry loop ended unexpectedly.");
    }

    private static PocWriteResult FromExisting(PocOperationState operation, string requestHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(operation.RequestHash),
                Encoding.UTF8.GetBytes(requestHash)))
        {
            throw new IdempotencyConflictException(
                "The idempotency key was already used with a different request payload.");
        }

        if (!string.Equals(operation.State, "COMMITTED", StringComparison.Ordinal))
        {
            throw new ReconciliationRequiredException(
                $"Operation exists with non-terminal state '{operation.State}'.");
        }

        return new PocWriteResult(
            operation.OperationId,
            operation.EntityId,
            operation.ObjectCode,
            operation.CounterVersion,
            PocWriteOutcome.Reconciled);
    }

    private static PocWriteResult FromWrite(PocAtomicWrite write, PocWriteOutcome outcome) =>
        new(
            write.OperationId,
            write.EntityId,
            write.ObjectCode,
            write.ExpectedCounterVersion + 1,
            outcome);

    private static string ComputeRequestHash(PocWriteCommand command)
    {
        var canonical = string.Join(
            '\n',
            command.CounterKey,
            command.Prefix,
            command.Payload,
            command.Actor);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void Validate(PocWriteCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128)
        {
            throw new ArgumentException("IdempotencyKey is required and must be at most 128 characters.");
        }

        if (string.IsNullOrWhiteSpace(command.CounterKey) ||
            string.IsNullOrWhiteSpace(command.Prefix) ||
            string.IsNullOrWhiteSpace(command.Actor))
        {
            throw new ArgumentException("CounterKey, Prefix and Actor are required.");
        }
    }
}
