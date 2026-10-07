using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class InProcessSingleWriterCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_SerializesConcurrentOperations()
    {
        using var coordinator = new InProcessSingleWriterCoordinator();
        var firstEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = coordinator.ExecuteAsync(async cancellationToken =>
        {
            firstEntered.SetResult(true);
            await releaseFirst.Task.WaitAsync(cancellationToken);
            return 1;
        });

        await firstEntered.Task;
        var second = coordinator.ExecuteAsync(_ => Task.FromResult(2));

        Assert.False(second.IsCompleted);
        releaseFirst.SetResult(true);

        Assert.Equal(1, await first);
        Assert.Equal(2, await second);
    }
}
