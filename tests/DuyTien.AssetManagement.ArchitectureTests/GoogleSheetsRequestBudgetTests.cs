using System.Net;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleSheetsRequestBudgetTests
{
    [Fact]
    public async Task Read_pacing_survives_new_instance_without_caching_any_business_data()
    {
        using var r = new Rig(); await r.New().WaitAsync(Get(), default); await r.New().WaitAsync(Get(), default);
        Assert.Equal([TimeSpan.FromMilliseconds(1250)], r.Waits); Assert.DoesNotContain("spreadsheet", await File.ReadAllTextAsync(r.Path));
    }
    [Fact]
    public async Task Reads_and_writes_have_separate_quota_slots()
    {
        using var r = new Rig(); var b = r.New(); await b.WaitAsync(Get(), default); await b.WaitAsync(new(HttpMethod.Post, "https://sheets.googleapis.com/v4/spreadsheets/fake:batchUpdate"), default); Assert.Empty(r.Waits);
        await b.WaitAsync(Get(), default); Assert.Equal(TimeSpan.FromMilliseconds(1250), Assert.Single(r.Waits));
    }
    [Fact]
    public async Task Rejected_429_cooldown_is_persisted_without_real_elapsed_wait_or_policy_change()
    {
        using var r = new Rig(); await r.New().RejectedAsync(Get(), HttpStatusCode.TooManyRequests, default); await r.New().WaitAsync(Get(), default);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(r.Waits));
    }
    [Fact]
    public async Task Drive_requests_do_not_consume_sheet_budget()
    { using var r = new Rig(); await r.New().WaitAsync(new(HttpMethod.Get, "https://www.googleapis.com/drive/v3/files/fake"), default); Assert.False(File.Exists(r.Path)); }
    [Fact]
    public async Task Corrupt_budget_fails_closed_and_does_not_reset_file()
    {
        using var r = new Rig(); await File.WriteAllTextAsync(r.Path, "{bad}"); await Assert.ThrowsAsync<ExternalStorageException>(() => r.New().WaitAsync(Get(), default)); Assert.Equal("{bad}", await File.ReadAllTextAsync(r.Path));
    }
    [Fact]
    public async Task Cancellation_does_not_consume_a_new_slot()
    {
        using var r = new Rig(); var b = r.New(); await b.WaitAsync(Get(), default); var before = await File.ReadAllTextAsync(r.Path); using var canceled = new CancellationTokenSource(); canceled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b.WaitAsync(Get(), canceled.Token)); Assert.Equal(before, await File.ReadAllTextAsync(r.Path));
    }
    private static HttpRequestMessage Get() => new(HttpMethod.Get, "https://sheets.googleapis.com/v4/spreadsheets/fake");
    private sealed class Rig : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DuyTien-Budget-Fake-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(directory, "budget.json");
        private readonly Clock clock = new(); public List<TimeSpan> Waits { get; } = [];
        public Rig() => Directory.CreateDirectory(directory);
        public GoogleSheetsRequestBudget New() => new(Path, clock, (wait, ct) => { ct.ThrowIfCancellationRequested(); Waits.Add(wait); clock.Now += wait; return Task.CompletedTask; });
        public void Dispose() { foreach (var p in Directory.EnumerateFiles(directory)) File.Delete(p); Directory.Delete(directory); }
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
}
