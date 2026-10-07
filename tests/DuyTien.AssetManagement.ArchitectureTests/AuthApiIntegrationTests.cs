using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AuthApiIntegrationTests
{
    [Fact]
    public async Task Api_enforces_bearer_and_admin_and_never_returns_password_hash()
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
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTask005AuthApi();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

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

        using var cookieRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        cookieRequest.Headers.Add("Cookie", $"{OpaqueSessionAuthenticationHandler.CookieName}={adminToken}");
        using var cookieResponse = await client.SendAsync(cookieRequest);
        Assert.Equal(HttpStatusCode.Forbidden, cookieResponse.StatusCode);

        await app.StopAsync();
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
