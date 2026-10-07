using DuyTien.AssetManagement.Application.Concurrency;

namespace DuyTien.AssetManagement.Infrastructure.Google.Concurrency;

public sealed class InProcessSingleWriterCoordinator : ISingleWriterCoordinator, IDisposable
{
    private readonly SemaphoreSlim semaphore = new(initialCount: 1, maxCount: 1);

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Dispose() => semaphore.Dispose();
}
