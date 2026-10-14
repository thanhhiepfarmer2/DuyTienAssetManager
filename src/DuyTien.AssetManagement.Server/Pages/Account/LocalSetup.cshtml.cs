using System.ComponentModel.DataAnnotations;
using System.Net;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Infrastructure.Local;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Account;

public sealed class LocalSetupModel(AuthApplicationService auth, IServiceProvider services) : PageModel
{
    [BindProperty, Required, MinLength(8)] public string Password { get; set; } = "";
    [BindProperty, Required] public string Confirmation { get; set; } = "";
    private async Task<bool> Available() => services.GetService<LocalWorkspace>() is { } local && HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip) && await ((IAuthSystemStore)local).ReadAsync(s => s.Users.Count == 0);
    public async Task<IActionResult> OnGetAsync() => await Available() ? Page() : NotFound();
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await Available()) return NotFound(); if (Password != Confirmation) ModelState.AddModelError("Confirmation", "Hai mật khẩu chưa khớp.");
        if (!ModelState.IsValid) return Page(); var result = await auth.BootstrapAdminAsync(Password, "local-operator-setup", ct);
        if (!result.Created) return StatusCode(409); return RedirectToPage("Login");
    }
}
