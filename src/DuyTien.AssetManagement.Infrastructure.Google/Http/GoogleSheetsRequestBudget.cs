using System.Net;
using System.Text.Json;
using DuyTien.AssetManagement.Application.ExternalStorage;
namespace DuyTien.AssetManagement.Infrastructure.Google.Http;

// Operational timestamps only; the backend/maintenance CLI already hold the shared writer lease.
// No cached business/auth data, so every guard/version/scope read remains fresh.
public sealed class GoogleSheetsRequestBudget(string path, TimeProvider? timeProvider = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync = delay ?? Task.Delay;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(1250);
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public async Task WaitAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!Applies(request)) return;
        while (true)
        {
            var reservation = await ReserveAsync(request.Method != HttpMethod.Get, false, ct);
            var wait = reservation.Delay;
            if (wait == TimeSpan.Zero) return;
            if (wait > TimeSpan.FromSeconds(65)) throw new ExternalStorageTransientException("Google request budget đang bận; giữ yêu cầu để thử lại.", false);
            using (var metric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure(reservation.Cooldown ? "google-cooldown" : "google-pacing")) await delayAsync(wait, ct);
        }
    }
    public async Task RejectedAsync(HttpRequestMessage request, HttpStatusCode status, CancellationToken ct)
    { if (status == HttpStatusCode.TooManyRequests && Applies(request)) _ = await ReserveAsync(request.Method != HttpMethod.Get, true, ct); }
    private static bool Applies(HttpRequestMessage request) => request.RequestUri?.Host.Equals("sheets.googleapis.com", StringComparison.OrdinalIgnoreCase) == true;
    private async Task<(TimeSpan Delay, bool Cooldown)> ReserveAsync(bool write, bool rejected, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await using var fileLock = await LockAsync(full + ".reservation", ct);
            Cursor cursor;
            try { cursor = File.Exists(full) ? JsonSerializer.Deserialize<Cursor>(await File.ReadAllBytesAsync(full, ct), Json) ?? throw new JsonException() : new(default, default); }
            catch (Exception e) when (e is JsonException or IOException) { throw new ExternalStorageException("Local Google budget không hợp lệ; chưa gửi request.", e); }
            var now = clock.GetUtcNow(); var ready = write ? cursor.Write : cursor.Read;
            if (!rejected && ready > now) return (ready - now, (write ? cursor.WriteCooldown : cursor.ReadCooldown) > now);
            var next = rejected ? now.AddMinutes(1) : now + Spacing;
            if (rejected && ready > next) next = ready;
            var updated = write ? cursor with { Write = next, WriteCooldown = rejected ? next : cursor.WriteCooldown } : cursor with { Read = next, ReadCooldown = rejected ? next : cursor.ReadCooldown };
            var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllBytesAsync(temp, JsonSerializer.SerializeToUtf8Bytes(updated, Json), ct); File.Move(temp, full, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return (TimeSpan.Zero, false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new ExternalStorageException("Không giữ được Google request budget; chưa gửi request.", e); }
        finally { gate.Release(); }
    }
    private async Task<FileStream> LockAsync(string file, CancellationToken ct)
    {
        while (true)
        { ct.ThrowIfCancellationRequested(); try { return new(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); } catch (IOException) { await delayAsync(TimeSpan.FromMilliseconds(20), ct); } }
    }
    private sealed record Cursor(DateTimeOffset Read, DateTimeOffset Write, DateTimeOffset ReadCooldown = default, DateTimeOffset WriteCooldown = default);
}
