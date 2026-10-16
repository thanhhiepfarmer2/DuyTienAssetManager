using System.Net;
using System.Reflection;
using System.Text.Json;
using DuyTien.AssetManagement.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DuyTien.AssetManagement.ArchitectureTests;

// Actual build manifest/Kestrel, synthetic auth failure. No Google or browser claim.
public sealed class WebStaticAssetTests
{
    [Fact]
    public async Task Public_fingerprinted_assets_do_not_validate_session_but_business_and_file_routes_still_do()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var serverRoot = Path.Combine(root.FullName, "src", "DuyTien.AssetManagement.Server");
        var configuration = typeof(PublicUiAssetEndpoints).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var output = Path.Combine(serverRoot, "bin", configuration, "net10.0");
        var manifest = Path.Combine(output, "DuyTien.AssetManagement.Server.staticwebassets.endpoints.json");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(manifest));
        var routes = json.RootElement.GetProperty("Endpoints").EnumerateArray().Select(e => e.GetProperty("Route").GetString()!).ToArray();
        var assets = new[] { "js/connection.", "css/site.", "img/icons." }.Select(prefix => "/" + routes.First(r => r.StartsWith(prefix) && r.Count(c => c == '.') == 2)).ToArray();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ApplicationName = typeof(PublicUiAssetEndpoints).Assembly.GetName().Name, ContentRootPath = serverRoot });
        builder.Configuration[WebHostDefaults.StaticWebAssetsKey] = Path.Combine(output, "DuyTien.AssetManagement.Server.staticwebassets.runtime.json");
        builder.WebHost.UseStaticWebAssets(); builder.WebHost.UseKestrel(); builder.Logging.ClearProviders();
        await using var app = builder.Build(); app.Urls.Add("http://127.0.0.1:0"); app.UseRouting();
        var validations = 0;
        app.Use(async (HttpContext context, RequestDelegate next) => { Interlocked.Increment(ref validations); context.Response.StatusCode = 401; await context.Response.WriteAsync("FAKE session validation denied"); });
        app.MapPublicUiAssets(manifest);
        app.MapGet("/api/assets", () => "Must remain protected");
        app.MapGet("/Files/Content/FAKE", () => "Must remain protected");
        app.MapGet("/css/not-a-public-asset", () => "Must remain protected");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("Cookie", "dt_session=FAKE-not-a-valid-session");
            foreach (var path in assets)
            {
                using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
            }
            Assert.Equal(0, validations);
            foreach (var path in new[] { "/api/assets", "/Files/Content/FAKE", "/css/not-a-public-asset" })
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(3, validations);
        }
        finally { await app.StopAsync(); }
    }
}
