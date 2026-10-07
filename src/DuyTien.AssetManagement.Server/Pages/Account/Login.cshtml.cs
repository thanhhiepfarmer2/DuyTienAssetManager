using System.ComponentModel.DataAnnotations;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.Server.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(AuthApplicationService authService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        LoginResult result;
        try
        {
            result = await authService.LoginAsync(new LoginRequest(
                Input.Username,
                Input.Password,
                AuthClientType.Web,
                Input.RememberMe,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                cancellationToken);
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Dịch vụ đăng nhập hiện chưa sẵn sàng.");
            return Page();
        }

        if (result.Status == LoginStatus.RateLimited)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ModelState.AddModelError(string.Empty, "Quá nhiều lần đăng nhập. Vui lòng thử lại sau.");
            return Page();
        }

        if (result.Status != LoginStatus.Success || result.AccessToken is null)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            return Page();
        }

        Response.Cookies.Append(
            OpaqueSessionAuthenticationHandler.CookieName,
            result.AccessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = Input.RememberMe && !result.MustChangePassword
                    ? result.AbsoluteExpiresAt
                    : null
            });
        if (result.MustChangePassword)
        {
            return RedirectToPage("/Account/ChangePassword");
        }

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Nhập tên đăng nhập.")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhập mật khẩu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }
}
