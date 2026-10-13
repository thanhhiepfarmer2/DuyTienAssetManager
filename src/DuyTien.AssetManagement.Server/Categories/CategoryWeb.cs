using System.Security.Claims;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Authorization;
namespace DuyTien.AssetManagement.Server.Categories;

public sealed record CategoryPendingPreview(string Actor, CategoryDocument Document, CategoryPreview Preview, string IdempotencyKey, DateTimeOffset ExpiresAt);
public sealed class CategoryPreviewCache(TimeProvider clock)
{
    private readonly Dictionary<string, CategoryPendingPreview> entries = new(StringComparer.Ordinal);
    private readonly object gate = new();
    public string Add(string actor, CategoryDocument document, CategoryPreview preview)
    {
        lock (gate)
        {
            foreach (var key in entries.Where(p => p.Value.ExpiresAt <= clock.GetUtcNow()).Select(p => p.Key).ToArray()) entries.Remove(key);
            if (entries.Count >= 16) throw new CategoryValidationException("Đã có nhiều preview đang chờ; thử lại sau.");
            var keyNew = Guid.NewGuid().ToString("N");
            entries.Add(keyNew, new(actor, document, preview, Guid.NewGuid().ToString("N"), clock.GetUtcNow().AddMinutes(15))); return keyNew;
        }
    }
    public CategoryPendingPreview Get(string key, string actor)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry) || entry.Actor != actor || entry.ExpiresAt <= clock.GetUtcNow())
                throw new CategoryConflictException("Preview đã hết hạn hoặc không thuộc tài khoản này; cần nạp lại file.");
            return entry;
        }
    }
    public void Remove(string key) { lock (gate) entries.Remove(key); }
}
public static class CategoryWeb
{
    public static string Actor(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static async Task<CategoryDocument> ReadUpload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > CategoryImportReader.MaxFileBytes)
            throw new CategoryValidationException("Chọn file XLSX/JSON có dữ liệu, tối đa 10 MB.");
        await using var source = file.OpenReadStream(); using var output = new MemoryStream();
        var buffer = new byte[8192]; int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > CategoryImportReader.MaxFileBytes) throw new CategoryValidationException("File vượt 10 MB.");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return CategoryImportReader.Read(output.ToArray(), file.FileName);
    }
    public static bool IsExpected(Exception e) => e is CategoryValidationException or CategoryConflictException or CategoryForbiddenException or CategoryNotFoundException or ExternalStorageException;
    public static int Status(Exception e) => e switch
    {
        CategoryForbiddenException => 403, CategoryNotFoundException => 404,
        CategoryConflictException => 409, CategoryValidationException => 400, _ => 503
    };
    public static string Message(Exception e) => e is ExternalStorageException
        ? "Không đọc/ghi được danh mục trên Google Sheets. Dữ liệu chưa được xác nhận; vui lòng thử lại." : e.Message;
    public static IResult Error(Exception e) => Results.Problem(statusCode: Status(e), title: Message(e));
    public static IEndpointRouteBuilder MapTask006CategoryApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/categories").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (context.HttpContext.User.FindFirstValue(OpaqueSessionAuthenticationHandler.CredentialSourceClaim) != "bearer")
                return Results.Problem(statusCode: 403, title: "API này yêu cầu bearer token.");
            try { return await next(context); } catch (Exception e) when (IsExpected(e)) { return Error(e); }
        });
        group.MapGet("", async (string? q, ClaimsPrincipal user, CategoryCatalogService service, CancellationToken ct) => Results.Ok(await service.ListAsync(Actor(user), q, ct: ct)));
        group.MapGet("/tree", async (ClaimsPrincipal user, CategoryCatalogService service, CancellationToken ct) => Results.Ok(await service.ListAsync(Actor(user), ct: ct)));
        group.MapGet("/export", async (ClaimsPrincipal user, CategoryCatalogService service, CancellationToken ct) => Results.File(await service.ExportAsync(Actor(user), ct), "text/plain; charset=utf-8", "danh-muc-DRAFT.txt"));
        group.MapGet("/{id}/history", async (string id, ClaimsPrincipal user, CategoryCatalogService service, CancellationToken ct) => Results.Ok(await service.HistoryAsync(Actor(user), id, ct)));
        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, CategoryCatalogService service, CancellationToken ct) => Results.Ok(await service.DetailAsync(Actor(user), id, ct)));
        group.MapPost("/imports/preview", async (HttpContext context, CategoryCatalogService service, CategoryPreviewCache cache, CancellationToken ct) =>
        {
            if (!context.Request.HasFormContentType) return Results.Problem(statusCode: 400, title: "Nạp multipart/form-data với trường file.");
            var form = await context.Request.ReadFormAsync(ct);
            var document = await ReadUpload(form.Files.GetFile("file"), ct);
            var actor = Actor(context.User); var preview = await service.PreviewAsync(actor, document, ct);
            var key = preview.CanCommit ? cache.Add(actor, document, preview) : null;
            return Results.Ok(new { preview, previewId = key });
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin,CatalogManager" });
        group.MapPost("/imports/commit", async (CommitRequest input, ClaimsPrincipal user, CategoryCatalogService service, CategoryPreviewCache cache, CancellationToken ct) =>
        {
            var actor = Actor(user); var entry = cache.Get(input.PreviewId ?? "", actor);
            var result = await service.ImportAsync(actor, entry.Document, entry.Preview.Revision, entry.Preview.PreviewHash, entry.IdempotencyKey, input.ConfirmDuplicateNames, ct);
            // Retain the bounded entry until TTL to support a client retry after a lost response.
            return Results.Ok(result);
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin,CatalogManager" });
        return endpoints;
    }
    public sealed record CommitRequest(string? PreviewId, bool ConfirmDuplicateNames);
}
