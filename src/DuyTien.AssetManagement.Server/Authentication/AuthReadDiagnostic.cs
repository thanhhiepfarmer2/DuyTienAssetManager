using System.Text.Json;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Server.ConnectedCache;

namespace DuyTien.AssetManagement.Server.Authentication;

// Operator CLI only, under the same exclusive lease as auth writers. No HTTP endpoint,
// no password verification, mutation, schema init, resource provisioning or journal replay.
public static class AuthReadDiagnostic
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var options = services.GetRequiredService<GoogleWorkspaceOptions>();
        var missing = options.GetMissingSystemSheetsSettings();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            diagnostic = "auth-read", stage = "configuration", environment = environment.EnvironmentName,
            storage = config["Storage:Mode"] ?? "Google", cacheEnabled = config.GetValue<bool?>("ConnectedCache:Enabled") ?? true,
            oauthAndSystemSettingsPresent = missing.Count == 0,
            systemSeparateFromBusiness = !string.IsNullOrEmpty(options.SystemSpreadsheetId) && options.SystemSpreadsheetId != options.SpreadsheetId,
            build = typeof(AuthReadDiagnostic).Assembly.ManifestModule.ModuleVersionId
        }));
        if (missing.Count != 0) throw new InvalidOperationException("Auth diagnostic configuration missing.");
        var gate = services.GetRequiredService<ConnectionGate>();
        var connectivity = await gate.CheckAsync(ct);
        Console.WriteLine(JsonSerializer.Serialize(new { diagnostic = "auth-read", stage = "connectivity", state = connectivity.ToString() }));
        await gate.EnsureAllowedAsync(ct);
        var samples = Math.Clamp(config.GetValue<int?>("Diagnostics:AuthReadSamples") ?? 1, 1, 5);
        for (var sample = 1; sample <= samples; sample++)
        {
            using var trace = Application.Diagnostics.PerformanceTrace.Begin();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
            var result = await services.GetRequiredService<GoogleAuthSystemStore>().ReadAsync(state =>
            {
                var admin = state.Users.SingleOrDefault(user => user.NormalizedUsername == "ADMIN");
                return new
                {
                    diagnostic = "auth-read", stage = "central-store", runtimeAuthTablesReadable = true,
                    adminExists = admin is not null,
                    adminActive = admin?.State == AuthUserState.Active,
                    passwordHashPresent = !string.IsNullOrEmpty(admin?.PasswordHash),
                    adminHasEffectiveThreeAll = admin is not null && AuthAuthorizationRules.IsActiveAdmin(state, admin, now),
                    currentlyTemporarilyLocked = admin?.LockoutEndAt > now,
                    noMutation = true
                };
            }, ct);
            Console.WriteLine(JsonSerializer.Serialize(result));
            Console.WriteLine(JsonSerializer.Serialize(new { diagnostic = "auth-read", stage = "timing", sample, samples, elapsedMs = timer.Elapsed.TotalMilliseconds, phases = trace.Values.ToDictionary(p => p.Key, p => new { milliseconds = p.Value.Milliseconds, count = p.Value.Count }) }));
        }
    }
}
