using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AuthApiIntegrationTests
{
    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    public async Task Api_enforces_bearer_and_admin_and_never_returns_password_hash(string accept)
    {
        var now = new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
        var hasher = new TestPasswordHashService();
        var state = new AuthSystemState();
        AddUser(state, hasher, now, "ADMIN-1", "admin_demo", "admin-pass", AuthRole.Admin);
        AddUser(state, hasher, now, "VIEWER-1", "viewer_demo", "viewer-pass", AuthRole.Viewer);
        var store = new InMemoryAuthSystemStore(state);
        var time = new ManualTimeProvider(now);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseKestrel();
        builder.Services.AddSingleton<IAuthSystemStore>(store);
        builder.Services.AddSingleton<IPasswordHashService>(hasher);
        builder.Services.AddSingleton(new AuthPolicyOptions());
        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddSingleton<AuthApplicationService>();
        builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(
                OpaqueSessionAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.AddAuthorization();

        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseTask005SessionAuthentication();
        app.UseAuthorization();
        app.MapTask005AuthApi();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        using var anonymousResponse = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Null(anonymousResponse.Headers.Location);

        var adminToken = await LoginAsync(client, "admin_demo", "admin-pass");
        var viewerToken = await LoginAsync(client, "viewer_demo", "viewer-pass");

        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var adminResponse = await client.SendAsync(adminRequest);
        var body = await adminResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HASH::", body, StringComparison.Ordinal);

        using var viewerRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        viewerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);
        using var viewerResponse = await client.SendAsync(viewerRequest);
        Assert.Equal(HttpStatusCode.Forbidden, viewerResponse.StatusCode);
        Assert.Null(viewerResponse.Headers.Location);

        using var cookieRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        cookieRequest.Headers.Add("Cookie", $"{OpaqueSessionAuthenticationHandler.CookieName}={adminToken}");
        using var cookieResponse = await client.SendAsync(cookieRequest);
        Assert.Equal(HttpStatusCode.Forbidden, cookieResponse.StatusCode);

        await app.StopAsync();
    }

    [Theory]
    [InlineData("application/json", false)]
    [InlineData("text/html", false)]
    [InlineData("application/json", true)]
    [InlineData("text/html", true)]
    public async Task Storage_fault_returns_503_without_login_redirect_or_cookie_loss_and_session_recovers(
        string accept, bool cookie)
    {
        var now = new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
        var hasher = new TestPasswordHashService();
        var state = new AuthSystemState();
        AddUser(state, hasher, now, "ADMIN-1", "admin_demo", "admin-pass", AuthRole.Admin);
        var store = new FaultingAuthStore(new InMemoryAuthSystemStore(state));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseKestrel();
        builder.Services.AddSingleton<IAuthSystemStore>(store);
        builder.Services.AddSingleton<IPasswordHashService>(hasher);
        builder.Services.AddSingleton(new AuthPolicyOptions());
        builder.Services.AddSingleton<TimeProvider>(new ManualTimeProvider(now));
        builder.Services.AddSingleton<AuthApplicationService>();
        builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(
                OpaqueSessionAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseTask005SessionAuthentication();
        app.UseAuthorization();
        app.MapTask005AuthApi();
        app.MapGet("/Admin/Users", async (IAuthSystemStore data) =>
            Results.Ok(await data.ReadAsync(value => value.Users.Count))).RequireAuthorization();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { BaseAddress = new Uri(address) };
        var token = await LoginAsync(client, "admin_demo", "admin-pass");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (cookie) client.DefaultRequestHeaders.Add("Cookie", $"{OpaqueSessionAuthenticationHandler.CookieName}={token}");
        else client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var path = cookie ? "/Admin/Users" : "/api/admin/users";

        store.FailMutate = true;
        using (var unavailable = await client.GetAsync(path))
        {
            await AssertSafeUnavailable(unavailable, token);
        }
        store.FailMutate = false;
        using (var recovered = await client.GetAsync(path))
        {
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        }

        store.FailRead = true;
        using (var unavailable = await client.GetAsync(path))
        {
            await AssertSafeUnavailable(unavailable, token);
        }
        store.FailRead = false;
        using (var recovered = await client.GetAsync(path))
        {
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        }
        await app.StopAsync();
    }

    private static async Task AssertSafeUnavailable(HttpResponseMessage response, string token)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-FAULT-CANARY", body, StringComparison.Ordinal);
        Assert.DoesNotContain("HASH::", body, StringComparison.Ordinal);
    }

    private sealed class FaultingAuthStore(IAuthSystemStore inner) : IAuthSystemStore
    {
        public bool FailRead { get; set; }
        public bool FailMutate { get; set; }
        public Task<T> ReadAsync<T>(Func<AuthSystemState, T> reader, CancellationToken cancellationToken = default) =>
            FailRead ? throw Fault() : inner.ReadAsync(reader, cancellationToken);
        public Task<T> MutateAsync<T>(Func<AuthSystemState, T> mutation, CancellationToken cancellationToken = default) =>
            FailMutate ? throw Fault() : inner.MutateAsync(mutation, cancellationToken);
        public Task<AuthSystemInitializationResult> InitializeAsync(string actor, CancellationToken cancellationToken = default) =>
            inner.InitializeAsync(actor, cancellationToken);
        private static ExternalStorageTransientException Fault() => new("PRIVATE-FAULT-CANARY", false);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static void AddUser(
        AuthSystemState state,
        IPasswordHashService hasher,
        DateTimeOffset now,
        string userId,
        string username,
        string password,
        string role)
    {
        state.Users.Add(new AuthUser(
            userId, username, username.ToUpperInvariant(), username,
            hasher.Hash(userId, password), AuthUserState.Active, false,
            0, null, null, 1, now, now, "test", now, "test", 1));
        var assignmentId = $"ASSIGN-{userId}";
        state.RoleAssignments.Add(new AuthRoleAssignment(
            assignmentId, userId, role, null, "MANUAL", RoleAssignmentState.Active,
            now.AddMinutes(-1), null, now, "test", 1));
        foreach (var type in Enum.GetValues<AuthScopeType>())
        {
            state.RoleAssignmentScopes.Add(new AuthScopeGrant(
                Guid.NewGuid().ToString("N"), assignmentId, type,
                AuthScopeGrant.All, true, 1));
        }
    }
}
