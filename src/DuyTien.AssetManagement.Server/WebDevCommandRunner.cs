using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Server.Authentication;
namespace DuyTien.AssetManagement.Server;

internal static class WebDevCommandRunner
{
    public static async Task<bool> TryRunAsync(string[] args, IServiceProvider services, IHostEnvironment environment, CancellationToken ct = default)
    {
        var command = args.FirstOrDefault(); if (command is not ("google-web-dev-init" or "google-web-dev-demo-references")) return false;
        try
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("DEV environment required.");
            await using var lease = await services.GetRequiredService<IAuthWriterLeaseManager>().AcquireAsync(command, ct);
            var initializer = services.GetRequiredService<GoogleWebDevInitializer>();
            if (command == "google-web-dev-demo-references" && args.Length < 2) throw new ArgumentException("Supply fake root IDs, no secrets.");
            var result = command == "google-web-dev-init" ? await initializer.InitializeAsync(ct) : await initializer.SeedFakeReferencesAsync(args.Skip(1).ToArray(), ct);
            Console.WriteLine($"WEB DEV schema/reference verified: changedTables={result.CreatedTables}; noOp={result.NoOp}; existing auth/PoC/data preserved.");
            Environment.ExitCode = 0;
        }
        catch (Exception e) { Console.Error.WriteLine($"WEB DEV command failed: {e.GetType().Name}. No credential/resource IDs/body printed."); Environment.ExitCode = 1; }
        return true;
    }
}
