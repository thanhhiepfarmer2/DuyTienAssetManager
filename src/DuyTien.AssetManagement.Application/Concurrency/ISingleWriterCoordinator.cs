namespace DuyTien.AssetManagement.Application.Concurrency;

public interface ISingleWriterCoordinator
{
    Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => ExecuteAsync(operation, cancellationToken);
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
