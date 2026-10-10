using System.Security.Claims;
using System.Text.Encodings.Web;
using DuyTien.AssetManagement.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DuyTien.AssetManagement.Server.Authentication;

public sealed class OpaqueSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthApplicationService authService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DuyTienOpaqueSession";
    public const string CookieName = "dt_session";
    public const string CredentialSourceClaim = "credential_source";
    internal const string StorageUnavailableItem = "DuyTien.AuthStorageUnavailable";
    public const string MustChangePasswordClaim = "must_change_password";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var (token, source) = ResolveCredential();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        AuthenticatedSession? session;
        try
        {
            session = await authService.ValidateAccessTokenAsync(token, Context.RequestAborted);
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                "Auth session validation failed with {ExceptionType}; no credential was logged.",
                exception.GetType().Name);
            Context.Items[StorageUnavailableItem] = true;
            return AuthenticateResult.Fail("Session validation unavailable.");
        }

        if (session is null)
        {
            return AuthenticateResult.Fail("Invalid or expired session.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId),
            new(ClaimTypes.Name, session.Username),
            new("display_name", session.DisplayName),
            new("session_id", session.SessionId),
            new(CredentialSourceClaim, source),
            new(MustChangePasswordClaim, session.MustChangePassword ? bool.TrueString : bool.FalseString)
        };
        claims.AddRange(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (AcceptsHtml())
        {
            var returnUrl = Uri.EscapeDataString(Request.PathBase + Request.Path + Request.QueryString);
            Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        }
        else
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
        }

        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        if (AcceptsHtml())
        {
            Response.Redirect("/Account/AccessDenied");
        }
        else
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
        }

        return Task.CompletedTask;
    }

    private (string? Token, string Source) ResolveCredential()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return (authorization["Bearer ".Length..].Trim(), "bearer");
        }

        return (Request.Cookies.TryGetValue(CookieName, out var cookie) ? cookie : null, "cookie");
    }

    private bool AcceptsHtml() =>
        !Request.Path.StartsWithSegments("/api") &&
        Request.Headers.Accept.Any(value =>
            value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
}
