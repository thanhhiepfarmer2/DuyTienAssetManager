using DuyTien.AssetManagement.Application.Authentication;
namespace DuyTien.AssetManagement.Server.LocalOffline;

public sealed class RequestAuthStore(IAuthSystemStore inner) : IAuthSystemStore, IAuthSnapshotValidation
{
    public bool CanValidateReadOnly => inner is IAuthSnapshotValidation { CanValidateReadOnly: true };
    private static readonly AsyncLocal<Frame?> Current = new();
    private sealed class Frame { public Dictionary<RequestAuthStore, AuthSystemState> States { get; } = new(); }
    public static IDisposable Begin() { var old = Current.Value; Current.Value = new(); return new Reset(() => Current.Value = old); }
    public async Task<T> ReadAsync<T>(Func<AuthSystemState, T> reader, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); var frame = Current.Value; if (frame is null) return await inner.ReadAsync(reader, cancellationToken); if (!frame.States.TryGetValue(this, out var state)) { using var metric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure("auth-store-read"); state = await inner.ReadAsync(s => s.Clone(), cancellationToken); frame.States[this] = state; } return reader(state.Clone()); }
    public async Task<T> MutateAsync<T>(Func<AuthSystemState, T> mutation, CancellationToken cancellationToken = default)
    {
        Current.Value?.States.Remove(this); AuthSystemState? committed = null;
        try { using var metric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure("auth-store-mutate"); var result = await inner.MutateAsync(s => { var value = mutation(s); committed = s.Clone(); return value; }, cancellationToken); if (Current.Value is { } frame && committed is not null) frame.States[this] = committed; return result; }
        catch { Current.Value?.States.Remove(this); throw; }
    }
    public Task<AuthSystemInitializationResult> InitializeAsync(string actor, CancellationToken cancellationToken = default) => inner.InitializeAsync(actor, cancellationToken);
    private sealed class Reset(Action action) : IDisposable { public void Dispose() => action(); }
}
