namespace DuyTien.AssetManagement.Application.Concurrency;

public interface ISingleWriterCoordinator
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
