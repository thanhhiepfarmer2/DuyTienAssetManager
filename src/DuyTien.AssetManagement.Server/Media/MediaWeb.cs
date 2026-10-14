using System.Globalization;
using System.Net.Http.Headers;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Media;

public static class MediaWeb
{
    public const long RequestCap = 26L * 1024 * 1024;
    public static bool Expected(Exception e) => e is MediaValidationException or MediaTooLargeException or MediaForbiddenException or MediaNotFoundException or MediaConflictException or MediaPendingException or ExternalStorageException;
    public static int Status(Exception e) => e switch { MediaValidationException => 400, MediaTooLargeException => 413, MediaForbiddenException => 403, MediaNotFoundException => 404, MediaConflictException => 409, _ => 503 };
    public static string Message(Exception e) => e is ExternalStorageException ? "Dịch vụ lưu trữ chưa xác nhận thao tác. Giữ key, tải lại/đối soát; chưa báo lưu thành công." : e.Message;
    public static IResult Error(Exception e) => Results.Problem(title: Message(e), statusCode: Status(e), extensions: e is MediaPendingException p ? new Dictionary<string, object?> { { "fileId", p.FileId } } : null);
    public static string Key(HttpContext c) { var values = c.Request.Headers["Idempotency-Key"]; if (values.Count != 1) throw new MediaValidationException("Cần đúng một Idempotency-Key."); return values[0]!; }
    public static async Task<MediaFileContent> ReadFile(IFormFile? file, string kind, MediaUploadOptions options, CancellationToken ct)
    {
        if (file is null) throw new MediaValidationException("Chọn file để upload."); var cap = kind == "PHOTO" ? options.MaxPhotoBytes : options.MaxDocumentBytes; if (file.Length > cap) throw new MediaTooLargeException();
        await using var input = file.OpenReadStream(); using var buffer = new MemoryStream(); var chunk = new byte[81920]; int read; while ((read = await input.ReadAsync(chunk, ct)) > 0) { if (buffer.Length + read > cap) throw new MediaTooLargeException(); buffer.Write(chunk, 0, read); }
        return new(file.FileName, file.ContentType, buffer.ToArray());
    }
    public static void DownloadHeaders(HttpResponse response, MediaDownload file, bool preview)
    {
        response.Headers.CacheControl = "no-store"; response.Headers["X-Content-Type-Options"] = "nosniff"; response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'"; var inline = preview && file.Kind is "PHOTO" or "PDF";
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment") { FileNameStar = file.FileName }.ToString();
    }
    public static IEndpointRouteBuilder MapTask009MediaApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").RequireAuthorization().AddEndpointFilter(async (c, next) =>
        { c.HttpContext.Response.Headers.CacheControl = "no-store"; if (c.HttpContext.User.FindFirst(OpaqueSessionAuthenticationHandler.CredentialSourceClaim)?.Value != "bearer") return Results.Problem(statusCode: 403, title: "API này yêu cầu bearer token."); try { return await next(c); } catch (Exception e) when (Expected(e)) { return Error(e); } });
        group.MapGet("/documents", async (string? q, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.DocumentsAsync(CategoryWeb.Actor(c.User), q, ct)));
        group.MapGet("/documents/{id}", async (string id, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.DocumentAsync(CategoryWeb.Actor(c.User), id, ct)));
        group.MapPost("/documents", async (DocumentWriteRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.CreateDocumentAsync(CategoryWeb.Actor(c.User), input, Key(c), ct)));
        group.MapPatch("/documents/{id}", async (string id, DocumentWriteRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.UpdateDocumentAsync(CategoryWeb.Actor(c.User), id, input, Key(c), ct)));
        group.MapPost("/documents/{id}/archive", async (string id, MediaLifecycleRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.ArchiveDocumentAsync(CategoryWeb.Actor(c.User), id, input, Key(c), ct)));
        group.MapPost("/documents/{id}/versions", async (string id, DocumentVersionRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.CreateVersionAsync(CategoryWeb.Actor(c.User), id, input, Key(c), ct)));
        group.MapPost("/document-versions/{id}/publish", async (string id, DocumentPublishRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.PublishVersionAsync(CategoryWeb.Actor(c.User), id, input, Key(c), ct)));
        group.MapGet("/assets/{id}/photos", async (string id, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.PhotosAsync(CategoryWeb.Actor(c.User), id, ct)));
        group.MapPost("/assets/{id}/photos", async (string id, HttpContext c, MediaLibraryService service, MediaUploadOptions options, CancellationToken ct) =>
        { var form = await Form(c, ["expectedVersion", "description", "isPrimary"], ct); var file = await ReadFile(form.Files.Single(), "PHOTO", options, ct); if (!bool.TryParse(form["isPrimary"], out var primary) && !string.IsNullOrEmpty(form["isPrimary"])) throw new MediaValidationException("isPrimary không hợp lệ."); return Results.Ok(await service.UploadPhotoAsync(CategoryWeb.Actor(c.User), id, file, Version(form), form["description"].ToString(), primary, Key(c), ct)); }).WithMetadata(new RequestSizeLimitAttribute(RequestCap), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = RequestCap });
        group.MapPost("/document-versions/{id}/files/{kind}", async (string id, string kind, HttpContext c, MediaLibraryService service, MediaUploadOptions options, CancellationToken ct) =>
        { var form = await Form(c, ["expectedVersion"], ct); return Results.Ok(await service.UploadDocumentFileAsync(CategoryWeb.Actor(c.User), id, kind.ToUpperInvariant(), await ReadFile(form.Files.Single(), kind.ToUpperInvariant(), options, ct), Version(form), Key(c), ct)); }).WithMetadata(new RequestSizeLimitAttribute(RequestCap), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = RequestCap });
        group.MapPatch("/files/{id}/photo", async (string id, PhotoUpdateRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.UpdatePhotoAsync(CategoryWeb.Actor(c.User), id, input, Key(c), ct)));
        group.MapDelete("/files/{id}", async (string id, [FromBody] MediaLifecycleRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.DeletePhotoAsync(CategoryWeb.Actor(c.User), id, input, Key(c), ct)));
        group.MapPost("/files/{id}/cancel", async (string id, MediaLifecycleRequest input, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.CancelUploadAsync(CategoryWeb.Actor(c.User), id, input, Key(c), ct)));
        group.MapPost("/files/{id}/reconcile", async (string id, HttpContext c, MediaLibraryService service, CancellationToken ct) => Results.Ok(await service.ReconcileAsync(CategoryWeb.Actor(c.User), id, ct)));
        group.MapGet("/files/{id}/content", async (string id, bool? preview, HttpContext c, MediaLibraryService service, CancellationToken ct) => { var file = await service.DownloadAsync(CategoryWeb.Actor(c.User), id, ct); DownloadHeaders(c.Response, file, preview == true); return Results.File(file.Bytes, file.MimeType); });
        return endpoints;
    }
    private static async Task<IFormCollection> Form(HttpContext c, string[] allowed, CancellationToken ct)
    { if (c.Request.ContentLength > RequestCap) throw new MediaTooLargeException(); var maxBody = c.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>(); if (maxBody is { IsReadOnly: false }) maxBody.MaxRequestBodySize = Math.Min(maxBody.MaxRequestBodySize ?? RequestCap, RequestCap); if (!c.Request.HasFormContentType || c.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) != true) throw new MediaValidationException("Cần multipart/form-data."); IFormCollection form; try { form = await c.Request.ReadFormAsync(ct); } catch (InvalidDataException) { throw new MediaTooLargeException(); } if (form.Files.Count != 1 || form.Files[0].Name != "file" || form.Keys.Any(k => !allowed.Contains(k)) || form.Any(p => p.Value.Count != 1)) throw new MediaValidationException("Cần một file và đúng metadata fields; không nhận Drive ID/scope từ client."); return form; }
    private static long Version(IFormCollection form) { if (!long.TryParse(form["expectedVersion"], NumberStyles.None, CultureInfo.InvariantCulture, out var version)) throw new MediaValidationException("expectedVersion phải là integer không âm."); return version; }
}
