using System.Security.Claims;
using DuyTien.AssetManagement.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DuyTien.AssetManagement.Server.Authentication;

public static class AuthApi
{
    public static IEndpointRouteBuilder MapTask005AuthApi(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync).AllowAnonymous();
        auth.MapPost("/refresh", RefreshAsync).AllowAnonymous();

        auth.MapPost("/logout", LogoutAsync).RequireAuthorization().AddEndpointFilter(RequireBearerAsync);
        auth.MapPost("/logout-all", LogoutAllAsync).RequireAuthorization().AddEndpointFilter(RequireBearerAsync);
        auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization().AddEndpointFilter(RequireBearerAsync);
        endpoints.MapGet("/api/me", Me).RequireAuthorization().AddEndpointFilter(RequireBearerAsync);

        var admin = endpoints.MapGroup("/api/admin")
            .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRole.Admin })
            .AddEndpointFilter(RequireBearerAsync);
        admin.MapGet("/users", ListUsersAsync);
        admin.MapGet("/users/{userId}", GetUserAsync);
        admin.MapPost("/users", CreateUserAsync);
        admin.MapPatch("/users/{userId}", UpdateUserAsync);
        admin.MapPost("/users/{userId}/lock", LockUserAsync);
        admin.MapPost("/users/{userId}/unlock", UnlockUserAsync);
        admin.MapPost("/users/{userId}/reset-password", ResetPasswordAsync);
        admin.MapPut("/users/{userId}/role-assignments", ReplaceAssignmentsAsync);
        admin.MapGet("/authorization-catalog", AuthorizationCatalog);
        admin.MapGet("/security-events", ListSecurityEventsAsync);
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        ApiLoginRequest input,
        HttpContext context,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await service.LoginAsync(
            new LoginRequest(input.Username ?? string.Empty, input.Password ?? string.Empty,
                AuthClientType.Mobile, false, clientKey),
            cancellationToken);
        return result.Status switch
        {
            LoginStatus.Success => Results.Ok(new
            {
                result.AccessToken,
                result.RefreshToken,
                result.AccessExpiresAt,
                result.RefreshExpiresAt,
                result.AbsoluteExpiresAt,
                result.MustChangePassword
            }),
            LoginStatus.RateLimited => Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Quá nhiều lần đăng nhập."),
            _ => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Tên đăng nhập hoặc mật khẩu không đúng.")
        };
    }

    private static async Task<IResult> RefreshAsync(
        ApiRefreshRequest input,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RefreshAsync(input.RefreshToken, cancellationToken);
        return result.Status == RefreshStatus.Success
            ? Results.Ok(new
            {
                result.AccessToken,
                result.RefreshToken,
                result.AccessExpiresAt,
                result.RefreshExpiresAt
            })
            : Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Refresh token không hợp lệ.");
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        await service.LogoutAsync(BearerToken(context), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        await service.LogoutAllAsync(UserId(principal), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ApiChangePasswordRequest input,
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var changed = await service.ChangePasswordAsync(
                UserId(principal), input.CurrentPassword ?? string.Empty,
                input.NewPassword ?? string.Empty, cancellationToken);
            return changed
                ? Results.NoContent()
                : Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["currentPassword"] = ["Mật khẩu hiện tại không đúng."]
                });
        }
        catch (AuthValidationException exception)
        {
            return Validation(exception);
        }
    }

    private static IResult Me(ClaimsPrincipal principal) => Results.Ok(new
    {
        UserId = UserId(principal),
        Username = principal.Identity?.Name,
        DisplayName = principal.FindFirstValue("display_name"),
        Roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
        MustChangePassword = bool.TryParse(
            principal.FindFirstValue(OpaqueSessionAuthenticationHandler.MustChangePasswordClaim),
            out var mustChange) && mustChange
    });

    private static async Task<IResult> ListUsersAsync(
        AuthApplicationService service,
        CancellationToken cancellationToken) =>
        Results.Ok((await service.ListUsersAsync(cancellationToken)).Select(UserView));

    private static async Task<IResult> GetUserAsync(
        string userId,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        var user = await service.GetUserAsync(userId, cancellationToken);
        return user is null ? Results.NotFound() : Results.Ok(UserView(user));
    }

    private static async Task<IResult> CreateUserAsync(
        ApiCreateUserRequest input,
        HttpContext context,
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateUserAsync(
                UserId(principal), input.Username ?? string.Empty,
                input.DisplayName ?? string.Empty, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Created($"/api/admin/users/{result.UserId}", result);
        }
        catch (AuthValidationException exception)
        {
            return Validation(exception);
        }
        catch (AuthConflictException exception)
        {
            return Results.Conflict(new ProblemDetails { Title = exception.Message, Status = 409 });
        }
    }

    private static async Task<IResult> UpdateUserAsync(
        string userId,
        ApiUpdateUserRequest input,
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken) =>
        await AdminMutationAsync(() => service.UpdateUserAsync(
            UserId(principal), userId, input.DisplayName ?? string.Empty, cancellationToken));

    private static async Task<IResult> LockUserAsync(
        string userId,
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken) =>
        await AdminMutationAsync(() => service.SetUserLockedAsync(
            UserId(principal), userId, true, cancellationToken));

    private static async Task<IResult> UnlockUserAsync(
        string userId,
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken) =>
        await AdminMutationAsync(() => service.SetUserLockedAsync(
            UserId(principal), userId, false, cancellationToken));

    private static async Task<IResult> ResetPasswordAsync(
        string userId,
        HttpContext context,
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ResetPasswordAsync(
                UserId(principal), userId, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result);
        }
        catch (AuthNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> ReplaceAssignmentsAsync(
        string userId,
        ApiReplaceAssignmentsRequest input,
        ClaimsPrincipal principal,
        AuthApplicationService service,
        CancellationToken cancellationToken)
    {
        var values = (input.Assignments ?? [])
            .Select(assignment => new RoleAssignmentInput(
                assignment.RoleCode ?? string.Empty,
                assignment.EffectiveFrom,
                assignment.EffectiveTo,
                (assignment.Scopes ?? []).Select(scope => new ScopeGrantInput(
                    scope.ScopeType,
                    scope.ScopeId ?? string.Empty,
                    scope.IncludeDescendants)).ToArray(),
                assignment.PositionId))
            .ToArray();
        return await AdminMutationAsync(() => service.ReplaceRoleAssignmentsAsync(
            UserId(principal), userId, values, cancellationToken));
    }

    private static IResult AuthorizationCatalog() => Results.Ok(new
    {
        DirectUserPermissionsAllowed = false,
        Roles = AuthRole.All.Select(role => new
        {
            Code = role,
            Permissions = AuthRoleCatalog.GetPermissions(role).OrderBy(value => value).ToArray()
        })
    });

    private static async Task<IResult> ListSecurityEventsAsync(
        AuthApplicationService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListSecurityEventsAsync(cancellationToken: cancellationToken));

    private static async Task<IResult> AdminMutationAsync(Func<Task> mutation)
    {
        try
        {
            await mutation();
            return Results.NoContent();
        }
        catch (AuthNotFoundException)
        {
            return Results.NotFound();
        }
        catch (LastActiveAdminException exception)
        {
            return Results.Conflict(new ProblemDetails { Title = exception.Message, Status = 409 });
        }
        catch (AuthValidationException exception)
        {
            return Validation(exception);
        }
    }

    private static object UserView(AuthUser user) => new
    {
        user.UserId,
        user.Username,
        user.DisplayName,
        State = user.State.ToString(),
        user.MustChangePassword,
        user.LockoutEndAt,
        user.CreatedAt,
        user.UpdatedAt,
        user.Version
    };

    private static IResult Validation(Exception exception) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [exception.Message]
        });

    private static string UserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
        throw new InvalidOperationException("Authenticated principal has no user id.");

    private static string? BearerToken(HttpContext context)
    {
        var value = context.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? value["Bearer ".Length..].Trim()
            : null;
    }

    private static ValueTask<object?> RequireBearerAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var source = context.HttpContext.User.FindFirstValue(
            OpaqueSessionAuthenticationHandler.CredentialSourceClaim);
        return string.Equals(source, "bearer", StringComparison.Ordinal)
            ? next(context)
            : ValueTask.FromResult<object?>(Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "API này yêu cầu bearer token."));
    }

    public sealed record ApiLoginRequest(string? Username, string? Password);
    public sealed record ApiRefreshRequest(string? RefreshToken);
    public sealed record ApiChangePasswordRequest(string? CurrentPassword, string? NewPassword);
    public sealed record ApiCreateUserRequest(string? Username, string? DisplayName);
    public sealed record ApiUpdateUserRequest(string? DisplayName);
    public sealed record ApiReplaceAssignmentsRequest(IReadOnlyList<ApiRoleAssignment>? Assignments);
    public sealed record ApiRoleAssignment(
        string? RoleCode,
        DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo,
        string? PositionId,
        IReadOnlyList<ApiScopeGrant>? Scopes);
    public sealed record ApiScopeGrant(
        AuthScopeType ScopeType,
        string? ScopeId,
        bool IncludeDescendants);
}
