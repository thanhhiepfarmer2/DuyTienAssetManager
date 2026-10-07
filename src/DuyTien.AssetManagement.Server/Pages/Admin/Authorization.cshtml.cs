using DuyTien.AssetManagement.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.Server.Pages.Admin;

[Authorize(Roles = AuthRole.Admin)]
public sealed class AuthorizationModel : PageModel
{
    public IReadOnlyList<string> Roles => AuthRole.All;
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Permissions =>
        AuthRole.All.ToDictionary(role => role, AuthRoleCatalog.GetPermissions, StringComparer.Ordinal);

    public void OnGet()
    {
    }
}
