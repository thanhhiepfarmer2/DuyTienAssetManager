using System.Security.Claims;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Assets;

public static class AssetWeb
{
    public static string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static bool IsExpected(Exception e) => e is AssetValidationException or AssetConflictException or AssetForbiddenException or AssetNotFoundException or ExternalStorageException;
    public static int Status(Exception e) => e switch { AssetValidationException => 422, AssetConflictException => 409, AssetForbiddenException => 403, AssetNotFoundException => 404, _ => 503 };
    public static string Message(Exception e) => e is ExternalStorageException ? "Chưa xác nhận được dữ liệu lưu trữ. Giữ dữ liệu đang nhập và thử lại cùng yêu cầu; chưa báo lưu thành công." : e.Message;
    private static string Key(HttpContext context)
    {
        var values = context.Request.Headers["Idempotency-Key"];
        if (values.Count != 1) throw new AssetValidationException("Cần một Idempotency-Key cho thao tác ghi.");
        return values[0]!;
    }
    public static IEndpointRouteBuilder MapTask007AssetApi(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/assets").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (context.HttpContext.User.FindFirstValue(OpaqueSessionAuthenticationHandler.CredentialSourceClaim) != "bearer") return Results.Problem(statusCode: 403, title: "API này yêu cầu bearer token.");
            try { return await next(context); } catch (Exception e) when (IsExpected(e)) { return Results.Problem(statusCode: Status(e), title: Message(e)); }
        });
        g.MapGet("", async ([AsParameters] AssetQuery query, ClaimsPrincipal user, AssetCrudService s, CancellationToken ct) => Results.Ok(await s.ListAsync(Actor(user), query, ct)));
        g.MapGet("/form-choices", async (bool? editing, ClaimsPrincipal user, AssetCrudService s, CancellationToken ct) => Results.Ok(await s.ChoicesAsync(Actor(user), editing ?? false, ct)));
        g.MapGet("/{id}", async (string id, ClaimsPrincipal user, AssetCrudService s, CancellationToken ct) => Results.Ok(await s.DetailAsync(Actor(user), id, ct)));
        g.MapPost("", async (AssetWriteRequest input, HttpContext context, AssetCrudService s, CancellationToken ct) =>
        { var result = await s.CreateAsync(Actor(context.User), input, Key(context), ct); return Results.Created("/api/assets/" + result.Asset.AssetId, result); });
        g.MapPatch("/{id}", async (string id, AssetWriteRequest input, HttpContext context, AssetCrudService s, CancellationToken ct) => Results.Ok(await s.UpdateAsync(Actor(context.User), id, input, Key(context), ct)));
        g.MapDelete("/{id}", async (string id, [FromBody] AssetLifecycleRequest input, HttpContext context, AssetCrudService s, CancellationToken ct) => Results.Ok(await s.DeleteAsync(Actor(context.User), id, input, Key(context), ct)));
        g.MapPost("/{id}/restore", async (string id, AssetLifecycleRequest input, HttpContext context, AssetCrudService s, CancellationToken ct) => Results.Ok(await s.RestoreAsync(Actor(context.User), id, input, Key(context), ct)));
        return endpoints;
    }
}
