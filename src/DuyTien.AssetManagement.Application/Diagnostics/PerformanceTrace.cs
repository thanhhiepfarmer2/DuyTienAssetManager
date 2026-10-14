using System.Diagnostics;
namespace DuyTien.AssetManagement.Application.Diagnostics;

public sealed class PerformanceTrace : IDisposable
{
    private static readonly AsyncLocal<PerformanceTrace?> Current = new();
    private readonly PerformanceTrace? previous;
    private readonly Dictionary<string, (double Milliseconds, int Count)> values = new(StringComparer.Ordinal);
    private PerformanceTrace() { previous = Current.Value; Current.Value = this; }
    public static PerformanceTrace Begin() => new();
    public static IDisposable Measure(string name) => new Measurement(Current.Value, name);
    public IReadOnlyDictionary<string, (double Milliseconds, int Count)> Values => values;
    public void Dispose() => Current.Value = previous;
    private sealed class Measurement(PerformanceTrace? trace, string name) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        public void Dispose() { if (trace is null) return; lock (trace.values) { var old = trace.values.GetValueOrDefault(name); trace.values[name] = (old.Milliseconds + Stopwatch.GetElapsedTime(started).TotalMilliseconds, old.Count + 1); } }
    }
}
