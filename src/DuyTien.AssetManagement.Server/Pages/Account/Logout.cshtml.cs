using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.Server.Pages.Account;

[Authorize]
public sealed class LogoutModel(AuthApplicationService authService) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(OpaqueSessionAuthenticationHandler.CookieName, out var token);
        await authService.LogoutAsync(token, cancellationToken);
        Response.Cookies.Delete(OpaqueSessionAuthenticationHandler.CookieName);
        return RedirectToPage("/Account/Login");
    }
}
