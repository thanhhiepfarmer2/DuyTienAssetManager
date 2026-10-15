using System.Net.NetworkInformation;
using System.Net.Sockets;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Http;

namespace DuyTien.AssetManagement.Server.ConnectedCache;

public enum InternetState { Connected, Disconnected, Indeterminate }
public interface IInternetProbe { Task<InternetState> ProbeAsync(CancellationToken ct); }
// Any verified TLS HTTP response proves transport availability, including HTTP errors.
// A single host failure, DNS failure, 429/503 or a NIC flag alone never authorizes offline access.
public sealed class InternetProbe(IHttpClientFactory clients, Func<bool>? networkAvailable = null) : IInternetProbe
{
    public async Task<InternetState> ProbeAsync(CancellationToken ct)
    {
        var unreachable = 0; var timeouts = 0;
        foreach (var url in new[] { "https://www.msftconnecttest.com/connecttest.txt", "https://www.google.com/generate_204", "https://sheets.googleapis.com/" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            try { using var client = clients.CreateClient("InternetProbe"); using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); return InternetState.Connected; }
            catch (HttpRequestException e) when (NetworkFailure(e)) { unreachable++; }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { timeouts++; }
        }
        return timeouts == 3 || unreachable >= 2 || !(networkAvailable ?? NetworkInterface.GetIsNetworkAvailable)() ? InternetState.Disconnected : InternetState.Indeterminate;
    }
    private static bool NetworkFailure(Exception e) => e is SocketException s && s.SocketErrorCode is SocketError.NetworkDown or SocketError.NetworkUnreachable or SocketError.HostUnreachable || e.InnerException is { } inner && NetworkFailure(inner);
}
public sealed class ConnectionGate(IInternetProbe probe, TimeProvider clock) : IGoogleConnectivityGuard, IDisposable
{
    private sealed class Operation { public string? Epoch; }
    private static readonly AsyncLocal<Operation?> CurrentOperation = new();
    public static IDisposable BeginOperation() { var old = CurrentOperation.Value; CurrentOperation.Value = new(); return new OperationReset(() => CurrentOperation.Value = old); }
    public static void BindOperation(string epoch) { if (CurrentOperation.Value is { } operation) operation.Epoch ??= epoch; }
    private sealed class OperationReset(Action reset) : IDisposable { public void Dispose() => reset(); }
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextProbe;
    private int failures;
    private volatile InternetState state = InternetState.Indeterminate;
    public InternetState State { get => state; private set => state = value; }
    private volatile string epoch = Guid.NewGuid().ToString("N");
    public string Epoch { get => epoch; private set => epoch = value; }
    public bool BackgroundActive { get; set; }
    public Task<InternetState> ObserveAsync(CancellationToken ct) => BackgroundActive && LastProbeAt.HasValue ? Task.FromResult(State) : CheckAsync(ct);
    public DateTimeOffset? LastProbeAt { get; private set; }
    public async Task<InternetState> CheckAsync(CancellationToken ct, bool force = false)
    {
        await gate.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow();
            if (now < nextProbe && (!force || LastProbeAt > now.AddSeconds(-2))) return State;
            var current = await probe.ProbeAsync(ct);
            if (current != State) Epoch = Guid.NewGuid().ToString("N");
            State = current; LastProbeAt = clock.GetUtcNow();
            failures = current == InternetState.Connected ? 0 : Math.Min(failures + 1, 6);
            nextProbe = LastProbeAt.Value.AddSeconds(current == InternetState.Connected ? 15 : Math.Min(60, Math.Pow(2, failures)));
            return State;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    public async Task EnsureAllowedAsync(CancellationToken ct)
    {
        if (await CheckAsync(ct) != InternetState.Connected) throw new ExternalStorageException("Chưa xác minh được kết nối Internet; không gửi yêu cầu nghiệp vụ trung tâm.");
        if (CurrentOperation.Value is { } operation)
        {
            if (operation.Epoch is not null && operation.Epoch != Epoch) throw new ExternalStorageException("Kết nối đã đổi trong tác vụ. Dừng gửi tiếp, giữ key và đối soát bằng request đã xác minh lại.");
            operation.Epoch ??= Epoch;
        }
    }
}


// Probes run outside business reads. A known-offline snapshot read never waits on an Internet timeout.
public sealed class ConnectionMonitor(ConnectionGate gate) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        gate.BackgroundActive = true;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await gate.CheckAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { gate.BackgroundActive = false; }
    }
}
