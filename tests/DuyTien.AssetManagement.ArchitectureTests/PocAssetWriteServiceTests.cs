using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.ProofOfConcept;
using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class PocAssetWriteServiceTests
{
    [Fact]
    public async Task ConcurrentCommands_AreSerializedAndReceiveUniqueCodes()
    {
        var store = new InMemoryPocStore();
        using var coordinator = new InProcessSingleWriterCoordinator();
        var service = CreateService(coordinator, store);

        var results = await Task.WhenAll(
            service.ExecuteAsync(Command("key-a", "payload-a")),
            service.ExecuteAsync(Command("key-b", "payload-b")));

        Assert.Equal(2, results.Select(result => result.ObjectCode).Distinct().Count());
        Assert.Equal(2, store.EntityCount);
        Assert.Equal(2, store.AuditCount);
        Assert.Equal(2, store.OperationCount);
    }

    [Fact]
    public async Task SameKeyAfterServiceRestart_ReturnsOriginalEntityWithoutDuplicate()
    {
        var store = new InMemoryPocStore();
        PocWriteResult first;
        using (var firstCoordinator = new InProcessSingleWriterCoordinator())
        {
            first = await CreateService(firstCoordinator, store)
                .ExecuteAsync(Command("restart-key", "payload"));
        }

        using var restartedCoordinator = new InProcessSingleWriterCoordinator();
        var replay = await CreateService(restartedCoordinator, store)
            .ExecuteAsync(Command("restart-key", "payload"));

        Assert.Equal(PocWriteOutcome.Reconciled, replay.Outcome);
        Assert.Equal(first.EntityId, replay.EntityId);
        Assert.Equal(1, store.OperationCount);
    }

    [Fact]
    public async Task SameKeyWithDifferentPayload_IsRejected()
    {
        var store = new InMemoryPocStore();
        using var coordinator = new InProcessSingleWriterCoordinator();
        var service = CreateService(coordinator, store);
        await service.ExecuteAsync(Command("same-key", "payload-a"));

        await Assert.ThrowsAsync<IdempotencyConflictException>(
            () => service.ExecuteAsync(Command("same-key", "payload-b")));
        Assert.Equal(1, store.OperationCount);
    }

    [Fact]
    public async Task StaleExpectedVersion_IsRejectedBeforeCommit()
    {
        var store = new InMemoryPocStore();
        using var coordinator = new InProcessSingleWriterCoordinator();
        var service = CreateService(coordinator, store);
        await service.ExecuteAsync(Command("first", "payload"));

        await Assert.ThrowsAsync<VersionConflictException>(
            () => service.ExecuteAsync(Command("stale", "payload") with
            {
                ExpectedCounterVersion = 0
            }));
        Assert.Equal(1, store.OperationCount);
    }

    [Fact]
    public async Task TransientFailureBeforeCommit_IsRetriedWithinLimit()
    {
        var store = new InMemoryPocStore();
        store.Behaviors.Enqueue(CommitBehavior.TransientBeforeCommit);
        using var coordinator = new InProcessSingleWriterCoordinator();
        var service = CreateService(coordinator, store);

        var result = await service.ExecuteAsync(Command("retry-key", "payload"));

        Assert.Equal(PocWriteOutcome.Committed, result.Outcome);
        Assert.Equal(2, store.CommitAttempts);
        Assert.Equal(1, store.OperationCount);
    }

    [Fact]
    public async Task TimeoutAfterCommit_IsReconciledWithoutDuplicate()
    {
        var store = new InMemoryPocStore();
        store.Behaviors.Enqueue(CommitBehavior.AmbiguousAfterCommit);
        using var coordinator = new InProcessSingleWriterCoordinator();
        var service = CreateService(coordinator, store);

        var result = await service.ExecuteAsync(Command("timeout-key", "payload"));

        Assert.Equal(PocWriteOutcome.Reconciled, result.Outcome);
        Assert.Equal(1, store.OperationCount);
        Assert.Equal(1, store.CommitAttempts);
    }

    [Fact]
    public async Task TimeoutBeforeCommit_DoesNotReportSuccess()
    {
        var store = new InMemoryPocStore();
        store.Behaviors.Enqueue(CommitBehavior.AmbiguousBeforeCommit);
        using var coordinator = new InProcessSingleWriterCoordinator();
        var service = CreateService(coordinator, store);

        await Assert.ThrowsAsync<ReconciliationRequiredException>(
            () => service.ExecuteAsync(Command("unknown-key", "payload")));
        Assert.Equal(0, store.OperationCount);
    }

    [Fact]
    public async Task Repeated429Equivalent_StopsAtConfiguredAttemptLimit()
    {
        var store = new InMemoryPocStore();
        store.Behaviors.Enqueue(CommitBehavior.TransientBeforeCommit);
        store.Behaviors.Enqueue(CommitBehavior.TransientBeforeCommit);
        store.Behaviors.Enqueue(CommitBehavior.TransientBeforeCommit);
        using var coordinator = new InProcessSingleWriterCoordinator();
        var service = CreateService(coordinator, store);

        await Assert.ThrowsAsync<ExternalStorageTransientException>(
            () => service.ExecuteAsync(Command("limit-key", "payload")));
        Assert.Equal(3, store.CommitAttempts);
        Assert.Equal(0, store.OperationCount);
    }

    private static PocAssetWriteService CreateService(
        InProcessSingleWriterCoordinator coordinator,
        InMemoryPocStore store) =>
        new(coordinator, store, maxAttempts: 3, (_, _) => Task.CompletedTask);

    private static PocWriteCommand Command(string key, string payload) =>
        new(key, "TASK004", "POC", payload, "local-test");

    private enum CommitBehavior
    {
        Success,
        TransientBeforeCommit,
        AmbiguousBeforeCommit,
        AmbiguousAfterCommit
    }

    private sealed class InMemoryPocStore : IPocAtomicDataStore
    {
        private readonly Dictionary<string, PocOperationState> operations = [];
        private long lastAllocated;
        private int version;

        public Queue<CommitBehavior> Behaviors { get; } = [];
        public int CommitAttempts { get; private set; }
        public int EntityCount { get; private set; }
        public int AuditCount { get; private set; }
        public int OperationCount => operations.Count;

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PocCounterState> GetCounterAsync(
            string counterKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PocCounterState(counterKey, lastAllocated, "POC", version));

        public Task<PocOperationState?> FindOperationAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(operations.GetValueOrDefault(idempotencyKey));

        public Task CommitAsync(
            PocAtomicWrite write,
            CancellationToken cancellationToken = default)
        {
            CommitAttempts++;
            var behavior = Behaviors.TryDequeue(out var queued)
                ? queued
                : CommitBehavior.Success;
            if (behavior == CommitBehavior.TransientBeforeCommit)
            {
                throw new ExternalStorageTransientException("429", commitOutcomeUnknown: false);
            }

            if (behavior == CommitBehavior.AmbiguousBeforeCommit)
            {
                throw new ExternalStorageTransientException("timeout", commitOutcomeUnknown: true);
            }

            if (write.ExpectedCounterVersion != version)
            {
                throw new VersionConflictException("stale");
            }

            lastAllocated = write.NextCounterValue;
            version++;
            operations.Add(
                write.IdempotencyKey,
                new PocOperationState(
                    write.IdempotencyKey,
                    write.OperationId,
                    write.EntityId,
                    write.ObjectCode,
                    write.RequestHash,
                    "COMMITTED",
                    version));
            EntityCount++;
            AuditCount++;

            if (behavior == CommitBehavior.AmbiguousAfterCommit)
            {
                throw new ExternalStorageTransientException("timeout", commitOutcomeUnknown: true);
            }

            return Task.CompletedTask;
        }
    }
}
