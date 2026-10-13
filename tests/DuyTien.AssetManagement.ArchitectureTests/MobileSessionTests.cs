using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Mobile.Client;

namespace DuyTien.AssetManagement.ArchitectureTests;

// Synthetic credentials in RAM only; no platform SecureStorage, Google or device runtime evidence.
public sealed class MobileSessionTests
{
    [Theory]
    [InlineData("http://fixture.invalid")]
    [InlineData("https://user:pass@fixture.invalid")]
    [InlineData("https://fixture.invalid/?token=synthetic")]
    [InlineData("https://fixture.invalid/#fragment")]
    [InlineData("not a url")]
    public void Endpoint_rejects_insecure_or_credential_bearing_configuration(string url) => Assert.Throws<MobileApiException>(() => MobileEndpoint.Parse(url));

    [Fact]
    public async Task Login_persists_atomic_pair_and_binds_it_to_backend_without_plaintext_password()
    {
        var f = new MobileFake(); await f.Login();
        Assert.True(f.Session.IsSignedIn); Assert.Contains("https://fixture.invalid/", f.Store.Value!);
        Assert.DoesNotContain("fake-password", f.Store.Value!);
        Assert.DoesNotContain("fake-access", JsonSerializer.Deserialize<SessionEnvelope>(f.Store.Value!, MobileJson.Options)!.Tokens.ToString());
        var restore = new MobileSession(f.Http, f.Endpoint, f.Store, f.Clock); Assert.True(await restore.RestoreAsync());
    }
    [Theory]
    [InlineData(true, "https://fixture.invalid/", false)]
    [InlineData(false, "https://other.invalid/", false)]
    [InlineData(false, "https://fixture.invalid/", true)]
    public async Task Restore_clears_pending_rotation_wrong_environment_or_expired_pair(bool pending, string backend, bool expired)
    {
        var f = new MobileFake(); f.Store.Value = JsonSerializer.Serialize(new SessionEnvelope
        {
            Backend = backend,
            RefreshInFlight = pending,
            Tokens = f.Tokens(refreshExpiry: expired ? f.Clock.GetUtcNow().AddSeconds(-1) : null)
        });
        Assert.False(await f.Session.RestoreAsync()); Assert.Null(f.Store.Value); Assert.False(f.Session.IsSignedIn); Assert.Empty(f.Calls);
    }
    [Fact]
    public async Task Concurrent_expired_access_rotates_once_and_all_requests_use_replacement()
    {
        var f = new MobileFake(); await f.Login(expired: true); var rotations = 0;
        f.Handler.Respond = (r, _) =>
        {
            if (r.RequestUri!.AbsolutePath == "/api/auth/refresh") { rotations++; Assert.True(JsonSerializer.Deserialize<SessionEnvelope>(f.Store.Value!, MobileJson.Options)!.RefreshInFlight); Assert.Null(r.Headers.Authorization); return Task.FromResult(MobileFake.Json(f.Tokens("rotated"))); }
            Assert.Equal("rotated-access", r.Headers.Authorization!.Parameter); return Task.FromResult(MobileFake.Json(new { value = 1 }));
        };
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => f.Session.JsonAsync<JsonElement>(() => new(HttpMethod.Get, "api/me"))));
        Assert.Equal(1, rotations); Assert.False(JsonSerializer.Deserialize<SessionEnvelope>(f.Store.Value!, MobileJson.Options)!.RefreshInFlight);
    }
    [Fact]
    public async Task Unauthorized_access_rotates_once_but_revoked_refresh_clears_session()
    {
        var f = new MobileFake(); await f.Login(); var refreshes = 0;
        f.Handler.Respond = (r, _) => { if (r.RequestUri!.AbsolutePath.EndsWith("refresh")) refreshes++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); };
        var e = await Assert.ThrowsAsync<MobileApiException>(() => f.Session.JsonAsync<JsonElement>(() => new(HttpMethod.Get, "api/me")));
        Assert.Equal(401, e.Status); Assert.Equal(1, refreshes); Assert.Null(f.Store.Value); Assert.False(f.Session.IsSignedIn);
    }
    [Fact]
    public async Task Lost_refresh_response_is_never_replayed_and_deletes_temporary_pair()
    {
        var f = new MobileFake(); await f.Login(expired: true); var count = 0;
        f.Handler.Respond = (_, _) => { count++; throw new HttpRequestException("PRIVATE FAKE TRANSPORT"); };
        var e = await Assert.ThrowsAsync<MobileApiException>(() => f.Session.JsonAsync<JsonElement>(() => new(HttpMethod.Get, "api/me")));
        Assert.Equal(401, e.Status); Assert.DoesNotContain("PRIVATE", e.Message); Assert.Null(f.Store.Value);
        await Assert.ThrowsAsync<MobileApiException>(() => f.Session.JsonAsync<JsonElement>(() => new(HttpMethod.Get, "api/me")));
        Assert.Equal(1, count);
    }
    [Fact]
    public async Task Rotation_storage_failure_before_send_prevents_refresh_and_after_send_does_not_resume_old_token()
    {
        foreach (var failWrite in new[] { 2, 3 })
        {
            var f = new MobileFake(); await f.Login(expired: true); f.Store.FailWrite = failWrite; var sends = 0;
            f.Handler.Respond = (_, _) => { sends++; return Task.FromResult(MobileFake.Json(f.Tokens("rotated"))); };
            await Assert.ThrowsAsync<MobileApiException>(() => f.Session.JsonAsync<JsonElement>(() => new(HttpMethod.Get, "api/me")));
            Assert.False(f.Session.IsSignedIn); Assert.Null(f.Store.Value); Assert.Equal(failWrite == 2 ? 0 : 1, sends);
        }
    }
    [Fact]
    public async Task Must_change_blocks_business_and_password_minimum_only_then_revokes_local_pair()
    {
        var f = new MobileFake(); await f.Login(mustChange: true); var calls = f.Calls.Count;
        Assert.Equal(403, (await Assert.ThrowsAsync<MobileApiException>(() => new MobileAssets(f.Session).ListAsync(new()))).Status);
        await Assert.ThrowsAsync<MobileApiException>(() => f.Session.ChangePasswordAsync("fake-password", "1234567")); Assert.Equal(calls, f.Calls.Count);
        f.Handler.Respond = async (r, ct) => { var body = await r.Content!.ReadFromJsonAsync<JsonElement>(ct); Assert.Equal("12345678", body.GetProperty("newPassword").GetString()); return new(HttpStatusCode.NoContent); };
        await f.Session.ChangePasswordAsync("fake-password", "12345678"); Assert.Null(f.Store.Value); Assert.False(f.Session.IsSignedIn);
    }
    [Fact]
    public async Task Logout_network_failure_still_removes_local_credentials_and_never_logs_transport_secret()
    {
        var f = new MobileFake(); await f.Login(); f.Handler.Respond = (_, _) => throw new HttpRequestException("SECRET-FAKE");
        var e = await Assert.ThrowsAsync<MobileApiException>(() => f.Session.LogoutAsync()); Assert.DoesNotContain("SECRET", e.Message);
        Assert.Null(f.Store.Value); Assert.False(f.Session.IsSignedIn);
    }
    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Non401_failure_does_not_rotate_or_invent_success(int code)
    {
        var f = new MobileFake(); await f.Login(); f.Handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code));
        Assert.Equal(code, (await Assert.ThrowsAsync<MobileApiException>(() => new MobileAssets(f.Session).MeAsync())).Status);
        Assert.Equal(2, f.Calls.Count); Assert.True(f.Session.IsSignedIn);
    }
    [Fact]
    public async Task Login_storage_failure_is_not_reported_as_signed_in_and_corrupt_restore_is_cleared()
    {
        var f = new MobileFake(); f.Store.FailWrite = 1;
        Assert.Equal(0, (await Assert.ThrowsAsync<MobileApiException>(() => f.Login())).Status);
        Assert.False(f.Session.IsSignedIn); Assert.Null(f.Store.Value);
        f.Store.Value = "corrupt fake envelope"; Assert.False(await f.Session.RestoreAsync()); Assert.Null(f.Store.Value);
    }
    [Fact]
    public async Task Body_read_cancellation_keeps_business_session_and_returns_retry_error()
    {
        var f = new MobileFake(); await f.Login(); using var cancel = new CancellationTokenSource();
        f.Handler.Respond = (_, _) => { cancel.Cancel(); return Task.FromResult(MobileFake.Json(new { userId = "fake" })); };
        Assert.Equal(0, (await Assert.ThrowsAsync<MobileApiException>(() => new MobileAssets(f.Session).MeAsync(cancel.Token))).Status);
        Assert.True(f.Session.IsSignedIn);
    }
    [Fact]
    public async Task Cross_origin_request_cannot_send_bearer()
    {
        var f = new MobileFake(); await f.Login();
        Assert.Equal(400, (await Assert.ThrowsAsync<MobileApiException>(() => f.Session.JsonAsync<JsonElement>(() => new(HttpMethod.Get, "https://other.invalid/api/me")))).Status);
        Assert.Single(f.Calls);
    }
    [Fact]
    public async Task Bad_json_and_oversized_response_do_not_become_success_or_leak_raw_body()
    {
        var f = new MobileFake(); await f.Login();
        foreach (var body in new[] { "PRIVATE-FAKE-CANARY", new string('x', 2 * 1024 * 1024 + 1) })
        {
            f.Handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
            var e = await Assert.ThrowsAsync<MobileApiException>(() => new MobileAssets(f.Session).MeAsync()); Assert.Equal(503, e.Status); Assert.DoesNotContain("CANARY", e.Message);
        }
    }
}
internal sealed class MobileFake
{
    public Uri Endpoint { get; } = new("https://fixture.invalid/");
    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero));
    public MobileMemorySecrets Store { get; } = new();
    public MobileHandler Handler { get; } = new();
    public HttpClient Http { get; }
    public MobileSession Session { get; }
    public List<string> Calls { get; } = [];
    public MobileFake()
    {
        Handler.Observe = r => Calls.Add(r.RequestUri!.AbsolutePath);
        Http = new(Handler); Session = new(Http, Endpoint, Store, Clock);
    }
    public MobileTokens Tokens(string prefix = "fake", DateTimeOffset? refreshExpiry = null, bool expired = false, bool mustChange = false) => new()
    { AccessToken = prefix + "-access", RefreshToken = prefix + "-refresh", AccessExpiresAt = Clock.GetUtcNow().AddMinutes(expired ? -1 : 30), RefreshExpiresAt = refreshExpiry ?? Clock.GetUtcNow().AddDays(14), MustChangePassword = mustChange };
    public async Task Login(bool expired = false, bool mustChange = false)
    { Handler.Respond = (_, _) => Task.FromResult(Json(Tokens(expired: expired, mustChange: mustChange))); await Session.LoginAsync("fake-user", "fake-password"); }
    public static HttpResponseMessage Json<T>(T value, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = JsonContent.Create(value) };
}
internal sealed class MobileMemorySecrets : IMobileSecretStore
{
    public string? Value { get; set; }
    public int Writes { get; private set; }
    public int FailWrite { get; set; }
    public Task<string?> ReadAsync() => Task.FromResult(Value);
    public Task WriteAsync(string value) { if (++Writes == FailWrite) throw new IOException("fake storage failure"); Value = value; return Task.CompletedTask; }
    public Task RemoveAsync() { Value = null; return Task.CompletedTask; }
}
internal sealed class MobileHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } = (_, _) => throw new InvalidOperationException("Unconfigured fake request");
    public Action<HttpRequestMessage>? Observe { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Observe?.Invoke(request); return Respond(request, cancellationToken); }
}
