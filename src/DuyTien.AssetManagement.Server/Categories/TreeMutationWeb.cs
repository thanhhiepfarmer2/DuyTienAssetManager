using System.Text.Json.Serialization;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Authentication;
namespace DuyTien.AssetManagement.Server.Categories;
public static class TreeMutationWeb
{
    public static IEndpointRouteBuilder MapTask008TreeApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tree-changes").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (context.HttpContext.User.FindFirst(OpaqueSessionAuthenticationHandler.CredentialSourceClaim)?.Value != "bearer") return Results.Problem(statusCode: 403, title: "API này yêu cầu bearer token.");
            try { return await next(context); } catch (Exception e) when (CategoryWeb.IsExpected(e)) { return CategoryWeb.Error(e); }
        });
        group.MapPost("/preview", async (TreeMutationRequest input, HttpContext c, TreeMutationService service, CancellationToken ct) => Results.Ok(await service.PreviewAsync(CategoryWeb.Actor(c.User), input, ct)));
        group.MapPost("/commit", async (CommitRequest input, HttpContext c, TreeMutationService service, CancellationToken ct) =>
        {
            var keys = c.Request.Headers["Idempotency-Key"];
            if (keys.Count != 1) throw new CategoryValidationException("Cần một Idempotency-Key.");
            return Results.Ok(await service.CommitAsync(CategoryWeb.Actor(c.User), input.Request, input.ExpectedTreeRevision, input.ExpectedAssetRevision, input.PreviewHash, keys[0]!, input.ConfirmImpact, ct));
        });
        return endpoints;
    }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CommitRequest(TreeMutationRequest Request, long ExpectedTreeRevision, long ExpectedAssetRevision, string PreviewHash, bool ConfirmImpact);
}
