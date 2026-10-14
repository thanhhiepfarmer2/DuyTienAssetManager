using System.Security.Claims;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Inventory;

public static class InventoryWeb
{
    public static string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static bool Expected(Exception e) => e is InventoryValidationException or InventoryConflictException or InventoryForbiddenException or InventoryNotFoundException or ExternalStorageException;
    public static int Status(Exception e) => e switch { InventoryValidationException => 422, InventoryConflictException => 409, InventoryForbiddenException => 403, InventoryNotFoundException => 404, _ => 503 };
    public static string Message(Exception e) => e is ExternalStorageException ? "Chưa xác nhận được dữ liệu lưu trữ. Giữ input/key; kiểm tiến độ và retry cùng yêu cầu. Không coi job PARTIAL/STAGING là COMPLETE." : e.Message;
    private static string Key(HttpContext c) { var k = c.Request.Headers["Idempotency-Key"]; if (k.Count != 1) throw new InventoryValidationException("Cần đúng một Idempotency-Key."); return k[0]!; }
    public sealed record StartRequest(ItemImportRequest Request, long Revision, string PreviewHash, bool Confirm);
    public sealed record ChunkRequest(int ExpectedProgress);
    public static IEndpointRouteBuilder MapTask010InventoryApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        { context.HttpContext.Response.Headers.CacheControl = "no-store"; if (context.HttpContext.User.FindFirstValue(OpaqueSessionAuthenticationHandler.CredentialSourceClaim) != "bearer") return Results.Problem(statusCode: 403, title: "API yêu cầu bearer token."); try { return await next(context); } catch (Exception e) when (Expected(e)) { return Results.Problem(statusCode: Status(e), title: Message(e)); } });
        api.MapGet("/items", async ([AsParameters] InventoryQuery q, ClaimsPrincipal user, InventoryService s, CancellationToken ct) => Results.Ok(await s.ListAsync(Actor(user), q, ct)));
        api.MapGet("/items/{id}", async (string id, ClaimsPrincipal user, InventoryService s, CancellationToken ct) => Results.Ok(await s.DetailAsync(Actor(user), id, ct)));
        api.MapPost("/item-imports/preview", async (ItemImportRequest r, int? page, int? pageSize, ClaimsPrincipal user, InventoryService s, CancellationToken ct) => Results.Ok(await s.PreviewAsync(Actor(user), r, page ?? 1, pageSize ?? 20, ct)));
        api.MapPost("/item-imports/preview-file", async (HttpContext c, InventoryService service, CancellationToken ct) =>
        {
            if (!c.Request.HasFormContentType) throw new InventoryValidationException("Cần multipart file/groups/decisions/reason.");
            try
            {
                var form = await c.Request.ReadFormAsync(ct); if (form.Files.Count != 1) throw new InventoryValidationException("Cần đúng một file XLSX/JSON.");
                var d = await ReadUpload(form.Files[0], ct); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
                var r = new ItemImportRequest(d, JsonSerializer.Deserialize<ItemGroupMapping[]>(form["groups"].ToString(), json)!, JsonSerializer.Deserialize<ItemDuplicateDecision[]>(form["decisions"].ToString(), json)!, form["reason"].ToString());
                return Results.Ok(new { Document = d, Preview = await service.PreviewAsync(Actor(c.User), r, ct: ct) });
            }
            catch (JsonException) { throw new InventoryValidationException("JSON group/decision không đúng cấu trúc."); }
            catch (InvalidDataException) { throw new InventoryValidationException("Multipart vượt giới hạn hoặc không hợp lệ."); }
        }).WithMetadata(new RequestSizeLimitAttribute(64 * 1024 * 1024), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = 40 * 1024 * 1024, ValueLengthLimit = 48 * 1024 * 1024 });
        api.MapPost("/item-imports", async (StartRequest r, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.StartAsync(Actor(c.User), r.Request, r.Revision, r.PreviewHash, Key(c), r.Confirm, ct)));
        api.MapGet("/item-imports", async (ClaimsPrincipal user, InventoryService s, CancellationToken ct) => Results.Ok(await s.JobsAsync(Actor(user), ct)));
        api.MapGet("/item-imports/{id}", async (string id, ClaimsPrincipal user, InventoryService s, CancellationToken ct) => Results.Ok(await s.JobAsync(Actor(user), id, ct)));
        api.MapPost("/item-imports/{id}/chunks", async (string id, ChunkRequest r, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.CommitChunkAsync(Actor(c.User), id, r.ExpectedProgress, Key(c), ct)));
        api.MapGet("/inventory/choices", async (bool? stock, ClaimsPrincipal user, InventoryService s, CancellationToken ct) => Results.Ok(await s.ChoicesAsync(Actor(user), stock ?? false, ct)));
        api.MapGet("/stock-balances", async ([AsParameters] InventoryQuery q, ClaimsPrincipal user, InventoryService s, CancellationToken ct) => Results.Ok(await s.StockAsync(Actor(user), q, ct)));
        api.MapPost("/stock-balances", async (StockWriteRequest r, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.WriteStockAsync(Actor(c.User), r, Key(c), ct)));
        return endpoints;
    }
    public static async Task<ItemDocument> ReadUpload(IFormFile file, CancellationToken ct)
    {
        if (file.Length is 0 or > ItemImportReader.MaxBytes) throw new InventoryValidationException("File tối đa32MiB."); using var ms = new MemoryStream(); await using var stream = file.OpenReadStream(); var buffer = new byte[81920]; int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0) { if (ms.Length + n > ItemImportReader.MaxBytes) throw new InventoryValidationException("File vượt32MiB."); await ms.WriteAsync(buffer.AsMemory(0, n), ct); }
        return ItemImportReader.Read(ms.ToArray(), file.FileName);
    }
}
