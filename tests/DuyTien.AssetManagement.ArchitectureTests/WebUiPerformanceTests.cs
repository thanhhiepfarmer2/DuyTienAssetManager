using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Assets;
using DuyTien.AssetManagement.Server.ConnectedCache;
using DuyTien.AssetManagement.Server.LocalOffline;
using DuyTien.AssetManagement.Server.Presentation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace DuyTien.AssetManagement.ArchitectureTests;

// Connected pipeline + compiled Razor + real protected cache. Central data/probe are synthetic,
// never a runtime LocalOffline mode, Google performance claim or browser evidence.
public sealed class WebUiPerformanceTests
{
    [Fact]
    public async Task Large_synthetic_connected_Razor_and_offline_cache_have_separate_timings_and_bounded_pages()
    {
        using var outbound = new OutboundObservation(); var timer = Stopwatch.StartNew();
        using var f = await LocalOfflineFixture.Start(20744, true); var fixtureBuildMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(665, (await f.Categories.ListAsync(f.Admin)).Total);
        timer.Restart(); f.Restart(); var centralSnapshotReloadMs = timer.Elapsed.TotalMilliseconds;
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow); var probe = new SyntheticProbe();
        var auth = new RequestAuthStore(f.W); var stores = new ObservedBusinessStores(f.W, f.W, f.W, f.W, f.W);
        using var cache = new ProtectedQueryCache(Path.Combine(f.Root, "read-cache"), "SYNTHETIC-CENTRAL-ONLY", new EphemeralDataProtectionProvider());
        timer.Restart();
        var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ApplicationName = typeof(AssetWeb).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory });
        b.WebHost.UseKestrel(); b.Logging.ClearProviders(); b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Mode"] = "Google", ["Diagnostics:Enabled"] = "true" });
        b.Services.AddRazorPages().AddMvcOptions(o => { o.Filters.Add<PerformanceResultFilter>(); o.ModelMetadataDetailsProviders.Add(new WebUiMetadataProvider()); });
        b.Services.AddSingleton<IAuthSystemStore>(auth); b.Services.AddSingleton<IPasswordHashService, FrameworkPasswordHashService>(); b.Services.AddSingleton(new AuthPolicyOptions()); b.Services.AddSingleton<TimeProvider>(clock); b.Services.AddSingleton<AuthApplicationService>();
        b.Services.AddSingleton<IInternetProbe>(probe); b.Services.AddSingleton(cache); b.Services.AddSingleton<ConnectionGate>(); b.Services.AddSingleton<ConnectedRuntime>();
        b.Services.AddSingleton(new CategoryCatalogService(stores, auth, f.W, clock, stores, stores, stores));
        b.Services.AddSingleton(new AssetCrudService(stores, stores, auth, f.W, clock));
        b.Services.AddSingleton(new InventoryService(stores, stores, stores, auth, f.W, clock));
        b.Services.AddSingleton(new ReportService(stores, stores, stores, auth, f.W, clock));
        b.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); b.Services.AddAuthorization();
        await using var app = b.Build(); app.Urls.Add("http://127.0.0.1:0"); app.UseRouting(); app.UsePerformanceTrace();
        app.Use(async (c, next) => { using var frame = RequestAuthStore.Begin(); using var data = BusinessReadProof.Begin(true); await next(); });
        app.UseConnectedCache(); app.UseTask005SessionAuthentication(); app.UseConnectedWriteGate(); app.UseAuthorization(); app.MapRazorPages();
        await app.StartAsync(); var startupMs = timer.Elapsed.TotalMilliseconds;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) }; client.DefaultRequestHeaders.Accept.Add(new("text/html"));
        timer.Restart(); var login = await client.GetStringAsync("/Account/Login"); var loginGetColdMs = timer.Elapsed.TotalMilliseconds;
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value), ["Input.Username"] = "admin", ["Input.Password"] = "bbbbbbbb" };
        timer.Restart(); using var loggedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields)); var loginPostMs = timer.Elapsed.TotalMilliseconds; Assert.Equal(HttpStatusCode.Redirect, loggedIn.StatusCode);
        client.DefaultRequestHeaders.Add("Cookie", loggedIn.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("dt_session=", StringComparison.Ordinal)).Split(';')[0]);
        var groups = new List<object>(); var paths = new[] { "/Categories", "/Assets", "/Items", "/Stock", "/Reports?Code=R01", "/Reports?Code=R04" };
        foreach (var offline in new[] { false, true })
        {
            if (offline) { probe.State = InternetState.Disconnected; clock.Advance(TimeSpan.FromSeconds(61)); }
            foreach (var path in paths)
            {
                var samples = new List<double>(); var phases = new Dictionary<string, List<double>>(); double cold = 0; long maxBytes = 0;
                for (var i = 0; i <= 20; i++)
                {
                    timer.Restart(); using var response = await client.GetAsync(path); var bytes = await response.Content.ReadAsByteArrayAsync(); var ms = timer.Elapsed.TotalMilliseconds;
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(offline ? "OFFLINE_READ_ONLY" : "ONLINE", response.Headers.GetValues("X-DuyTien-State").Single());
                    if (i == 0) cold = ms; else samples.Add(ms); maxBytes = Math.Max(maxBytes, bytes.Length);
                    if (path == "/Items") Assert.True(bytes.Length < 100000, "Page must not embed20744 rows");
                    if (response.Headers.TryGetValues("Server-Timing", out var timings)) foreach (Match m in Regex.Matches(string.Join(",", timings), @"([a-z-]+);dur=([0-9.]+)")) { var name = m.Groups[1].Value; if (!phases.TryGetValue(name, out var list)) phases[name] = list = []; list.Add(double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)); }
                }
                samples.Sort(); groups.Add(new { mode = offline ? "OFFLINE_READ_ONLY" : "ONLINE_SYNTHETIC_CENTRAL", path, coldFirstRequestMs = cold, warmCount = samples.Count, p50Ms = samples[9], p95Ms = samples[18], maxMs = samples[^1], maxBytes, observedWarmTargetMet = samples[18] < 500, phases = phases.ToDictionary(p => p.Key, p => new { samples = p.Value.Count, meanMs = p.Value.Average(), maxMs = p.Value.Max() }) });
            }
        }
        Assert.Empty(outbound.ExternalHttpHosts);
        var output = Environment.GetEnvironmentVariable("DUYTIEN_WEB_UI_BENCHMARK_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output)) await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { source = "SYNTHETIC central filesystem + current ConnectedRuntime + compiled Razor + protected read cache", googleLive = false, browser = false, nodes = 665, items = 20744, fixtureBuildMs, centralSnapshotReloadMs, centralSnapshotLoadPhases = f.W.StartupPhases, startupMs, loginGetColdMs, loginPostMs, googleCalls = 0, externalManagedHttpRequests = outbound.ExternalHttpHosts.Count, groups }, new JsonSerializerOptions { WriteIndented = true }));
        await app.StopAsync();
    }
    private sealed class SyntheticProbe : IInternetProbe { public InternetState State = InternetState.Connected; public Task<InternetState> ProbeAsync(CancellationToken ct) => Task.FromResult(State); }
}
