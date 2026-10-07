using System.Security.Cryptography;
using DuyTien.AssetManagement.Application.ExternalStorage;

namespace DuyTien.AssetManagement.Application.ProofOfConcept;

public sealed class PocFileWriteService(
    IPocFileStateStore stateStore,
    IAssetFileStore fileStore)
{
    public async Task<PocFileResult> UploadAndVerifyAsync(
        string operationId,
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("OperationId and file name are required.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(content.Span));
        var pending = new PocFileState(
            operationId,
            Guid.NewGuid().ToString("N"),
            fileName,
            contentType,
            sha256,
            PocFileStatus.Pending,
            DriveFileId: null,
            FailureCode: null,
            Version: 1);

        var state = await stateStore.BeginFileAsync(pending, cancellationToken)
            .ConfigureAwait(false);
        EnsureSameRequest(state, pending);

        if (state.Status == PocFileStatus.Available && state.DriveFileId is not null)
        {
            await VerifyDownloadAsync(state.DriveFileId, sha256, cancellationToken).ConfigureAwait(false);
            return ToResult(state, reconciled: true);
        }

        StoredFile storedFile;
        try
        {
            storedFile = await fileStore.UploadAsync(
                new FileUploadRequest(operationId, fileName, contentType, sha256),
                content,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalStorageTransientException exception) when (exception.CommitOutcomeUnknown)
        {
            var found = await fileStore.FindByOperationAsync(operationId, cancellationToken)
                .ConfigureAwait(false);
            if (found is null)
            {
                throw new ReconciliationRequiredException(
                    "The Drive upload outcome is unknown; the Sheets state remains PENDING.",
                    exception);
            }

            storedFile = found with { Reconciled = true };
        }
        catch (Exception exception) when (exception is ExternalStorageException)
        {
            await stateStore.UpdateFileAsync(
                operationId,
                PocFileStatus.Failed,
                driveFileId: null,
                failureCode: "DRIVE_WRITE_FAILED",
                state.Version,
                cancellationToken).ConfigureAwait(false);
            throw;
        }

        PocFileState available;
        try
        {
            available = await stateStore.UpdateFileAsync(
                operationId,
                PocFileStatus.Available,
                storedFile.FileId,
                failureCode: null,
                state.Version,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalStorageTransientException exception) when (exception.CommitOutcomeUnknown)
        {
            var reconciled = await stateStore.FindFileAsync(operationId, cancellationToken)
                .ConfigureAwait(false);
            if (reconciled is null ||
                reconciled.Status != PocFileStatus.Available ||
                !string.Equals(reconciled.DriveFileId, storedFile.FileId, StringComparison.Ordinal))
            {
                throw new ReconciliationRequiredException(
                    "Drive has the file but the AVAILABLE Sheets state is not confirmed.",
                    exception);
            }

            available = reconciled;
        }

        await VerifyDownloadAsync(storedFile.FileId, sha256, cancellationToken).ConfigureAwait(false);
        return ToResult(available, storedFile.Reconciled);
    }

    private async Task VerifyDownloadAsync(
        string fileId,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var downloaded = await fileStore.DownloadAsync(fileId, cancellationToken).ConfigureAwait(false);
        var actualSha256 = Convert.ToHexString(SHA256.HashData(downloaded));
        if (!string.Equals(expectedSha256, actualSha256, StringComparison.Ordinal))
        {
            throw new ExternalStorageException("Downloaded Drive content failed the SHA-256 check.");
        }
    }

    private static void EnsureSameRequest(PocFileState existing, PocFileState requested)
    {
        if (!string.Equals(existing.FileName, requested.FileName, StringComparison.Ordinal) ||
            !string.Equals(existing.ContentType, requested.ContentType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existing.Sha256, requested.Sha256, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException(
                "The file operation id was already used with different content or metadata.");
        }
    }

    private static PocFileResult ToResult(PocFileState state, bool reconciled) =>
        new(
            state.OperationId,
            state.AttachmentId,
            state.DriveFileId ?? throw new InvalidOperationException("DriveFileId is required."),
            state.Sha256,
            state.Status,
            reconciled);
}
