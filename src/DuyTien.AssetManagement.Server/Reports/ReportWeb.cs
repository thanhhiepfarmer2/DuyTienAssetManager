using System.Security.Claims;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Server.Authentication;
namespace DuyTien.AssetManagement.Server.Reports;

public static class ReportWeb
{
    public static readonly IReadOnlyDictionary<string, string> StatusNames = new Dictionary<string, string>(StringComparer.Ordinal)
    { ["MATCHES"] = "Kết quả báo cáo", ["OUT"] = "Hết số lượng (=0)", ["LOW"] = "Sắp hết (0<quantity<=minimum)", ["AVAILABLE"] = "Có số lượng, vượt ngưỡng đã nhập", ["UNKNOWN"] = "UNKNOWN — chưa có số lượng", ["MISSING_MINIMUM"] = "Thiếu ngưỡng minimum", ["NOT_TRACKED"] = "Không theo dõi tồn / dịch vụ", ["TRACKING_UNCONFIRMED"] = "Chưa xác nhận theo dõi tồn", ["SOURCE_MISSING"] = "Thiếu nguồn snapshot", ["INVALID_SNAPSHOT"] = "Snapshot cần đối soát" };
    public static string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static bool Expected(Exception e) => e is ReportValidationException or ReportForbiddenException or ReportNotFoundException or ExternalStorageException;
    public static int Status(Exception e) => e switch { ReportValidationException => 422, ReportForbiddenException => 403, ReportNotFoundException => 404, _ => 503 };
    public static string Message(Exception e) => e is ExternalStorageException ? "Chưa đọc được nguồn báo cáo. Giữ bộ lọc và thử lại; không coi lỗi dịch vụ là báo cáo rỗng hoặc tổng0." : e.Message;
    public static IEndpointRouteBuilder MapTask011ReportApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/reports").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        { context.HttpContext.Response.Headers.CacheControl = "no-store"; if (context.HttpContext.User.FindFirstValue(OpaqueSessionAuthenticationHandler.CredentialSourceClaim) != "bearer") return Results.Problem(statusCode: 403, title: "API yêu cầu bearer token."); try { return await next(context); } catch (Exception e) when (Expected(e)) { return Results.Problem(statusCode: Status(e), title: Message(e)); } });
        foreach (var (route, code) in new[] { ("assets", "R01"), ("assets-by-category", "R02"), ("stock-empty", "R03"), ("stock-low", "R04") })
        {
            api.MapGet("/" + route, async ([AsParameters] ReportQuery q, ClaimsPrincipal user, ReportService service, CancellationToken ct) => Results.Ok(await service.ReadAsync(Actor(user), code, q, ct)));
            api.MapGet("/" + route + "/export", async ([AsParameters] ReportQuery q, HttpContext context, ReportService service, CancellationToken ct) =>
            { var result = await service.ExportAsync(Actor(context.User), code, q, ct); context.Response.Headers.XContentTypeOptions = "nosniff"; return Results.File(result.Bytes, result.ContentType, result.FileName); });
        }
        return endpoints;
    }
}
