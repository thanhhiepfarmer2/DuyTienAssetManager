using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DuyTien.AssetManagement.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.Server.Pages.Admin.Users;

[Authorize(Roles = AuthRole.Admin)]
public sealed class IndexModel(AuthApplicationService authService) : PageModel
{
    public IReadOnlyList<AuthUser> Users { get; private set; } = [];
    public string? TemporaryPassword { get; private set; }

    [BindProperty]
    public CreateInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Users = await authService.ListUsersAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await authService.CreateUserAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    Input.Username,
                    Input.DisplayName,
                    cancellationToken);
                TemporaryPassword = result.TemporaryPassword;
                Response.Headers.CacheControl = "no-store";
                ModelState.Clear();
                Input = new CreateInput();
            }
            catch (Exception exception) when (exception is AuthValidationException or AuthConflictException)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
            }
        }

        Users = await authService.ListUsersAsync(cancellationToken);
        return Page();
    }

    public sealed class CreateInput
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập."), Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên hiển thị."), Display(Name = "Tên hiển thị")]
        public string DisplayName { get; set; } = string.Empty;
    }
}
