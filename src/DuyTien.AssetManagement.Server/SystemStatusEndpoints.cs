using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Server.Models;
namespace DuyTien.AssetManagement.Server;

public static class SystemStatusEndpoints
{
    // Public readiness is configuration-only. Live probes belong to explicitly run operator flows.
    public static IEndpointRouteBuilder MapLocalSystemStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/status", (GoogleWorkspaceOptions options, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new SystemStatusResponse("WEB_DEMO_LOCAL", true,
                new ExternalStorageStatus(options.GetMissingSheetsSettings().Count == 0, false),
                new ExternalStorageStatus(options.GetMissingDriveSettings().Count == 0, false)));
        });
        return app;
    }
}
