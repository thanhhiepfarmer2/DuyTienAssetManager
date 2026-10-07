using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.ProofOfConcept;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class PocFileWriteServiceTests
{
    [Fact]
    public async Task UploadAndDownloadSuccess_SetsAvailableAndVerifiesContent()
    {
        var states = new InMemoryFileStateStore();
        var files = new InMemoryFileStore();
        var service = new PocFileWriteService(states, files);

        var result = await service.UploadAndVerifyAsync(
            "file-op-success",
            "probe.txt",
            "text/plain",
            "hello"u8.ToArray());

        Assert.Equal(PocFileStatus.Available, result.Status);
        Assert.Equal(PocFileStatus.Available, states.Current!.Status);
        Assert.Equal(1, files.UploadCount);
        Assert.Equal(1, files.DownloadCount);
    }

    [Fact]
    public async Task KnownDriveFailure_SetsFailedAndDoesNotReportSuccess()
    {
        var states = new InMemoryFileStateStore();
        var files = new InMemoryFileStore { Behavior = UploadBehavior.KnownFailure };
        var service = new PocFileWriteService(states, files);

        await Assert.ThrowsAsync<ExternalStorageException>(
            () => service.UploadAndVerifyAsync(
                "file-op-fail",
                "probe.txt",
                "text/plain",
                "hello"u8.ToArray()));

        Assert.Equal(PocFileStatus.Failed, states.Current!.Status);
        Assert.Equal("DRIVE_WRITE_FAILED", states.Current.FailureCode);
    }

    [Fact]
    public async Task AmbiguousDriveResponseAfterUpload_IsReconciled()
    {
        var states = new InMemoryFileStateStore();
        var files = new InMemoryFileStore { Behavior = UploadBehavior.AmbiguousAfterUpload };
        var service = new PocFileWriteService(states, files);

        var result = await service.UploadAndVerifyAsync(
            "file-op-reconcile",
            "probe.txt",
            "text/plain",
            "hello"u8.ToArray());

        Assert.True(result.Reconciled);
        Assert.Equal(PocFileStatus.Available, result.Status);
        Assert.Equal(1, files.UploadCount);
    }

    [Fact]
    public async Task AmbiguousDriveResponseBeforeUpload_RemainsPending()
    {
        var states = new InMemoryFileStateStore();
        var files = new InMemoryFileStore { Behavior = UploadBehavior.AmbiguousBeforeUpload };
        var service = new PocFileWriteService(states, files);

        await Assert.ThrowsAsync<ReconciliationRequiredException>(
            () => service.UploadAndVerifyAsync(
                "file-op-unknown",
                "probe.txt",
                "text/plain",
                "hello"u8.ToArray()));

        Assert.Equal(PocFileStatus.Pending, states.Current!.Status);
    }

    private enum UploadBehavior
    {
        Success,
        KnownFailure,
        AmbiguousBeforeUpload,
        AmbiguousAfterUpload
    }

    private sealed class InMemoryFileStateStore : IPocFileStateStore
    {
        public PocFileState? Current { get; private set; }

        public Task<PocFileState> BeginFileAsync(
            PocFileState pendingFile,
            CancellationToken cancellationToken = default)
        {
            Current ??= pendingFile;
            return Task.FromResult(Current);
        }

        public Task<PocFileState?> FindFileAsync(
            string operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.OperationId == operationId ? Current : null);

        public Task<PocFileState> UpdateFileAsync(
            string operationId,
            PocFileStatus status,
            string? driveFileId,
            string? failureCode,
            int expectedVersion,
            CancellationToken cancellationToken = default)
        {
            Assert.NotNull(Current);
            Assert.Equal(expectedVersion, Current.Version);
            Current = Current with
            {
                Status = status,
                DriveFileId = driveFileId,
                FailureCode = failureCode,
                Version = Current.Version + 1
            };
            return Task.FromResult(Current);
        }
    }

    private sealed class InMemoryFileStore : IAssetFileStore
    {
        private readonly Dictionary<string, (StoredFile File, byte[] Content)> files = [];

        public UploadBehavior Behavior { get; init; }
        public int UploadCount { get; private set; }
        public int DownloadCount { get; private set; }

        public Task<StorageReadiness> CheckReadinessAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageReadiness("fake", true, false, []));

        public Task<StoredFile> UploadAsync(
            FileUploadRequest request,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default)
        {
            UploadCount++;
            if (Behavior == UploadBehavior.KnownFailure)
            {
                throw new ExternalStorageException("known failure");
            }

            if (Behavior == UploadBehavior.AmbiguousBeforeUpload)
            {
                throw new ExternalStorageTransientException("timeout", commitOutcomeUnknown: true);
            }

            var file = new StoredFile(
                $"file-{request.OperationId}",
                request.FileName,
                request.ContentType,
                content.Length,
                null,
                request.OperationId,
                Reconciled: false);
            files[request.OperationId] = (file, content.ToArray());
            if (Behavior == UploadBehavior.AmbiguousAfterUpload)
            {
                throw new ExternalStorageTransientException("timeout", commitOutcomeUnknown: true);
            }

            return Task.FromResult(file);
        }

        public Task<StoredFile?> FindByOperationAsync(
            string operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                files.TryGetValue(operationId, out var entry)
                    ? entry.File with { Reconciled = true }
                    : null);

        public Task<byte[]> DownloadAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            DownloadCount++;
            return Task.FromResult(files.Values.Single(entry => entry.File.FileId == fileId).Content);
        }
    }
}
