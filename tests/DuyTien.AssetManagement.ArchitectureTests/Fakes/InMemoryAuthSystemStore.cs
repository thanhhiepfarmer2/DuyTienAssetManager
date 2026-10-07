using DuyTien.AssetManagement.Application.Authentication;

namespace DuyTien.AssetManagement.ArchitectureTests.Fakes;

internal sealed class InMemoryAuthSystemStore(AuthSystemState? initialState = null)
    : IAuthSystemStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private AuthSystemState state = initialState?.Clone() ?? new AuthSystemState();

    public async Task<T> ReadAsync<T>(
        Func<AuthSystemState, T> reader,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return reader(state.Clone());
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> MutateAsync<T>(
        Func<AuthSystemState, T> mutation,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var candidate = state.Clone();
            var result = mutation(candidate);
            state = candidate;
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<AuthSystemInitializationResult> InitializeAsync(
        string actor,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthSystemInitializationResult(true, AuthSystemSchema.CurrentVersion));

    public AuthSystemState Snapshot => state.Clone();
}

internal sealed class TestPasswordHashService : IPasswordHashService
{
    public string Hash(string userId, string password) => $"HASH::{userId}::{password}";

    public PasswordHashVerification Verify(string userId, string hash, string password) =>
        new(string.Equals(hash, Hash(userId, password), StringComparison.Ordinal), false);
}

internal sealed class ManualTimeProvider(DateTimeOffset value) : TimeProvider
{
    private DateTimeOffset value = value;

    public override DateTimeOffset GetUtcNow() => value;

    public void Advance(TimeSpan duration) => value = value.Add(duration);
}
