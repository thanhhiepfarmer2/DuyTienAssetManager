using System.Globalization;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
namespace DuyTien.AssetManagement.Server.LocalOffline;

public static class PerformanceMiddleware
{
    public static void UsePerformanceTrace(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Diagnostics:Enabled")) return;
        app.Use(async (context, next) =>
        {
            using var trace = PerformanceTrace.Begin();
            context.Response.OnStarting(() => { var value = string.Join(", ", trace.Values.Select(p => $"{p.Key};dur={p.Value.Milliseconds.ToString("F2", CultureInfo.InvariantCulture)};desc=\"count {p.Value.Count}\"")); context.Response.Headers["Server-Timing"] = value; return Task.CompletedTask; });
            using (var total = PerformanceTrace.Measure("request")) await next();
            var output = app.Configuration["Diagnostics:OutputFile"];
            if (output is not null)
            {
                try
                {
                    if (!Path.IsPathFullyQualified(output) || !Path.GetFullPath(output).StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Diagnostics output phải ở temp/private workspace ngoài Git.");
                    var phases = trace.Values.ToDictionary(p => p.Key, p => new { milliseconds = p.Value.Milliseconds, count = p.Value.Count });
                    await File.AppendAllTextAsync(output, JsonSerializer.Serialize(new { status = context.Response.StatusCode, phases }) + Environment.NewLine);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { app.Logger.LogWarning("Performance output unavailable: {Type}", e.GetType().Name); }
            }
        });
    }
}
public sealed class PerformanceResultFilter : IAsyncResultFilter
{ public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next) { using var render = PerformanceTrace.Measure("razor-render"); await next(); } }
