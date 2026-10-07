using DuyTien.AssetManagement.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.Server.Pages.Admin;

[Authorize(Roles = AuthRole.Admin)]
public sealed class SecurityEventsModel(AuthApplicationService authService) : PageModel
{
    public IReadOnlyList<AuthSecurityEvent> Events { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Events = await authService.ListSecurityEventsAsync(cancellationToken: cancellationToken);
}
