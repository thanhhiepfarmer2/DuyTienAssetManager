using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace DuyTien.AssetManagement.Server.ConnectedCache;

public static class ConnectedRegistration
{
    public static void AddConnectedCache(this WebApplicationBuilder builder)
    {
        var root = builder.Configuration["ConnectedCache:Path"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DuyTienAssets", "ConnectedCache");
        ProtectedQueryCache.PrepareRoot(root);
        var protection = builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(root, "keys"))).SetApplicationName("DuyTien.ConnectedCache.v2");
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        builder.Services.AddHttpClient("InternetProbe", c => c.Timeout = TimeSpan.FromSeconds(3)).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        builder.Services.AddSingleton<IInternetProbe, InternetProbe>();
        builder.Services.AddSingleton<ConnectionGate>();
        builder.Services.AddHostedService<ConnectionMonitor>();
        builder.Services.AddSingleton<IGoogleConnectivityGuard>(p => p.GetRequiredService<ConnectionGate>());
        builder.Services.AddSingleton(p =>
        {
            var config = p.GetRequiredService<GoogleWorkspaceOptions>();
            var identity = string.Join("|", config.SheetsApiBaseUrl, config.DriveApiBaseUrl, config.SpreadsheetId, config.SystemSpreadsheetId, config.DriveFolderId);
            return new ProtectedQueryCache(root, identity, p.GetRequiredService<IDataProtectionProvider>());
        });
        builder.Services.AddSingleton<ConnectedRuntime>();
    }
    public static IApplicationBuilder UseConnectedCache(this IApplicationBuilder app) => app.Use((c, next) => c.RequestServices.GetRequiredService<ConnectedRuntime>().InvokeAsync(c, _ => next()));
    public static IApplicationBuilder UseConnectedWriteGate(this IApplicationBuilder app) => app.Use((c, next) => c.RequestServices.GetRequiredService<ConnectedRuntime>().WriteAuthorityAsync(c, _ => next()));
    public static IEndpointRouteBuilder MapConnectedCache(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/connection/reconcile", async (HttpContext c, ProtectedQueryCache cache, ConnectionGate gate, ISingleWriterCoordinator writer, IAssetCatalogStore assets, ICategoryCatalogStore categories, IInventoryStore inventory, IMediaCatalogStore media, MediaLibraryService mediaService, TimeProvider clock, CancellationToken ct) =>
        {
            var token = ConnectedRuntime.Credential(c); var ticket = cache.Read(token);
            if (c.User.Identity?.IsAuthenticated != true || !ProtectedQueryCache.Valid(ticket, clock.GetUtcNow()) || c.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value != ticket!.Proof.UserId) return Results.Unauthorized();
            if (c.User.FindFirst(OpaqueSessionAuthenticationHandler.CredentialSourceClaim)?.Value != "bearer")
            {
                try { await c.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>().ValidateRequestAsync(c); }
                catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(new { code = "ANTIFORGERY" }); }
            }
            if (gate.State != InternetState.Connected || ticket.ReadyEpoch != gate.Epoch) return Results.Conflict(new { code = "RECONNECT_REQUIRED" });
            if (ticket.Pending is not { } pending) return Results.Ok(new { state = "NONE" });
            if (pending.Key == "") return Results.Conflict(new { state = "UNKNOWN", title = "Thiếu key để đối soát tự động; tiếp tục giữ form và chặn retry." });
            // Explicit online reconcile reuses the existing authorized media contract. Never upload local bytes.
            if (pending.Path.Contains("photos", StringComparison.OrdinalIgnoreCase) || pending.Path.StartsWith("/api/files/") || pending.Path.StartsWith("/api/documents/") || pending.Path.StartsWith("/Documents/"))
            {
                var mediaState = await writer.ReadAsync(t => media.ReadAsync(t), ct);
                var operation = mediaState.Operations.SingleOrDefault(o => o.Actor == ticket.Proof.UserId && o.IdempotencyKey == pending.Key);
                if (operation is not null)
                {
                    try
                    {
                        foreach (var f in operation.FilesAfter.Where(f => mediaState.Files.Any(current => current.FileId == f.FileId && current.State == "PENDING" && !current.IsDeleted)))
                            await mediaService.ReconcileAsync(ticket.Proof.UserId, f.FileId, ct);
                    }
                    catch (Exception e) when (e is MediaConflictException or MediaPendingException or MediaValidationException or MediaForbiddenException or MediaNotFoundException)
                    { return Results.Conflict(new { state = "UNKNOWN", title = "Chưa đối soát được file trong quyền hiện tại; giữ form/key và không upload lại bằng key mới." }); }
                }
            }
            var found = await writer.ReadAsync(async t =>
            {
                var actor = ticket.Proof.UserId; var key = pending.Key;
                if (!pending.Path.Contains("photos", StringComparison.OrdinalIgnoreCase) && (pending.Path.StartsWith("/api/assets") || pending.Path.StartsWith("/Assets"))) return (await assets.ReadAsync(t)).Operations.Any(o => o.Actor == actor && o.IdempotencyKey == key);
                if (pending.Path.StartsWith("/api/categories") || pending.Path.StartsWith("/Categories")) { var s = await categories.ReadAsync(t); return s.Operations.Any(o => o.ActorUserId == actor && o.IdempotencyKey == key) || s.TreeChanges.Any(o => o.Actor == actor && o.IdempotencyKey == key); }
                if (pending.Path.StartsWith("/api/items") || pending.Path.StartsWith("/api/stock") || pending.Path.StartsWith("/Items") || pending.Path.StartsWith("/Stock")) return (await inventory.ReadAsync(t)).Operations.Any(o => o.Actor == actor && o.Key == key);
                var m = await media.ReadAsync(t); return m.Operations.Any(o => o.Actor == actor && o.IdempotencyKey == key && o.FilesAfter.All(f => m.Files.Any(current => current.FileId == f.FileId && (current.IsDeleted || current.State is "AVAILABLE" or "FAILED"))));
            }, ct);
            if (!found) return Results.Conflict(new { state = "UNKNOWN", title = "Chưa có bằng chứng journal hoàn tất. Không tự coi là chưa ghi hoặc retry bằng key mới." });
            await cache.UpdatePendingAsync(token!, pending with { JournalConfirmed = true }, ct);
            return Results.Ok(new { state = "JOURNAL_CONFIRMED", title = "Chỉ cho phép retry đúng method/path/key/payload cũ; backend tiếp tục kiểm scope/version/idempotency." });
        }).RequireAuthorization();
        return endpoints;
    }
}
