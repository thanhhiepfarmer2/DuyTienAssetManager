using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Pages.Admin.Users;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AuthRazorUserDetailTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reopened_detail_prefills_persisted_role_and_scopes()
    {
        var (service, clock, userId) = await FixtureAsync();
        await service.ReplaceRoleAssignmentsAsync("operator", userId, [Assignment(AuthRole.AssetEditor, "A")]);
        var page = new DetailModel(service, clock);

        Assert.IsType<PageResult>(await page.OnGetAsync(userId, default));

        Assert.Equal(AuthRole.AssetEditor, page.Input.RoleCode);
        Assert.Equal("OU-DEMO-A", page.Input.OrgUnitScopeId);
        Assert.Equal("LOC-DEMO-A", page.Input.LocationScopeId);
        Assert.Equal("CAT-DEMO-A", page.Input.CategoryScopeId);
        Assert.True(page.Input.IncludeDescendants);
        Assert.Equal(3, Assert.Single(page.AssignmentDetails).Scopes.Count);
    }

    [Fact]
    public async Task Multiple_assignments_keep_their_own_scopes_in_detail()
    {
        var (service, clock, userId) = await FixtureAsync();
        await service.ReplaceRoleAssignmentsAsync("operator", userId,
            [Assignment(AuthRole.AssetEditor, "A"), Assignment(AuthRole.DocumentManager, "B")]);
        var page = new DetailModel(service, clock);

        await page.OnGetAsync(userId, default);

        Assert.Equal(2, page.AssignmentDetails.Count);
        var editor = page.AssignmentDetails.Single(detail => detail.Assignment.RoleCode == AuthRole.AssetEditor);
        Assert.All(editor.Scopes, scope => Assert.EndsWith("-A", scope.ScopeId));
        var documents = page.AssignmentDetails.Single(detail => detail.Assignment.RoleCode == AuthRole.DocumentManager);
        Assert.All(documents.Scopes, scope => Assert.EndsWith("-B", scope.ScopeId));
        Assert.Empty(page.Input.OrgUnitScopeId); // No misleading single-assignment prefill.
    }

    [Fact]
    public async Task Invalid_assignment_form_keeps_attempted_input_and_persisted_scope_display()
    {
        var (service, clock, userId) = await FixtureAsync();
        await service.ReplaceRoleAssignmentsAsync("operator", userId, [Assignment(AuthRole.Viewer, "A")]);
        var page = new DetailModel(service, clock)
        {
            Input = new DetailModel.EditInput { RoleCode = AuthRole.AssetEditor, OrgUnitScopeId = "attempted" }
        };
        page.ModelState.AddModelError("Input.LocationScopeId", "Scope required");

        Assert.IsType<PageResult>(await page.OnPostAssignAsync(userId, default));

        Assert.Equal("attempted", page.Input.OrgUnitScopeId);
        Assert.Equal(AuthRole.AssetEditor, page.Input.RoleCode);
        Assert.Equal(AuthRole.Viewer, Assert.Single(page.AssignmentDetails).Assignment.RoleCode);
        Assert.Contains(page.AssignmentDetails[0].Scopes, scope => scope.ScopeId == "OU-DEMO-A");
    }

    private static RoleAssignmentInput Assignment(string role, string suffix) => new(
        role, Now.AddMinutes(-1), null,
        [new(AuthScopeType.OrgUnit, "OU-DEMO-" + suffix, true),
         new(AuthScopeType.Location, "LOC-DEMO-" + suffix, true),
         new(AuthScopeType.CategorySubtree, "CAT-DEMO-" + suffix, true)]);

    private static async Task<(AuthApplicationService Service, TimeProvider Clock, string UserId)> FixtureAsync()
    {
        var clock = new ManualTimeProvider(Now);
        var service = new AuthApplicationService(new InMemoryAuthSystemStore(),
            new TestPasswordHashService(), new AuthPolicyOptions(), clock);
        await service.BootstrapAdminAsync("12345678", "operator");
        var user = await service.CreateUserAsync("operator", "scope_ui_demo", "Fake UI account");
        return (service, clock, user.UserId);
    }
}
