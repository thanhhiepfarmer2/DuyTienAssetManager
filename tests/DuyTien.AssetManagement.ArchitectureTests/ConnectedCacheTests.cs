using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.ConnectedCache;
using DuyTien.AssetManagement.Server.LocalOffline;
using DuyTien.AssetManagement.Server.Presentation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class ConnectedCacheTests
{
    [Fact]
    public async Task Protected_snapshot_restart_missing_bytes_and_user_isolation()
    {
        using var fixture = new Disk(); var now = DateTimeOffset.UtcNow;
        var proof = Proof(now); string file;
        using (var cache = fixture.Open())
        {
            Assert.Null(cache.Read("never-online")); await cache.SaveProofAsync("FAKE-session-one", proof, default);
            await cache.SaveViewAsync("FAKE-session-one", proof, "epoch", "/api/assets|json", Encoding.UTF8.GetBytes("GIẢ scoped asset"), "application/json", null, now, default);
            var ticket = cache.Read("FAKE-session-one")!; file = ticket.Views.Single().File;
            Assert.Equal("GIẢ scoped asset", Encoding.UTF8.GetString(cache.Bytes("FAKE-session-one", ticket.Views.Single())!));
            Assert.Null(cache.Read("FAKE-session-two"));
            Assert.Throws<IOException>(() => fixture.Open());
            foreach (var path in Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith("cache.lock"))) Assert.DoesNotContain("GIẢ scoped asset", Encoding.UTF8.GetString(File.ReadAllBytes(path)));
        }
        using (var cache = fixture.Open())
        {
            var ticket = cache.Read("FAKE-session-one")!; Assert.Single(ticket.Views);
            File.Delete(Directory.GetFiles(fixture.Root, file, SearchOption.AllDirectories).Single());
            Assert.Null(cache.Bytes("FAKE-session-one", ticket.Views.Single()));
            await cache.RevokeAsync("FAKE-session-one", default);
        }
        using (var cache = fixture.Open()) Assert.False(ProtectedQueryCache.Valid(cache.Read("FAKE-session-one"), now));
    }
    [Fact]
    public async Task Authority_shrink_purges_old_bytes_and_old_request_cannot_restore_cache()
    {
        using var f = new Disk(); using var cache = f.Open(); var now = DateTimeOffset.UtcNow; var a = Proof(now);
        await cache.SaveProofAsync("FAKE-a", a, default); await cache.SaveViewAsync("FAKE-a", a, "e", "/Assets|html", [1, 2], "text/html", null, now, default);
        Assert.Single(Directory.GetFiles(f.Root, "*.view", SearchOption.AllDirectories));
        var narrow = a with { AuthorityHash = "narrow", VerifiedAt = now.AddSeconds(1) };
        await cache.SaveProofAsync("FAKE-a", narrow, default); Assert.Empty(cache.Read("FAKE-a")!.Views); Assert.Empty(Directory.GetFiles(f.Root, "*.view", SearchOption.AllDirectories));
        await cache.SaveProofAsync("FAKE-a", a, default); await cache.SaveViewAsync("FAKE-a", a, "e", "/Assets|html", [3], "text/html", null, now, default);
        Assert.Equal("narrow", cache.Read("FAKE-a")!.Proof.AuthorityHash); Assert.Empty(cache.Read("FAKE-a")!.Views);
        await cache.SaveProofAsync("FAKE-a", narrow with { SecurityVersion = 2, VerifiedAt = now.AddSeconds(2) }, default);
        await cache.SaveProofAsync("FAKE-a", a with { VerifiedAt = now.AddSeconds(3) }, default);
        Assert.Equal(2, cache.Read("FAKE-a")!.Proof.SecurityVersion);
        Assert.False(ProtectedQueryCache.Valid(cache.Read("FAKE-a"), now.AddHours(1)));
    }
    [Fact]
    public async Task Probe_coalesces_and_backs_off_and_service_unknown_is_not_offline()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow); var probe = new FakeProbe(); var gate = new ConnectionGate(probe, clock);
        await Task.WhenAll(Enumerable.Range(0, 15).Select(_ => gate.CheckAsync(default))); Assert.Equal(1, probe.Calls);
        probe.State = InternetState.Disconnected; Assert.Equal(InternetState.Connected, await gate.CheckAsync(default));
        clock.Advance(TimeSpan.FromSeconds(16)); Assert.Equal(InternetState.Disconnected, await gate.CheckAsync(default));
        await Assert.ThrowsAsync<DuyTien.AssetManagement.Application.ExternalStorage.ExternalStorageException>(() => gate.EnsureAllowedAsync(default));
        clock.Advance(TimeSpan.FromSeconds(3)); probe.State = InternetState.Indeterminate; Assert.Equal(InternetState.Indeterminate, await gate.CheckAsync(default));
    }
    [Theory]
    [InlineData("/api/assets")][InlineData("/api/items/imports")][InlineData("/api/files/id/reconcile")][InlineData("/api/categories/tree")][InlineData("/Admin/Users/Create")][InlineData("/api/auth/change-password")][InlineData("/Account/LocalSetup")]
    public async Task Direct_mutation_denied_offline_before_any_central_access(string path)
    {
        await using var host = await Host.Start(); await host.Warm(); host.Offline(); var before = host.Store.Calls;
        var r = await host.Client.PostAsJsonAsync(path, new { payload = "FAKE" });
        Assert.True((int)r.StatusCode is 423 or 410); Assert.Equal(before, host.Store.Calls); Assert.Equal(0, host.Writes);
    }
    [Fact]
    public async Task First_login_requires_online_and_unknown_network_never_uses_cached_data()
    {
        await using var h = await Host.Start(); h.Offline(); h.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync("/api/assets")).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await h.Client.PostAsJsonAsync("/api/auth/login", new { username = "FAKE" })).StatusCode);
        h.Network(InternetState.Connected); await h.Warm(); h.Network(InternetState.Indeterminate);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Client.GetAsync("/api/assets")).StatusCode);
    }
    [Fact]
    public async Task First_online_login_without_cookie_or_cache_is_not_blocked_by_read_only_guard()
    {
        await using var h = await Host.Start();
        Assert.Null(h.Cache.Read(h.Token));
        Assert.Null(h.Client.DefaultRequestHeaders.Authorization);
        var response = await h.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "FAKE-final-password", AuthClientType.Web, false, "FAKE-first-online"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.Equal(LoginStatus.Success, login!.Status); Assert.NotNull(login.AccessToken);
        // No central proof has been cached until the issued session is validated online.
        Assert.Null(h.Cache.Read(login.AccessToken));
        h.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync("/api/assets")).StatusCode);
    }
    [Fact]
    public async Task Online_offline_reconnect_requires_fresh_read_before_write_and_receives_new_data()
    {
        await using var h = await Host.Start(); await h.Warm(); h.Offline(); var before = h.Store.Calls;
        var cached = await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.OK, cached.StatusCode); Assert.Equal("OFFLINE_READ_ONLY", cached.Headers.GetValues("X-DuyTien-State").Single()); Assert.Contains("1", await cached.Content.ReadAsStringAsync()); Assert.Equal(before, h.Store.Calls);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Client.GetAsync("/api/assets?q=uncached")).StatusCode);
        var search = await h.Client.GetFromJsonAsync<JsonElement>("/Offline?q=FAKE"); Assert.True(search.GetProperty("partial").GetBoolean()); Assert.Equal(1, search.GetProperty("total").GetInt32());
        h.Revision = 2; h.Network(InternetState.Connected);
        Assert.Equal(HttpStatusCode.Conflict, (await h.Write("same-key")).StatusCode); Assert.Equal(0, h.Writes);
        var fresh = await h.Client.GetAsync("/api/assets"); Assert.Contains("2", await fresh.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await h.Write("same-key")).StatusCode); Assert.Equal(1, h.Writes);
    }
    [Theory]
    [InlineData("revoke")][InlineData("lock")][InlineData("security-version")]
    public async Task Offline_revoke_or_lock_is_detected_before_reenabling_writes(string mode)
    {
        await using var h = await Host.Start(); await h.Warm(); h.Offline();
        await h.Store.Inner.MutateAsync(s => { if (mode == "revoke") s.Sessions[0] = s.Sessions[0] with { RevokedAt = h.Clock.GetUtcNow() }; else s.Users[0] = s.Users[0] with { State = mode == "lock" ? AuthUserState.Locked : AuthUserState.Active, SecurityVersion = s.Users[0].SecurityVersion + 1 }; return true; });
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync("/api/assets")).StatusCode);
        h.Network(InternetState.Connected); Assert.Equal(HttpStatusCode.Conflict, (await h.Write("fake-key")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync("/api/assets")).StatusCode); Assert.True(h.Cache.Read(h.Token)!.Revoked); Assert.Equal(0, h.Writes);
    }
    [Fact]
    public async Task Expiry_offline_does_not_extend_and_logout_revokes_across_cache_restart()
    {
        await using var h = await Host.Start(); await h.Warm(); h.Offline();
        Assert.Equal(HttpStatusCode.NoContent, (await h.Client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync("/api/assets")).StatusCode);
        Assert.True(h.Cache.Read(h.Token)!.Revoked);
    }
    [Fact]
    public async Task Idle_expiry_cannot_be_extended_by_offline_reads()
    {
        await using var h = await Host.Start(); await h.Warm(); h.Offline(); var expires = h.Cache.Read(h.Token)!.Proof.ExpiresAt;
        h.Clock.Advance(TimeSpan.FromMinutes(31)); Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync("/api/assets")).StatusCode); Assert.Equal(expires, h.Cache.Read(h.Token)!.Proof.ExpiresAt);
        Assert.Equal(HttpStatusCode.NoContent, (await h.Client.PostAsync("/api/auth/logout", null)).StatusCode); Assert.True(h.Cache.Read(h.Token)!.Revoked);
    }
    [Fact]
    public async Task Service_503_does_not_fall_back_and_unknown_write_keeps_key_and_blocks_blind_retry()
    {
        await using var h = await Host.Start(); await h.Warm(); h.FailRead = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Client.GetAsync("/api/assets")).StatusCode); Assert.Equal(InternetState.Connected, h.Gate.State);
        Assert.Equal("SERVICE_UNAVAILABLE", (await h.Client.GetFromJsonAsync<JsonElement>("/api/connection")).GetProperty("state").GetString());
        h.FailRead = false; await h.Client.GetAsync("/api/assets"); h.FailWrite = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Write("original-key")).StatusCode); Assert.Equal("original-key", h.Cache.Read(h.Token)!.Pending!.Key);
        h.FailWrite = false; await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.Conflict, (await h.Write("new-key")).StatusCode); Assert.Equal(HttpStatusCode.Conflict, (await h.Write("original-key")).StatusCode);
        await h.Cache.UpdatePendingAsync(h.Token, h.Cache.Read(h.Token)!.Pending! with { JournalConfirmed = true }, default);
        Assert.Equal(HttpStatusCode.Conflict, (await h.Write("original-key", "DIFFERENT")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await h.Write("original-key")).StatusCode); Assert.Null(h.Cache.Read(h.Token)!.Pending);
    }
    [Theory]
    [InlineData(401)][InlineData(403)][InlineData(429)][InlineData(503)]
    public async Task Probe_HTTP_errors_mean_transport_exists_not_offline(int status)
    {
        var handler = new ProbeHandler(() => new HttpResponseMessage((HttpStatusCode)status));
        Assert.Equal(InternetState.Connected, await new InternetProbe(new ProbeClients(handler), () => true).ProbeAsync(default)); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Probe_needs_independent_transport_evidence_and_DNS_failure_is_indeterminate()
    {
        var down = new ProbeHandler(() => throw new HttpRequestException("FAKE network down", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.NetworkUnreachable)));
        Assert.Equal(InternetState.Disconnected, await new InternetProbe(new ProbeClients(down), () => true).ProbeAsync(default)); Assert.Equal(3, down.Calls);
        var dns = new ProbeHandler(() => throw new HttpRequestException("FAKE DNS", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound)));
        Assert.Equal(InternetState.Indeterminate, await new InternetProbe(new ProbeClients(dns), () => true).ProbeAsync(default));
    }
    [Fact]
    public async Task Different_online_user_cannot_read_previous_users_cached_queries()
    {
        await using var h = await Host.Start(); await h.Warm(); var auth = h.app.Services.GetRequiredService<AuthApplicationService>();
        var other = await auth.CreateUserAsync(h.Store.Inner.Snapshot.Users.Single().UserId, "fake_other", "GIẢ userB");
        await auth.ChangePasswordAsync(other.UserId, other.TemporaryPassword, "FAKE-other-password");
        var login = await auth.LoginAsync(new("fake_other", "FAKE-other-password", AuthClientType.Web, false, "FAKE-other"));
        h.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken); h.Offline();
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync("/api/assets")).StatusCode);
        Assert.Null(h.Cache.Read(login.AccessToken)); Assert.NotNull(h.Cache.Read(h.Token));
    }
    [Fact]
    public async Task Unicode_index_filters_only_cached_snapshots_and_missing_bytes_are_explicit()
    {
        using var f = new Disk(); using var c = f.Open(); var now = DateTimeOffset.UtcNow; var proof = Proof(now);
        await c.SaveProofAsync("FAKE-index", proof, default);
        await c.SaveViewAsync("FAKE-index", proof, "e", "/api/assets|json", JsonSerializer.SerializeToUtf8Bytes(new { name = "GIẢ Động cơ" }), "application/json", null, now, default);
        Assert.Contains("DONG CO", c.SearchText("FAKE-index", c.Read("FAKE-index")!.Views.Single()));
        await c.SaveViewAsync("FAKE-index", proof, "e", "/api/files/FAKE/content|json", [1, 2, 3], "application/pdf", "attachment; filename=fake.pdf", now, default);
        var view = c.Read("FAKE-index")!.Views.Last(); Assert.Equal(new byte[] { 1, 2, 3 }, c.Bytes("FAKE-index", view));
        var file = Directory.GetFiles(f.Root, view.File, SearchOption.AllDirectories).Single(); var bytes = File.ReadAllBytes(file); bytes[^1] ^= 1; File.WriteAllBytes(file, bytes);
        // A new reader verifies DataProtection integrity instead of trusting a disk manifest.
        c.Dispose(); using var restarted = f.Open(); Assert.Null(restarted.Bytes("FAKE-index", restarted.Read("FAKE-index")!.Views.Last()));
    }
    [Fact]
    public async Task Warm_online_and_offline_timings_are_separate_fake_central_samples()
    {
        await using var h = await Host.Start(); await h.Warm(); var groups = new List<object>();
        foreach (var offline in new[] { false, true })
        {
            if (offline) h.Offline(); var samples = new List<double>(); var before = h.Store.Calls;
            for (int i = 0; i < 20; i++) { var clock = System.Diagnostics.Stopwatch.StartNew(); var r = await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.OK, r.StatusCode); await r.Content.ReadAsByteArrayAsync(); samples.Add(clock.Elapsed.TotalMilliseconds); }
            groups.Add(new { mode = offline ? "OFFLINE_READ_ONLY" : "ONLINE", source = "FAKE central/auth; real loopback Kestrel and protected filesystem cache", samples = samples.Count, p95Ms = samples.OrderBy(x => x).ElementAt(18), centralAuthStoreCalls = h.Store.Calls - before });
            if (offline) Assert.Equal(before, h.Store.Calls);
        }
        var target = Environment.GetEnvironmentVariable("DUYTIEN_CONNECTED_BENCHMARK"); if (!string.IsNullOrWhiteSpace(target)) await File.WriteAllTextAsync(target, JsonSerializer.Serialize(new { version = "REQ-002 v2", generatedAt = DateTimeOffset.UtcNow, synthetic = true, googleLive = false, groups }, new JsonSerializerOptions { WriteIndented = true }));
    }
    [Fact]
    public async Task Legacy_HTML_remains_readable_without_old_scripts_forms_or_fingerprint_and_preserves_pending_and_file_bytes()
    {
        await using var h = await Host.Start(); await h.Warm(); var proof = h.Cache.Read(h.Token)!.Proof;
        var old = Encoding.UTF8.GetBytes("<html><head><link href='/css/retired.hash.css'></head><body><h1>GIẢ tài sản cũ</h1><script>PRIVATE-OLD-SCRIPT</script><form><input value='PRIVATE-HIDDEN-FIELD'></form><p>Chưa duyệt</p></body></html>");
        await h.Cache.SaveViewAsync(h.Token, proof, h.Gate.Epoch, "/Assets|html", old, "text/html", null, h.Clock.GetUtcNow(), default);
        var ticket = h.Cache.Read(h.Token)!; var index = Array.FindIndex(ticket.Views, v => v.Key == "/Assets|html");
        // Own synthetic ticket injection models the pre-version serialized format, not a personal browser profile.
        ticket.Views[index] = ticket.Views[index] with { PresentationVersion = null };
        await h.Cache.NotReadyAsync(h.Token, default); var originalFile = ticket.Views[index].File;
        await h.Cache.UpdatePendingAsync(h.Token, new("POST", "/api/assets", "FAKE-kept-key", "FAKE-kept-payload"), default);
        await h.Cache.SaveViewAsync(h.Token, proof, h.Gate.Epoch, "/Files/Content/FAKE|html", [1, 2, 3], "application/pdf", null, h.Clock.GetUtcNow(), default);
        h.Offline(); var before = h.Store.Calls;
        using var response = await h.Client.GetAsync("/Assets"); response.EnsureSuccessStatusCode(); var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal("LEGACY_READ_ONLY", response.Headers.GetValues("X-DuyTien-Ui-Revision").Single());
        Assert.Contains("GIẢ tài sản cũ", html); Assert.Contains("giao diện cũ", html); Assert.DoesNotContain("PRIVATE-OLD-SCRIPT", html); Assert.DoesNotContain("PRIVATE-HIDDEN-FIELD", html); Assert.DoesNotContain("retired.hash", html); Assert.DoesNotContain("<form", html); Assert.Contains("href=\"/Files/Content/FAKE\"", html);
        Assert.Equal(before, h.Store.Calls); Assert.Equal(old, h.Cache.Bytes(h.Token, ticket.Views[index]));
        Assert.Equal("FAKE-kept-key", h.Cache.Read(h.Token)!.Pending!.Key);
        Assert.Contains(h.Cache.Read(h.Token)!.Views, v => v.File == originalFile);
        using var file = await h.Client.GetAsync("/Files/Content/FAKE"); Assert.Equal(new byte[] { 1, 2, 3 }, await file.Content.ReadAsByteArrayAsync());
    }
    [Fact]
    public async Task Refreshing_one_HTML_cache_view_stamps_new_revision_without_purging_other_views_or_pending()
    {
        using var disk = new Disk(); using var cache = disk.Open(); var now = DateTimeOffset.UtcNow; var proof = Proof(now);
        await cache.SaveProofAsync("FAKE-ui-version", proof, default);
        await cache.UpdatePendingAsync("FAKE-ui-version", new("POST", "/Assets", "FAKE-key", "FAKE-hash"), default);
        await cache.SaveViewAsync("FAKE-ui-version", proof, "e", "/api/assets|json", [1], "application/json", null, now, default);
        await cache.SaveViewAsync("FAKE-ui-version", proof, "e", "/Assets|html", Encoding.UTF8.GetBytes("<body>FAKE old</body>"), "text/html", null, now, default);
        var old = cache.Read("FAKE-ui-version")!; var other = old.Views.First().File; old.Views[1] = old.Views[1] with { PresentationVersion = null };
        await cache.SaveViewAsync("FAKE-ui-version", proof, "e", "/Assets|html", Encoding.UTF8.GetBytes("<body>FAKE new</body>"), "text/html", null, now, default);
        var current = cache.Read("FAKE-ui-version")!; Assert.Equal(2, current.Views.Length); Assert.Contains(current.Views, v => v.File == other);
        Assert.Equal(WebUi.CacheRevision, current.Views.Single(v => v.Key == "/Assets|html").PresentationVersion); Assert.Equal("FAKE-key", current.Pending!.Key);
        var legacy = JsonSerializer.Deserialize<CachedView>("{\"Key\":\"/Assets|html\",\"File\":\"fake.view\",\"ContentType\":\"text/html\",\"Disposition\":null,\"AsOf\":\"2026-10-15T00:00:00Z\",\"Size\":1}")!;
        Assert.Null(legacy.PresentationVersion); Assert.False(CachedHtmlPresentation.IsCurrent(legacy));
    }
    private sealed class ProbeClients(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, false); }
    private sealed class ProbeHandler(Func<HttpResponseMessage> create) : HttpMessageHandler { public int Calls; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Calls++; return Task.FromResult(create()); } }
    [Fact]
    public async Task Unknown_write_survives_logout_new_online_session_and_cache_restart()
    {
        using var disk = new Disk(); var now = DateTimeOffset.UtcNow; var first = Proof(now);
        using (var cache = disk.Open())
        {
            await cache.SaveProofAsync("FAKE-old-session", first, default);
            await cache.UpdatePendingAsync("FAKE-old-session", new("POST", "/api/assets", "FAKE-original-key", "FAKE-payload-hash"), default);
            await cache.RevokeAsync("FAKE-old-session", default);
        }
        using (var cache = disk.Open())
        {
            await cache.SaveProofAsync("FAKE-new-session", first with { SessionId = "new-session", VerifiedAt = now.AddSeconds(2) }, default);
            Assert.Equal("FAKE-original-key", cache.Read("FAKE-new-session")!.Pending!.Key);
            await cache.SaveProofAsync("FAKE-other-user", first with { UserId = "FAKE-other", SessionId = "other-session" }, default);
            Assert.Null(cache.Read("FAKE-other-user")!.Pending);
        }
    }
    [Theory]
    [InlineData(true, false)][InlineData(false, false)][InlineData(true, true)]
    public async Task Reconciliation_uses_real_asset_journal_before_allowing_same_key_replay(bool commitFirst, bool mediaCase)
    {
        using var fixture = await LocalOfflineFixture.Start(items: 0); using var disk = new Disk(); using var cache = disk.Open(); var probe = new FakeProbe(); var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" }); b.WebHost.UseKestrel(); b.Logging.ClearProviders(); b.Services.AddAntiforgery();
        b.Services.AddSingleton<IAuthSystemStore>(new RequestAuthStore(fixture.W)); b.Services.AddSingleton<IPasswordHashService, FrameworkPasswordHashService>(); b.Services.AddSingleton(new AuthPolicyOptions()); b.Services.AddSingleton<TimeProvider>(clock); b.Services.AddSingleton<AuthApplicationService>();
        b.Services.AddSingleton<IInternetProbe>(probe); b.Services.AddSingleton<ConnectionGate>(); b.Services.AddSingleton(cache); b.Services.AddSingleton<ConnectedRuntime>();
        b.Services.AddSingleton(fixture.Media); b.Services.AddSingleton<DuyTien.AssetManagement.Application.Assets.IAssetCatalogStore>(fixture.W); b.Services.AddSingleton<DuyTien.AssetManagement.Application.Categories.ICategoryCatalogStore>(fixture.W); b.Services.AddSingleton<DuyTien.AssetManagement.Application.Inventory.IInventoryStore>(fixture.W); b.Services.AddSingleton<DuyTien.AssetManagement.Application.Media.IMediaCatalogStore>(fixture.W); b.Services.AddSingleton<DuyTien.AssetManagement.Application.Concurrency.ISingleWriterCoordinator>(fixture.W);
        b.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); b.Services.AddAuthorization();
        await using var app = b.Build(); app.Urls.Add("http://127.0.0.1:0"); app.UseRouting(); app.Use(async (c, next) => { using var frame = RequestAuthStore.Begin(); await next(); }); app.UseConnectedCache(); app.UseTask005SessionAuthentication(); app.UseConnectedWriteGate(); app.UseAuthorization();
        var fail = true;
        var owner = mediaCase ? (await fixture.Assets.CreateAsync(fixture.Admin, await fixture.Request(), "FAKE-owner")).Asset : null;
        var postPath = mediaCase ? "/api/assets/" + owner!.AssetId + "/photos" : "/api/assets";
        app.MapGet("/api/assets", async () => Results.Ok((await ((DuyTien.AssetManagement.Application.Assets.IAssetCatalogStore)fixture.W).ReadAsync()).Assets.ToArray())).RequireAuthorization();
        app.MapPost(postPath, async (DuyTien.AssetManagement.Application.Assets.AssetWriteRequest input, HttpContext c) =>
        {
            if (mediaCase)
            {
                var commits = 0; if (fail) fixture.W.FailureProbe = phase => { if (phase == "before-manifest" && ++commits == 2) throw new IOException("FAKE after bytes, before AVAILABLE"); };
                try { return Results.Ok(await fixture.Media.UploadPhotoAsync(fixture.Admin, owner!.AssetId, MediaBytes.Png(), 1, "GIẢ media reconcile", true, c.Request.Headers["Idempotency-Key"].ToString())); }
                catch (DuyTien.AssetManagement.Application.Media.MediaPendingException) { return Results.StatusCode(503); }
                finally { fixture.W.FailureProbe = null; }
            }
            if (!commitFirst && fail) return Results.StatusCode(503);
            var result = await fixture.Assets.CreateAsync(fixture.Admin, input, c.Request.Headers["Idempotency-Key"].ToString());
            return fail ? Results.StatusCode(503) : Results.Ok(result);
        }).RequireAuthorization(); app.MapConnectedCache(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var token = (await app.Services.GetRequiredService<AuthApplicationService>().LoginAsync(new("admin", "bbbbbbbb", AuthClientType.Web, false, "FAKE-central"))).AccessToken!; client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/assets")).StatusCode); var request = await fixture.Request("GIẢ unknown-write");
        async Task<HttpResponseMessage> Write() { var message = new HttpRequestMessage(HttpMethod.Post, postPath) { Content = JsonContent.Create(request) }; message.Headers.Add("Idempotency-Key", "FAKE-durable-key"); return await client.SendAsync(message); }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Write()).StatusCode); Assert.NotNull(cache.Read(token)!.Pending);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/assets")).StatusCode);
        var reconcile = await client.PostAsync("/api/connection/reconcile", null);
        Assert.Equal(commitFirst ? HttpStatusCode.OK : HttpStatusCode.Conflict, reconcile.StatusCode); fail = false;
        var retry = await Write(); Assert.Equal(commitFirst ? HttpStatusCode.OK : HttpStatusCode.Conflict, retry.StatusCode);
        var stored = await ((DuyTien.AssetManagement.Application.Assets.IAssetCatalogStore)fixture.W).ReadAsync(); Assert.Equal(commitFirst ? 1 : 0, stored.Assets.Count); Assert.Equal(commitFirst ? 1 : 0, stored.Operations.Count);
        if (mediaCase) { var files = await ((DuyTien.AssetManagement.Application.Media.IMediaCatalogStore)fixture.W).ReadAsync(); Assert.Single(files.Files); Assert.Equal("AVAILABLE", files.Files.Single().State); Assert.Single(Directory.GetFiles(Path.Combine(fixture.Root, "blobs"))); }
        if (commitFirst) { Assert.True((await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean()); Assert.Null(cache.Read(token)!.Pending); }
    }
    [Fact]
    public async Task Google_and_CLI_transport_gate_blocks_before_OAuth_or_business_transport()
    {
        var probe = new FakeProbe { State = InternetState.Disconnected }; using var gate = new ConnectionGate(probe, TimeProvider.System); var tokens = new ProbeTokens(); var transport = new ProbeHandler(() => new HttpResponseMessage(HttpStatusCode.OK)); using var client = new HttpClient(transport);
        var executor = new DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleApiRequestExecutor(new(), tokens, client, connectivity: gate);
        foreach (var semantics in Enum.GetValues<DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleRequestSemantics>()) await Assert.ThrowsAsync<DuyTien.AssetManagement.Application.ExternalStorage.ExternalStorageException>(() => executor.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "https://sheets.googleapis.com/FAKE"), semantics));
        Assert.Equal(0, tokens.Calls); Assert.Equal(0, transport.Calls);
    }
    [Fact]
    public async Task Cookie_offline_logout_requires_antiforgery_and_clears_offline_access()
    {
        await using var h = await Host.Start(); await h.Warm(); h.Offline(); h.Client.DefaultRequestHeaders.Authorization = null; h.Client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + h.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode); Assert.False(h.Cache.Read(h.Token)!.Revoked);
        h.Client.DefaultRequestHeaders.Accept.Clear(); h.Client.DefaultRequestHeaders.Accept.Add(new("text/html"));
        var html = await h.Client.GetStringAsync("/Offline"); var match = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\""); Assert.True(match.Success);
        await h.Client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value) })); Assert.True(h.Cache.Read(h.Token)!.Revoked);
    }
    private sealed class ProbeTokens : DuyTien.AssetManagement.Infrastructure.Google.OAuth.IGoogleAccessTokenProvider { public int Calls; public Task<string> GetAccessTokenAsync(CancellationToken ct = default) { Calls++; return Task.FromResult("FAKE_NOT_AN_OAUTH_CREDENTIAL"); } public void Invalidate() { } }
    [Fact]
    public async Task Home_is_not_business_refresh_and_authority_change_is_rechecked_before_mutation()
    {
        await using var h = await Host.Start(); h.Client.DefaultRequestHeaders.Authorization = new("Bearer", h.Token);
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await h.Write("home-only")).StatusCode); Assert.Equal(0, h.Writes);
        await h.Client.GetAsync("/api/assets");
        await h.Store.Inner.MutateAsync(s => { s.RoleAssignmentScopes[0] = s.RoleAssignmentScopes[0] with { Version = s.RoleAssignmentScopes[0].Version + 1 }; return true; });
        Assert.Equal(HttpStatusCode.Conflict, (await h.Write("changed-authority")).StatusCode); Assert.Equal(0, h.Writes); Assert.Empty(h.Cache.Read(h.Token)!.Views);
        await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.OK, (await h.Write("fresh-authority")).StatusCode);
    }
    [Fact]
    public async Task Head_does_not_erase_GET_and_offline_file_availability_checks_actual_bytes()
    {
        await using var h = await Host.Start(); await h.Warm(); var original = h.Cache.Read(h.Token)!.Views.Single().Size;
        await h.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/assets")); Assert.Equal(original, h.Cache.Read(h.Token)!.Views.Single().Size);
        Assert.Equal(new byte[] { 1, 2, 3 }, await h.Client.GetByteArrayAsync("/api/files/FAKE/content")); h.Offline(); var before = h.Store.Calls;
        Assert.Equal(new byte[] { 1, 2, 3 }, await h.Client.GetByteArrayAsync("/api/files/FAKE/content"));
        var info = await h.Client.GetFromJsonAsync<JsonElement>("/api/connection/files"); Assert.True(info.GetProperty("files")[0].GetProperty("offlineAvailable").GetBoolean());
        var file = h.Cache.Read(h.Token)!.Views.Single(v => v.Key.StartsWith("/api/files/")); File.Delete(Directory.GetFiles(h.disk.Root, file.File, SearchOption.AllDirectories).Single());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Client.GetAsync("/api/files/FAKE/content")).StatusCode);
        info = await h.Client.GetFromJsonAsync<JsonElement>("/api/connection/files"); Assert.False(info.GetProperty("files")[0].GetProperty("offlineAvailable").GetBoolean()); Assert.Equal(before, h.Store.Calls);
    }
    [Fact]
    public async Task Existing_API_bearer_sessions_keep_online_writes_and_original_access_expiry()
    {
        await using var h = await Host.Start(); var auth = h.app.Services.GetRequiredService<AuthApplicationService>();
        var login = await auth.LoginAsync(new("admin", "FAKE-final-password", AuthClientType.Mobile, false, "FAKE-existing-API"));
        h.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync("/api/assets")).StatusCode);
        Assert.Equal(login.AccessExpiresAt, h.Cache.Read(login.AccessToken)!.Proof.ExpiresAt);
        Assert.Equal(HttpStatusCode.OK, (await h.Write("existing-api-key")).StatusCode);
        h.Offline(); Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync("/api/assets")).StatusCode);
        h.Clock.Advance(TimeSpan.FromMinutes(16)); Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync("/api/assets")).StatusCode);
    }
    [Fact]
    public async Task WAN_timeout_with_link_up_needs_all_probes_and_center_probe_prevents_false_offline()
    {
        var timedOut = new ProbeHandler(() => throw new TaskCanceledException("FAKE timeout"));
        Assert.Equal(InternetState.Disconnected, await new InternetProbe(new ProbeClients(timedOut), () => true).ProbeAsync(default)); Assert.Equal(3, timedOut.Calls);
        var calls = 0; var centerReachable = new ProbeHandler(() => ++calls < 3 ? throw new TaskCanceledException("FAKE unrelated probe blocked") : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.Equal(InternetState.Connected, await new InternetProbe(new ProbeClients(centerReachable), () => true).ProbeAsync(default)); Assert.Equal(3, centerReachable.Calls);
        calls = 0; var selectedHostsBlocked = new ProbeHandler(() => ++calls < 3 ? throw new HttpRequestException("FAKE selected hosts blocked", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.NetworkUnreachable)) : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        Assert.Equal(InternetState.Connected, await new InternetProbe(new ProbeClients(selectedHostsBlocked), () => true).ProbeAsync(default)); Assert.Equal(3, selectedHostsBlocked.Calls);
    }
    [Fact]
    public async Task Request_metadata_memo_reduces_duplicate_calls_but_never_reuses_values_or_next_request()
    {
        var handler = new ProbeHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { sheets = new[] { "FAKE" } }) }); using var client = new HttpClient(handler); var tokens = new ProbeTokens();
        var executor = new DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleApiRequestExecutor(new(), tokens, client);
        async Task Read(string path) { using var response = await executor.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "https://sheets.googleapis.com/v4/spreadsheets/FAKE" + path), DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleRequestSemantics.SafeRead); Assert.True(response.IsSuccessStatusCode); }
        using (DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleMetadataReadMemo.Begin(true))
        {
            await Read("?fields=sheets.properties"); await Read("?fields=sheets.properties"); Assert.Equal(1, handler.Calls);
            await Read("/values/Assets"); await Read("/values/Assets"); Assert.Equal(3, handler.Calls);
            using var write = await executor.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, "https://sheets.googleapis.com/v4/spreadsheets/FAKE:batchUpdate"), DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleRequestSemantics.IdempotentWrite);
            await Read("?fields=sheets.properties"); Assert.Equal(5, handler.Calls);
        }
        using (DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleMetadataReadMemo.Begin(true)) { await Read("?fields=sheets.properties"); Assert.Equal(6, handler.Calls); }
        using (DuyTien.AssetManagement.Infrastructure.Google.Http.GoogleMetadataReadMemo.Begin(false)) { await Read("?fields=sheets.properties"); await Read("?fields=sheets.properties"); Assert.Equal(8, handler.Calls); }
    }
    [Fact]
    public async Task Failed_auth_before_business_handler_does_not_invent_an_unknown_business_write()
    {
        await using var h = await Host.Start(); await h.Warm(); h.Store.Fail = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Write("FAKE-not-sent")).StatusCode); Assert.Equal(0, h.Writes); Assert.Null(h.Cache.Read(h.Token)!.Pending);
        h.Store.Fail = false; await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.OK, (await h.Write("FAKE-not-sent")).StatusCode);
    }
    [Fact]
    public async Task Known_offline_reads_do_not_wait_for_background_probe_timeout()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow); var probe = new SlowProbe(); using var gate = new ConnectionGate(probe, clock); using var monitor = new ConnectionMonitor(gate);
        await monitor.StartAsync(default); for (var i = 0; i < 100 && gate.LastProbeAt is null; i++) await Task.Delay(10); Assert.Equal(InternetState.Disconnected, gate.State);
        clock.Advance(TimeSpan.FromSeconds(3)); await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var read = gate.ObserveAsync(default); Assert.True(read.IsCompletedSuccessfully); Assert.Equal(InternetState.Disconnected, await read);
        probe.Resume.TrySetResult(InternetState.Connected); for (var i = 0; i < 100 && gate.State != InternetState.Connected; i++) await Task.Delay(10); Assert.Equal(InternetState.Connected, gate.State); await monitor.StopAsync(default);
    }
    private sealed class SlowProbe : IInternetProbe
    {
        private int calls; public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); public readonly TaskCompletionSource<InternetState> Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<InternetState> ProbeAsync(CancellationToken ct) { if (Interlocked.Increment(ref calls) == 1) return InternetState.Disconnected; Entered.TrySetResult(); return await Resume.Task.WaitAsync(ct); }
    }
    [Fact]
    public async Task Read_started_before_a_connection_cycle_cannot_enable_writes_after_reconnect()
    {
        await using var h = await Host.Start(); await h.Warm();
        h.ReadHook = async () => { h.Offline(); await h.Gate.CheckAsync(default); h.Network(InternetState.Connected); await h.Gate.CheckAsync(default); h.ReadHook = null; };
        var staleFlight = await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.Conflict, staleFlight.StatusCode); Assert.DoesNotContain("FAKE scoped asset", await staleFlight.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await h.Write("FAKE-cross-epoch")).StatusCode); Assert.Equal(0, h.Writes);
        await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.OK, (await h.Write("FAKE-cross-epoch")).StatusCode);
    }
    [Fact]
    public async Task Oversized_request_is_rejected_before_auth_or_business_mutation()
    {
        await using var h = await Host.Start(); await h.Warm(); var context = new DefaultHttpContext { RequestServices = h.app.Services }; context.Request.Method = "POST"; context.Request.Path = "/api/assets"; context.Request.ContentLength = 29 * 1024 * 1024; context.Request.Headers.Authorization = "Bearer " + h.Token; context.Response.Body = new MemoryStream();
        var before = h.Store.Calls; await h.app.Services.GetRequiredService<ConnectedRuntime>().InvokeAsync(context, _ => throw new InvalidOperationException("Must not reach business"));
        Assert.Equal(413, context.Response.StatusCode); Assert.Equal(before, h.Store.Calls); Assert.Null(h.Cache.Read(h.Token)!.Pending);
    }
    [Fact]
    public async Task Data_checkpoint_changes_remove_old_scope_views_and_never_regress_to_older_data()
    {
        using var disk = new Disk(); using var cache = disk.Open(); var now = DateTimeOffset.UtcNow; var proof = Proof(now);
        await cache.SaveProofAsync("FAKE-revision", proof, default, "one");
        await cache.SaveViewAsync("FAKE-revision", proof, "one", "/api/assets/old|json", [1], "application/json", null, now, default, new Dictionary<string, string> { ["Assets"] = "1|refs" }, "one");
        await cache.SaveViewAsync("FAKE-revision", proof, "one", "/api/files/old/content|json", [2], "image/png", null, now, default, new Dictionary<string, string> { ["Assets"] = "1|refs", ["Media"] = "1" }, "one");
        await cache.SaveProofAsync("FAKE-revision", proof with { VerifiedAt = now.AddSeconds(1) }, default, "one");
        await cache.SaveViewAsync("FAKE-revision", proof, "one", "/api/assets|json", [3], "application/json", null, now.AddSeconds(2), default, new Dictionary<string, string> { ["Assets"] = "2|refs" }, "one");
        Assert.Single(cache.Read("FAKE-revision")!.Views); Assert.Equal("/api/assets|json", cache.Read("FAKE-revision")!.Views.Single().Key); Assert.Single(Directory.GetFiles(disk.Root, "*.view", SearchOption.AllDirectories));
        await cache.SaveViewAsync("FAKE-revision", proof, "one", "/api/assets/old|json", [1], "application/json", null, now.AddSeconds(3), default, new Dictionary<string, string> { ["Assets"] = "1|refs" }, "one");
        Assert.Single(cache.Read("FAKE-revision")!.Views); Assert.Equal("2|refs", cache.Read("FAKE-revision")!.Checkpoints!["Assets"]);
        await cache.SaveProofAsync("FAKE-revision", proof with { VerifiedAt = now.AddSeconds(4) }, default, "two"); Assert.Empty(cache.Read("FAKE-revision")!.Views); Assert.Empty(cache.Read("FAKE-revision")!.ReadyEpoch);
        await cache.SaveViewAsync("FAKE-revision", proof, "one", "/api/assets/late|json", [1], "application/json", null, now.AddSeconds(5), default, new Dictionary<string, string> { ["Assets"] = "2|refs" }, "one"); Assert.Empty(cache.Read("FAKE-revision")!.Views);
    }
    [Fact]
    public async Task Actual_store_observer_tracks_dependencies_without_extra_writes_and_detects_mixed_versions()
    {
        using var f = await LocalOfflineFixture.Start(items: 0); var observed = new ObservedBusinessStores(f.W, f.W, f.W, f.W, f.W);
        using var frame = BusinessReadProof.Begin(true); await ((DuyTien.AssetManagement.Application.Categories.ICategoryCatalogStore)observed).ReadAsync(); await ((DuyTien.AssetManagement.Application.Assets.IAssetCatalogStore)observed).ReadAsync(); Assert.False(BusinessReadProof.Mixed); Assert.Equal(2, BusinessReadProof.Values.Count);
        await f.Assets.CreateAsync(f.Admin, await f.Request(), "FAKE-observer"); await ((DuyTien.AssetManagement.Application.Assets.IAssetCatalogStore)observed).ReadAsync(); Assert.True(BusinessReadProof.Mixed); Assert.StartsWith("1|", BusinessReadProof.Values["Assets"]);
    }
    [Fact]
    public async Task Mixed_read_response_is_not_cached_or_delivered_as_current_and_offline_login_resumes_existing_proof()
    {
        await using var h = await Host.Start(); await h.Warm(); h.ReadHook = () => { BusinessReadProof.Track(new DuyTien.AssetManagement.Application.Assets.AssetCatalogState { Revision = 1 }); BusinessReadProof.Track(new DuyTien.AssetManagement.Application.Assets.AssetCatalogState { Revision = 2 }); return Task.CompletedTask; };
        var mixed = await h.Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.Conflict, mixed.StatusCode); Assert.DoesNotContain("FAKE scoped asset", await mixed.Content.ReadAsStringAsync()); Assert.Empty(h.Cache.Read(h.Token)!.Views);
        h.ReadHook = null; await h.Client.GetAsync("/api/assets"); h.Offline();
        h.Client.DefaultRequestHeaders.Authorization = null; h.Client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + h.Token);
        var resume = await h.Client.GetAsync("/Account/Login"); Assert.Equal(HttpStatusCode.OK, resume.StatusCode); Assert.Equal("/Offline", resume.RequestMessage!.RequestUri!.AbsolutePath);
    }
    [Fact]
    public async Task Cross_session_cache_copy_cannot_authenticate_or_open_another_users_bytes()
    {
        using var disk = new Disk(); var now = DateTimeOffset.UtcNow; string aDirectory, bDirectory;
        using (var cache = disk.Open())
        {
            var a = Proof(now); var b = a with { UserId = "FAKE-other", SessionId = "other" };
            await cache.SaveProofAsync("FAKE-A", a, default); await cache.SaveProofAsync("FAKE-B", b, default);
            await cache.SaveViewAsync("FAKE-A", a, "e", "/api/files/a/content|json", new byte[] { 1, 1, 1 }, "application/pdf", null, now, default);
            await cache.SaveViewAsync("FAKE-B", b, "e", "/api/files/b/content|json", new byte[] { 2, 2, 2 }, "application/pdf", null, now, default);
            var av = cache.Read("FAKE-A")!.Views.Single(); var bv = cache.Read("FAKE-B")!.Views.Single(); Assert.NotNull(cache.Bytes("FAKE-A", av));
            var af = Directory.GetFiles(disk.Root, av.File, SearchOption.AllDirectories).Single(); var bf = Directory.GetFiles(disk.Root, bv.File, SearchOption.AllDirectories).Single(); aDirectory = Path.GetDirectoryName(af)!; bDirectory = Path.GetDirectoryName(bf)!;
            File.Copy(af, bf, true); Assert.Null(cache.Bytes("FAKE-B", bv));
            File.Copy(Path.Combine(aDirectory, "ticket.bin"), Path.Combine(bDirectory, "ticket.bin"), true);
        }
        using (var restarted = disk.Open()) { Assert.NotNull(restarted.Read("FAKE-A")); Assert.Null(restarted.Read("FAKE-B")); }
    }
    [Fact]
    public async Task Operation_cannot_continue_Google_calls_after_network_cycle_without_new_validated_request()
    {
        var probe = new FakeProbe(); var clock = new ManualTimeProvider(DateTimeOffset.UtcNow); using var gate = new ConnectionGate(probe, clock); using var operation = ConnectionGate.BeginOperation(); await gate.EnsureAllowedAsync(default);
        probe.State = InternetState.Disconnected; clock.Advance(TimeSpan.FromSeconds(61)); await gate.CheckAsync(default);
        probe.State = InternetState.Connected; clock.Advance(TimeSpan.FromSeconds(61)); await gate.CheckAsync(default);
        Assert.Equal(InternetState.Connected, gate.State); await Assert.ThrowsAsync<DuyTien.AssetManagement.Application.ExternalStorage.ExternalStorageException>(() => gate.EnsureAllowedAsync(default));
    }
    [Fact]
    public async Task Razor_file_gateway_caches_exact_query_bytes_and_reports_missing_variant_without_central_access()
    {
        await using var h = await Host.Start(); await h.Warm();
        var url = "/Files/Content/FAKE?preview=true";
        using (var online = await h.Client.GetAsync(url)) { Assert.Equal(HttpStatusCode.OK, online.StatusCode); Assert.Equal(new byte[] { 4, 5, 6 }, await online.Content.ReadAsByteArrayAsync()); }
        h.Offline(); var before = h.Store.Calls;
        using (var offline = await h.Client.GetAsync(url)) { Assert.Equal(HttpStatusCode.OK, offline.StatusCode); Assert.Equal(new byte[] { 4, 5, 6 }, await offline.Content.ReadAsByteArrayAsync()); }
        var coverage = await h.Client.GetFromJsonAsync<JsonElement>("/api/connection/files");
        Assert.Contains(coverage.GetProperty("files").EnumerateArray(), f => f.GetProperty("url").GetString() == url && f.GetProperty("offlineAvailable").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Client.GetAsync("/Files/Content/FAKE")).StatusCode);
        Assert.Equal(before, h.Store.Calls);
    }
    private static CentralProof Proof(DateTimeOffset now) => new("FAKE-user", "FAKE-session", "fake", "GIẢ", ["Viewer"], 1, "scope-A", now, now.AddMinutes(30));
    private sealed class FakeProbe : IInternetProbe { public InternetState State = InternetState.Connected; public int Calls; public Task<InternetState> ProbeAsync(CancellationToken ct) { Interlocked.Increment(ref Calls); return Task.FromResult(State); } }
    private sealed class Disk : IDisposable
    {
        public string Root = Path.Combine(Path.GetTempPath(), "DuyTien-ConnectedCache-FAKE-" + Guid.NewGuid().ToString("N")); private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
        public ProtectedQueryCache Open() => new(Root, "FAKE-central", protection);
        public void Dispose() { var full = Path.GetFullPath(Root); if (!full.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("DuyTien-ConnectedCache-FAKE-")) throw new InvalidOperationException(); if (Directory.Exists(full)) Directory.Delete(full, true); }
    }
    private sealed class CountingStore : IAuthSystemStore
    {
        public readonly InMemoryAuthSystemStore Inner = new(); public int Calls; public bool Fail;
        public Task<T> ReadAsync<T>(Func<AuthSystemState, T> r, CancellationToken ct = default) { Interlocked.Increment(ref Calls); if (Fail) throw new DuyTien.AssetManagement.Application.ExternalStorage.ExternalStorageException("FAKE auth unavailable"); return Inner.ReadAsync(r, ct); }
        public Task<T> MutateAsync<T>(Func<AuthSystemState, T> m, CancellationToken ct = default) { Interlocked.Increment(ref Calls); if (Fail) throw new DuyTien.AssetManagement.Application.ExternalStorage.ExternalStorageException("FAKE auth unavailable"); return Inner.MutateAsync(m, ct); }
        public Task<AuthSystemInitializationResult> InitializeAsync(string actor, CancellationToken ct = default) => Inner.InitializeAsync(actor, ct);
    }
    private sealed class Host : IAsyncDisposable
    {
        public readonly CountingStore Store = new(); public readonly ManualTimeProvider Clock = new(DateTimeOffset.UtcNow); private readonly FakeProbe probe = new(); public readonly Disk disk = new(); public WebApplication app = null!;
        public HttpClient Client = null!; public ProtectedQueryCache Cache = null!; public ConnectionGate Gate = null!; public string Token = ""; public int Revision = 1, Writes; public bool FailRead, FailWrite; public Func<Task>? ReadHook;
        public static async Task<Host> Start()
        {
            var h = new Host(); var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" }); b.WebHost.UseKestrel(); b.Logging.ClearProviders(); b.Services.AddAntiforgery();
            b.Services.AddSingleton<IAuthSystemStore>(new RequestAuthStore(h.Store)); b.Services.AddSingleton<IPasswordHashService, FrameworkPasswordHashService>(); b.Services.AddSingleton(new AuthPolicyOptions()); b.Services.AddSingleton<TimeProvider>(h.Clock); b.Services.AddSingleton<AuthApplicationService>();
            b.Services.AddSingleton<IInternetProbe>(h.probe); b.Services.AddSingleton<ConnectionGate>(); h.Cache = h.disk.Open(); b.Services.AddSingleton(h.Cache); b.Services.AddSingleton<ConnectedRuntime>();
            b.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); b.Services.AddAuthorization();
            h.app = b.Build(); h.app.Urls.Add("http://127.0.0.1:0"); h.app.UseRouting(); h.app.Use(async (c, n) => { using var frame = RequestAuthStore.Begin(); using var data = BusinessReadProof.Begin(true); await n(); }); h.app.UseConnectedCache(); h.app.UseTask005SessionAuthentication(); h.app.UseConnectedWriteGate(); h.app.UseAuthorization();
            h.app.MapPost("/api/auth/login", (LoginRequest request, AuthApplicationService service) => service.LoginAsync(request));
            h.app.MapGet("/", () => Results.Content("FAKE navigation only", "text/html")).RequireAuthorization();
            h.app.MapGet("/Files/Content/FAKE", () => Results.Bytes(new byte[] { 4, 5, 6 }, "application/pdf")).RequireAuthorization();
            h.app.MapGet("/api/files/FAKE/content", () => Results.Bytes(new byte[] { 1, 2, 3 }, "application/pdf")).RequireAuthorization();
            h.app.MapGet("/api/assets", async () => { if (h.ReadHook is { } hook) await hook(); return h.FailRead ? Results.StatusCode(503) : Results.Ok(new { revision = h.Revision, name = "FAKE scoped asset" }); }).RequireAuthorization();
            h.app.MapPost("/api/assets", () => { Interlocked.Increment(ref h.Writes); return h.FailWrite ? Results.StatusCode(503) : Results.Ok(new { saved = true }); }).RequireAuthorization();
            await h.app.StartAsync(); h.Client = new HttpClient { BaseAddress = new Uri(h.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) }; h.Client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            var auth = h.app.Services.GetRequiredService<AuthApplicationService>(); var admin = await auth.BootstrapAdminAsync("FAKE-start-password", "FAKE test"); await auth.ChangePasswordAsync(admin.UserId, "FAKE-start-password", "FAKE-final-password");
            h.Token = (await auth.LoginAsync(new("admin", "FAKE-final-password", AuthClientType.Web, false, "FAKE-loopback"))).AccessToken!; h.Gate = h.app.Services.GetRequiredService<ConnectionGate>(); return h;
        }
        public void Network(InternetState state) { probe.State = state; Clock.Advance(TimeSpan.FromSeconds(61)); }
        public void Offline() => Network(InternetState.Disconnected);
        public async Task Warm() { Client.DefaultRequestHeaders.Authorization = new("Bearer", Token); var r = await Client.GetAsync("/api/assets"); Assert.Equal(HttpStatusCode.OK, r.StatusCode); await r.Content.ReadAsByteArrayAsync(); for (int i = 0; i < 100 && Cache.Read(Token)?.Views.Length != 1; i++) await Task.Delay(10); Assert.Single(Cache.Read(Token)!.Views); }
        public Task<HttpResponseMessage> Write(string key, string payload = "FAKE payload") { var request = new HttpRequestMessage(HttpMethod.Post, "/api/assets") { Content = JsonContent.Create(new { payload }) }; request.Headers.Add("Idempotency-Key", key); return Client.SendAsync(request); }
        public async ValueTask DisposeAsync() { Client?.Dispose(); await app.DisposeAsync(); Cache.Dispose(); disk.Dispose(); }
    }
}
