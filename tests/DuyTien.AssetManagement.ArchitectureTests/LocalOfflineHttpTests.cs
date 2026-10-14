using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Infrastructure.Local;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Assets;
using DuyTien.AssetManagement.Server.Categories;
using DuyTien.AssetManagement.Server.Inventory;
using DuyTien.AssetManagement.Server.Media;
using DuyTien.AssetManagement.Server.Reports;
using DuyTien.AssetManagement.Server.LocalOffline;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace DuyTien.AssetManagement.ArchitectureTests;

[CollectionDefinition("LocalOffline", DisableParallelization = true)] public sealed class LocalOfflineCollection { }
[Collection("LocalOffline")]
public sealed class LocalOfflineHttpTests
{
    [Fact]
    public async Task Actual_Razor_and_API_use_local_cookie_antiforgery_crud_files_and_no_outbound()
    {
        var beforeAttempts = OfflineOutboundGuard.Attempts; using var observation = new OutboundObservation(); using var fixture = await LocalOfflineFixture.Start();
        var root = fixture.Root; var admin = fixture.Admin; fixture.W.Dispose();
        await using var host = await LocalOfflineHost.Start(root); using var client = host.Client();
        // A Google cookie is ignored. New local cookie is returned only after a real Razor POST.
        client.DefaultRequestHeaders.Add("Cookie", "dt_session=untrusted-google-session");
        using (var r = await client.GetAsync("/Assets")) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); client.DefaultRequestHeaders.Remove("Cookie");
        var login = await client.GetStringAsync("/Account/Login"); var field = Field(login, "__RequestVerificationToken");
        using (var missing = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { { "Input.Username", "admin" }, { "Input.Password", "bbbbbbbb" } }))) Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using (var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", field }, { "Input.Username", "admin" }, { "Input.Password", "bbbbbbbb" } })))
        { Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("dt_local_")); Assert.Contains("httponly", cookie.ToLowerInvariant()); Assert.Contains("secure", cookie.ToLowerInvariant()); client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]); }
        foreach (var path in new[] { "/Assets", "/Categories", "/Items", "/Reports", "/Documents", "/Admin/Users", "/Admin/Authorization", "/Admin/SecurityEvents" }) { using var r = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Contains("LocalOffline", await r.Content.ReadAsStringAsync()); }
        using (var r = await client.GetAsync("/Account/LocalSetup")) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        var auth = host.App.Services.GetRequiredService<AuthApplicationService>(); var token = await auth.LoginAsync(new("admin", "bbbbbbbb", AuthClientType.Mobile, false, "fixture-http")); Assert.Equal(LoginStatus.Success, token.Status);
        using var api = host.Client(); api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); api.DefaultRequestHeaders.Accept.Clear(); api.DefaultRequestHeaders.Accept.Add(new("application/json"));
        var assets = host.App.Services.GetRequiredService<AssetCrudService>(); var tree = await ((DuyTien.AssetManagement.Application.Categories.ICategoryCatalogStore)host.Workspace).ReadAsync();
        var request = new AssetWriteRequest(new("GIẢ HTTP create", fixture.Child, "OU-FAKE", "LOC-FAKE"), 0, tree.ActiveTreeVersionId!);
        api.DefaultRequestHeaders.Add("Idempotency-Key", "http-create");
        using (var r = await api.PostAsJsonAsync("/api/assets", request)) Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        using (var r = await api.PostAsJsonAsync("/api/assets", request)) Assert.True(r.IsSuccessStatusCode);
        Assert.Equal(1, (await assets.ListAsync(admin, new())).Total);
        var asset = (await assets.ListAsync(admin, new())).Items.Single().Asset; var media = host.App.Services.GetRequiredService<MediaLibraryService>(); var photo = await media.UploadPhotoAsync(admin, asset.AssetId, MediaBytes.Png(), 1, "GIẢ", true, "http-photo");
        using (var r = await api.GetAsync("/api/files/" + photo.File.FileId + "/content")) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(MediaBytes.Png().Bytes, await r.Content.ReadAsByteArrayAsync()); }
        var rotated = await auth.RefreshAsync(token.RefreshToken); Assert.Equal(RefreshStatus.Success, rotated.Status); Assert.Equal(RefreshStatus.ReuseDetected, (await auth.RefreshAsync(token.RefreshToken)).Status); Assert.Null(await auth.ValidateAccessTokenAsync(rotated.AccessToken));
        Assert.Empty(observation.ExternalHttpHosts); Assert.Equal(beforeAttempts, OfflineOutboundGuard.Attempts);
        var factory = host.App.Services.GetRequiredService<IHttpClientFactory>(); using (var blocked = factory.CreateClient("unexpected")) await Assert.ThrowsAsync<HttpRequestException>(() => blocked.GetAsync("https://must-not-resolve.invalid/")); Assert.Equal(beforeAttempts + 1, OfflineOutboundGuard.Attempts);
        var logout = await client.GetStringAsync("/"); using (var r = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", Field(logout, "__RequestVerificationToken") } }))) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
    }
    [Fact]
    public async Task Benchmark_real_filesystem_snapshot_with_665_nodes_and_20744_Items()
    {
        using var observation = new OutboundObservation(); var build = Stopwatch.StartNew(); using var f = await LocalOfflineFixture.Start(20744, true); var fixtureBuildMs = build.Elapsed.TotalMilliseconds;
        Assert.Equal(665, (await f.Categories.ListAsync(f.Admin)).Total); Assert.Equal(20744, (await f.Inventory.ListAsync(f.Admin, new())).Total);
        var tree = await ((DuyTien.AssetManagement.Application.Categories.ICategoryCatalogStore)f.W).ReadAsync(); Assert.Equal(15, tree.ActiveNodes.Count(n => n.FLevel == 1)); Assert.Equal(98, tree.ActiveNodes.Count(n => n.FLevel == 2)); Assert.Equal(552, tree.ActiveNodes.Count(n => n.FLevel == 3));
        f.W.Dispose(); var startup = Stopwatch.StartNew(); await using var host = await LocalOfflineHost.Start(f.Root); var startupMs = startup.Elapsed.TotalMilliseconds;
        var metrics = new List<object>(); using var c = host.Client(); var sw = Stopwatch.StartNew(); var loginPage = await c.GetStringAsync("/Account/Login"); var loginGetMs = sw.Elapsed.TotalMilliseconds;
        var loginGetWarm = new List<double>(); for (var n = 0; n < 10; n++) { sw.Restart(); await c.GetStringAsync("/Account/Login"); loginGetWarm.Add(sw.Elapsed.TotalMilliseconds); }
        loginGetWarm.Sort();
        var loginFields = new Dictionary<string, string> { { "__RequestVerificationToken", Field(loginPage, "__RequestVerificationToken") }, { "Input.Username", "admin" }, { "Input.Password", "bbbbbbbb" } };
        var warmLogin = new List<double>(); double loginMs = 0; string cookie = "";
        for (var n = 0; n < 11; n++) { sw.Restart(); using var r = await c.PostAsync("/Account/Login", new FormUrlEncodedContent(loginFields)); await r.Content.ReadAsByteArrayAsync(); var elapsed = sw.Elapsed.TotalMilliseconds; Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); cookie = r.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith("dt_local_")).Split(';')[0]; if (n == 0) loginMs = elapsed; else warmLogin.Add(elapsed); }
        warmLogin.Sort(); c.DefaultRequestHeaders.Add("Cookie", cookie);
        foreach (var path in new[] { "/Categories", "/Assets", "/Items", "/Reports?Code=R01", "/Reports?Code=R04" })
        {
            var samples = new List<double>(); double cold = 0; long maxBytes = 0; var phaseSamples = new List<Dictionary<string, double>>();
            for (var i = 0; i < 21; i++) { sw.Restart(); using var r = await c.GetAsync(path); var body = await r.Content.ReadAsByteArrayAsync(); var elapsed = sw.Elapsed.TotalMilliseconds; Assert.Equal(HttpStatusCode.OK, r.StatusCode); if (i == 0) cold = elapsed; else samples.Add(elapsed); maxBytes = Math.Max(maxBytes, body.Length); if (path == "/Items") Assert.True(body.Length < 100000, "Items page must not embed the whole dataset"); await Task.Delay(1); }
            var phaseLines = File.ReadAllLines(Path.Combine(f.Root, "performance.jsonl")).TakeLast(21).Select(line => JsonDocument.Parse(line).RootElement.GetProperty("phases").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("milliseconds").GetDouble())).ToArray();
            var phases = phaseLines.SelectMany(p => p.Keys).Distinct().ToDictionary(k => k, k => new { meanMs = phaseLines.Average(p => p.GetValueOrDefault(k)), maxMs = phaseLines.Max(p => p.GetValueOrDefault(k)) });
            samples.Sort(); metrics.Add(new { path, coldFirstRequestMs = cold, warmCount = samples.Count, p50Ms = samples[(int)Math.Ceiling(samples.Count * .5) - 1], p95Ms = samples[(int)Math.Ceiling(samples.Count * .95) - 1], maxMs = samples.Last(), maxBytes, targetP95Ms = 500, observedTargetMet = samples[(int)Math.Ceiling(samples.Count * .95) - 1] < 500, phases });
        }
        Assert.Empty(observation.ExternalHttpHosts);
        var output = Environment.GetEnvironmentVariable("DUYTIEN_OFFLINE_BENCHMARK_OUTPUT");
        if (output is not null) await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { source = "PRODUCTION_LOCAL_FILESYSTEM_SYNTHETIC_FIXTURE", nodes = 665, f1 = 15, f2 = 98, f3 = 552, items = 20744, fixtureBuildMs, startupMs, workspaceLoadValidateMs = host.Workspace.StartupMilliseconds, startupPhasesMs = host.Workspace.StartupPhases, loginGetColdMs = loginGetMs, loginGetWarmP95Ms = loginGetWarm[(int)Math.Ceiling(loginGetWarm.Count * .95) - 1], loginPostColdMs = loginMs, loginWarmP95Ms = warmLogin[(int)Math.Ceiling(warmLogin.Count * .95) - 1], externalManagedHttpRequests = observation.ExternalHttpHosts.Count, googleCalls = 0, metrics }, new JsonSerializerOptions { WriteIndented = true }));
        var export = Environment.GetEnvironmentVariable("DUYTIEN_OFFLINE_RUNTIME_FIXTURE");
        if (export is not null)
        {
            var destination = Path.GetFullPath(export); if (!Path.GetFileName(destination).StartsWith("FAKE-TASK015-", StringComparison.Ordinal) || Directory.Exists(destination)) throw new InvalidOperationException("Export must be a new explicitly named FAKE-TASK015 workspace.");
            await host.App.Services.GetRequiredService<AuthApplicationService>().LogoutAllAsync(f.Admin); await host.DisposeAsync();
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(f.Root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(f.Root, file); if (relative.EndsWith(".lock") || relative.StartsWith("keys" + Path.DirectorySeparatorChar) || relative.EndsWith(".jsonl")) continue;
                var target = Path.Combine(destination, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
            }
            await File.WriteAllTextAsync(Path.Combine(destination, "FAKE-TASK015-marker.json"), "{\"fixture\":true,\"nodes\":665,\"items\":20744,\"source\":\"synthetic; no company data\"}");
        }

    }
    [Fact]
    public async Task Disk_failure_Razor_keeps_form_key_and_version_until_retry_succeeds()
    {
        using var f = await LocalOfflineFixture.Start(); f.W.Dispose(); await using var h = await LocalOfflineHost.Start(f.Root); using var c = h.Client();
        var login = await h.App.Services.GetRequiredService<AuthApplicationService>().LoginAsync(new("admin", "bbbbbbbb", AuthClientType.Web, false, "form-test")); c.DefaultRequestHeaders.Add("Cookie", "dt_local_" + h.Workspace.WorkspaceId + "=" + login.AccessToken);
        var html = await c.GetStringAsync("/Assets/Create"); var fields = new Dictionary<string, string> { { "__RequestVerificationToken", Field(html, "__RequestVerificationToken") }, { "RequestKey", Field(html, "RequestKey") }, { "TreeVersionId", Field(html, "TreeVersionId") }, { "Form.Name", "GIẢ giữ form" }, { "Form.CategoryId", f.Child }, { "Form.OrgUnitId", "OU-FAKE" }, { "Form.LocationId", "LOC-FAKE" }, { "Form.Condition", "UNKNOWN" } };
        h.Workspace.FailureProbe = phase => { if (phase == "before-manifest") throw new IOException("Injected filesystem failure"); };
        using (var r = await c.PostAsync("/Assets/Create", new FormUrlEncodedContent(fields))) { Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode); var kept = await r.Content.ReadAsStringAsync(); Assert.Equal(fields["RequestKey"], Field(kept, "RequestKey")); Assert.Equal(fields["TreeVersionId"], Field(kept, "TreeVersionId")); Assert.Equal(fields["Form.Name"], Field(kept, "Form.Name")); }
        h.Workspace.FailureProbe = null; using (var r = await c.PostAsync("/Assets/Create", new FormUrlEncodedContent(fields))) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(1, (await h.App.Services.GetRequiredService<AssetCrudService>().ListAsync(f.Admin, new())).Total);
    }
    [Fact]
    public async Task Explicit_demo_initialization_is_idempotent_and_never_runs_on_startup()
    {
        var root = Path.Combine(Path.GetTempPath(), "DuyTienOfflineTest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            await using var h = await LocalOfflineHost.Start(root); var auth = h.App.Services.GetRequiredService<AuthApplicationService>(); Assert.Empty(await auth.ListUsersAsync()); using var c = h.Client(); Assert.Contains("Khởi tạo", await c.GetStringAsync("/Account/LocalSetup"));
            var admin = await auth.BootstrapAdminAsync("aaaaaaaa", "GIẢ setup"); Assert.True(await auth.ChangePasswordAsync(admin.UserId, "aaaaaaaa", "bbbbbbbb")); var seed = h.App.Services.GetRequiredService<LocalDemoInitializer>(); await seed.InitializeAsync(admin.UserId); var generation = h.Workspace.Generation; await seed.InitializeAsync(admin.UserId); Assert.Equal(generation, h.Workspace.Generation);
            var assets = h.App.Services.GetRequiredService<AssetCrudService>(); var choices = await assets.ChoicesAsync(admin.UserId); Assert.NotEmpty(choices.Categories); Assert.Single(choices.OrgUnits); Assert.Single(choices.Locations);
            var req = new AssetWriteRequest(new("GIẢ có thể dùng offline", choices.Categories.First().Id, choices.OrgUnits[0].Id, choices.Locations[0].Id), 0, choices.TreeVersionId!); await assets.CreateAsync(admin.UserId, req, "seed-create"); Assert.Equal(1, (await assets.ListAsync(admin.UserId, new())).Total);
        }
        finally { if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()) + "DuyTienOfflineTest-")) throw new InvalidOperationException(); Directory.Delete(root, true); }
    }
    private static string Field(string html, string name) { var m = Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\""); Assert.True(m.Success, "Missing field " + name); return WebUtility.HtmlDecode(m.Groups[1].Value); }
}
internal sealed class OutboundObservation : EventListener
{
    public System.Collections.Concurrent.ConcurrentBag<string> ExternalHttpHosts { get; } = new();
    protected override void OnEventSourceCreated(EventSource source) { if (source.Name == "System.Net.Http") EnableEvents(source, EventLevel.Informational); }
    protected override void OnEventWritten(EventWrittenEventArgs e)
    { if (e.EventName == "RequestStart" && e.PayloadNames is { } names && e.Payload is { } values) { var i = names.IndexOf("host"); if (i >= 0 && values[i] is string host && host is not ("localhost" or "127.0.0.1" or "::1")) ExternalHttpHosts?.Add(host); } }
}
internal sealed class LocalOfflineHost(WebApplication app, string address) : IAsyncDisposable
{
    public WebApplication App => app; public LocalWorkspace Workspace => app.Services.GetRequiredService<LocalWorkspace>();
    public HttpClient Client() { var c = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(address) }; c.DefaultRequestHeaders.Accept.Add(new("text/html")); return c; }
    public static async Task<LocalOfflineHost> Start(string root)
    {
        var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ApplicationName = typeof(AssetWeb).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory }); b.Logging.ClearProviders(); b.WebHost.UseKestrel();
        b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { { "Storage:Mode", "LocalOffline" }, { "Storage:WorkspacePath", root }, { "Diagnostics:Enabled", "true" }, { "Diagnostics:OutputFile", Path.Combine(root, "performance.jsonl") } }); b.AddLocalOffline();
        b.Services.AddRazorPages().AddMvcOptions(o => o.Filters.Add<PerformanceResultFilter>()); b.Services.AddSingleton<IPasswordHashService, FrameworkPasswordHashService>(); b.Services.AddSingleton(new AuthPolicyOptions()); b.Services.AddSingleton(TimeProvider.System); b.Services.AddSingleton<AuthApplicationService>();
        b.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); b.Services.AddAuthorization();
        var app = b.Build(); _ = app.Services.GetRequiredService<LocalWorkspace>(); app.UseRouting(); app.UsePerformanceTrace(); app.Use(async (c, n) => { using var scope = RequestAuthStore.Begin(); await n(); }); app.UseTask005SessionAuthentication(); app.UseAuthorization();
        app.MapRazorPages(); app.MapTask005AuthApi(); app.MapTask006CategoryApi(); app.MapTask007AssetApi(); app.MapTask008TreeApi(); app.MapTask009MediaApi(); app.MapTask010InventoryApi(); app.MapTask011ReportApi(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync(); return new(app, app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
    }
    private bool disposed;
    public async ValueTask DisposeAsync() { if (disposed) return; disposed = true; await app.StopAsync(); await app.DisposeAsync(); }
}
