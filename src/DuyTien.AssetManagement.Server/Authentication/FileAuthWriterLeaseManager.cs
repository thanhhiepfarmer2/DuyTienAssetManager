namespace DuyTien.AssetManagement.Server.Authentication;

public interface IAuthWriterLeaseManager
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        string owner,
        CancellationToken cancellationToken = default);
}

public sealed class FileAuthWriterLeaseManager(AuthRuntimeOptions options)
    : IAuthWriterLeaseManager
{
    public ValueTask<IAsyncDisposable> AcquireAsync(
        string owner,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(options.SystemWriterLockFilePath))
        {
            throw new InvalidOperationException("Auth:SystemWriterLockFilePath is required.");
        }

        var fullPath = Path.GetFullPath(options.SystemWriterLockFilePath);
        var directory = Path.GetDirectoryName(fullPath) ??
            throw new InvalidOperationException("Auth writer lock path has no directory.");
        Directory.CreateDirectory(directory);

        FileStream stream;
        try
        {
            stream = new FileStream(
                fullPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
        }
        catch (IOException exception)
        {
            throw new AuthWriterLeaseUnavailableException(
                "Một web server hoặc lệnh auth khác đang giữ system-writer lease.",
                exception);
        }

        var metadata = System.Text.Encoding.UTF8.GetBytes(
            $"owner={owner};pid={Environment.ProcessId};acquiredAt={DateTimeOffset.UtcNow:O}");
        stream.SetLength(0);
        stream.Write(metadata);
        stream.Flush(flushToDisk: true);
        stream.Position = 0;
        return ValueTask.FromResult<IAsyncDisposable>(new FileLease(stream));
    }

    private sealed class FileLease(FileStream stream) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }
}

public sealed class AuthWriterLeaseUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
