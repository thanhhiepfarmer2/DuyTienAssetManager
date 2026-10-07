using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.Server.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel(AuthApplicationService authService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool MustChangePassword => bool.TryParse(
        User.FindFirstValue(OpaqueSessionAuthenticationHandler.MustChangePasswordClaim),
        out var value) && value;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!await authService.ChangePasswordAsync(
                    userId, Input.CurrentPassword, Input.NewPassword, cancellationToken))
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu hiện tại không đúng.");
                return Page();
            }
        }
        catch (AuthValidationException exception)
        {
            ModelState.AddModelError(nameof(Input.NewPassword), exception.Message);
            return Page();
        }

        Response.Cookies.Delete(OpaqueSessionAuthenticationHandler.CookieName);
        return RedirectToPage("/Account/Login", new { changed = true });
    }

    public sealed class InputModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
        [DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        [DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu mới")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
