using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DuyTien.AssetManagement.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.Server.Pages.Admin.Users;

[Authorize(Roles = AuthRole.Admin)]
public sealed class DetailModel(AuthApplicationService authService, TimeProvider timeProvider) : PageModel
{
    public string Id { get; private set; } = string.Empty;
    public AuthUser? Account { get; private set; }
    public IReadOnlyList<AuthRoleAssignmentWithScopes> AssignmentDetails { get; private set; } = [];
    public IReadOnlyList<string> Roles => AuthRole.All;
    public string? TemporaryPassword { get; private set; }
    public string? Message { get; private set; }

    [BindProperty]
    public EditInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken)) return NotFound();
        Input.DisplayName = Account!.DisplayName;
        if (AssignmentDetails.Count == 1)
        {
            var detail = AssignmentDetails[0];
            // The replacement form can represent one grant in each dimension and one shared flag.
            if (detail.Scopes.Count == 3 &&
                Enum.GetValues<AuthScopeType>().All(type => detail.Scopes.Count(scope => scope.ScopeType == type) == 1) &&
                detail.Scopes.Select(scope => scope.IncludeDescendants).Distinct().Count() == 1)
            {
                Input.RoleCode = detail.Assignment.RoleCode;
                Input.OrgUnitScopeId = detail.Scopes.Single(scope => scope.ScopeType == AuthScopeType.OrgUnit).ScopeId;
                Input.LocationScopeId = detail.Scopes.Single(scope => scope.ScopeType == AuthScopeType.Location).ScopeId;
                Input.CategoryScopeId = detail.Scopes.Single(scope => scope.ScopeType == AuthScopeType.CategorySubtree).ScopeId;
                Input.IncludeDescendants = detail.Scopes[0].IncludeDescendants;
            }
        }
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(string id, CancellationToken cancellationToken) =>
        await MutateAsync(id, () => authService.UpdateUserAsync(ActorId(), id, Input.DisplayName, cancellationToken), "Đã cập nhật.", cancellationToken);

    public async Task<IActionResult> OnPostLockAsync(string id, CancellationToken cancellationToken) =>
        await MutateAsync(id, () => authService.SetUserLockedAsync(ActorId(), id, true, cancellationToken), "Đã khóa tài khoản.", cancellationToken);

    public async Task<IActionResult> OnPostUnlockAsync(string id, CancellationToken cancellationToken) =>
        await MutateAsync(id, () => authService.SetUserLockedAsync(ActorId(), id, false, cancellationToken), "Đã mở khóa tài khoản.", cancellationToken);

    public async Task<IActionResult> OnPostResetAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.ResetPasswordAsync(ActorId(), id, cancellationToken);
            TemporaryPassword = result.TemporaryPassword;
            Response.Headers.CacheControl = "no-store";
            Message = "Đã reset mật khẩu và thu hồi các phiên cũ.";
        }
        catch (AuthNotFoundException)
        {
            return NotFound();
        }

        await LoadAsync(id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync(string id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(id, cancellationToken);
            return Page();
        }

        var input = new RoleAssignmentInput(
            Input.RoleCode,
            timeProvider.GetUtcNow(),
            null,
            [
                new ScopeGrantInput(AuthScopeType.OrgUnit, Input.OrgUnitScopeId, Input.IncludeDescendants),
                new ScopeGrantInput(AuthScopeType.Location, Input.LocationScopeId, Input.IncludeDescendants),
                new ScopeGrantInput(AuthScopeType.CategorySubtree, Input.CategoryScopeId, Input.IncludeDescendants)
            ]);
        return await MutateAsync(
            id,
            () => authService.ReplaceRoleAssignmentsAsync(ActorId(), id, [input], cancellationToken),
            "Đã thay assignment và thu hồi các phiên cũ.",
            cancellationToken);
    }

    private async Task<IActionResult> MutateAsync(
        string id,
        Func<Task> mutation,
        string success,
        CancellationToken cancellationToken)
    {
        try
        {
            await mutation();
            Message = success;
        }
        catch (AuthNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is AuthValidationException or LastActiveAdminException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        await LoadAsync(id, cancellationToken);
        return Page();
    }

    private async Task<bool> LoadAsync(string id, CancellationToken cancellationToken)
    {
        Id = id;
        Account = await authService.GetUserAsync(id, cancellationToken);
        if (Account is null) return false;
        AssignmentDetails = await authService.ListRoleAssignmentsWithScopesAsync(id, cancellationToken);
        if (string.IsNullOrEmpty(Input.DisplayName)) Input.DisplayName = Account.DisplayName;
        return true;
    }

    private string ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public sealed class EditInput
    {
        [Display(Name = "Tên hiển thị")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "Role")]
        public string RoleCode { get; set; } = AuthRole.Viewer;

        [Display(Name = "OrgUnit scope ID")]
        public string OrgUnitScopeId { get; set; } = string.Empty;

        [Display(Name = "Location scope ID")]
        public string LocationScopeId { get; set; } = string.Empty;

        [Display(Name = "Category scope ID")]
        public string CategoryScopeId { get; set; } = string.Empty;

        [Display(Name = "Bao gồm cấp con")]
        public bool IncludeDescendants { get; set; }
    }
}
