using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.Diagnostics;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Server.ConnectedCache;
using DuyTien.AssetManagement.Server.LocalOffline;
namespace DuyTien.AssetManagement.Server.Diagnostics;

// Operator-only read diagnostic under the exclusive process lease. No web session or auth touch.
public static class BusinessReadDiagnostic
{
    private sealed record Measurement(double ElapsedMs, object Phases, string Payload, string Authority, string Revisions)
    { public object Safe => new { elapsedMs = ElapsedMs, phases = Phases }; }
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        if (services.GetRequiredService<IHostEnvironment>().EnvironmentName != "Development" || (configuration["Storage:Mode"] ?? "Google") != "Google") throw new InvalidOperationException("Diagnostic chỉ đọc Google Development.");
        await services.GetRequiredService<ConnectionGate>().EnsureAllowedAsync(ct);
        var store = services.GetRequiredService<IAssetCatalogStore>(); var categories = services.GetRequiredService<ICategoryCatalogStore>();
        var auth = services.GetRequiredService<IAuthSystemStore>(); var clock = services.GetRequiredService<TimeProvider>();
        var separate = new AssetCrudService(new Separate(store), categories, auth, services.GetRequiredService<ISingleWriterCoordinator>(), clock);
        var optimized = services.GetRequiredService<AssetCrudService>();
        var count = Math.Clamp(configuration.GetValue<int?>("Diagnostics:BusinessReadSamples") ?? 3, 1, 5);
        async Task<Measurement> Read(string route, bool batch)
        {
            using var trace = PerformanceTrace.Begin(); using var requestAuth = RequestAuthStore.Begin(); using var metadata = GoogleMetadataReadMemo.Begin(true); using var proof = BusinessReadProof.Begin(true);
            var timer = Stopwatch.StartNew();
            var authority = await auth.ReadAsync(s => s.Clone(), ct);
            var user = authority.Users.SingleOrDefault(u => u.NormalizedUsername == "ADMIN" && AuthAuthorizationRules.IsActiveAdmin(authority, u, clock.GetUtcNow())) ?? throw new InvalidOperationException("Cần admin hiện có hiệu lực; không tạo/reset.");
            string payload;
            if (route == "Assets")
            {
                var result = await (batch ? optimized : separate).BrowseAsync(user.UserId, new(), ct);
                payload = Hash(new { result.List.Items, result.List.Total, result.List.Page, result.List.PageSize, result.Filters });
            }
            else payload = Hash(await services.GetRequiredService<CategoryCatalogService>().ListAsync(user.UserId, ct: ct));
            timer.Stop();
            return new(timer.Elapsed.TotalMilliseconds, trace.Values.ToDictionary(p => p.Key, p => new { milliseconds = p.Value.Milliseconds, count = p.Value.Count }), payload,
                Hash(new { user.UserId, user.SecurityVersion, user.State, authority.RoleAssignments, authority.RoleAssignmentScopes }), Hash(BusinessReadProof.Values.OrderBy(p => p.Key)));
        }
        foreach (var route in new[] { "Categories", "Assets" })
        {
            var warmup = await Read(route, true);
            Console.WriteLine(JsonSerializer.Serialize(new { diagnostic = "business-read", stage = "warmup", route, source = "CLI_SERVICE_READ_NO_WEB_SESSION", authTouch = false, noBusinessMutation = true, measurement = warmup.Safe }));
            for (var i = 1; i <= count; i++)
            {
                // Alternate order; both modes read fresh auth/schema/current business values each sample.
                Measurement before, after;
                if (i % 2 == 1) { before = await Read(route, false); after = await Read(route, true); }
                else { after = await Read(route, true); before = await Read(route, false); }
                Console.WriteLine(JsonSerializer.Serialize(new { diagnostic = "business-read", stage = "pair", route, sample = i, order = i % 2 == 1 ? "separate-then-batch" : "batch-then-separate", source = "CLI_SERVICE_READ_NO_WEB_SESSION", authTouch = false, noBusinessMutation = true, equivalentPayload = before.Payload == after.Payload, sameAuthority = before.Authority == after.Authority, sameRevision = before.Revisions == after.Revisions, separate = before.Safe, batched = after.Safe }));
            }
        }
    }
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private sealed class Separate(IAssetCatalogStore inner) : IAssetCatalogStore
    {
        public Task<AssetCatalogState> ReadAsync(CancellationToken ct = default) => inner.ReadAsync(ct);
        public Task CommitAsync(AssetCatalogState state, long revision, CancellationToken ct = default) => throw new InvalidOperationException("Read diagnostic cannot commit.");
    }
}
