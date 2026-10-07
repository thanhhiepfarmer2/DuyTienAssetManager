using DuyTien.AssetManagement.Application.Authentication;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class ScopeAuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Multiple_roles_union_permissions_but_do_not_cross_combine_scopes()
    {
        var state = StateWithMultiRoleUser();
        var hierarchy = DemoHierarchy();
        var authorization = new ScopeAuthorizationService();

        var branchA = ResourceScope.Required("OU-A", "LOC-A", "CAT-A");
        var branchB = ResourceScope.Required("OU-B", "LOC-B", "CAT-B");

        Assert.True(authorization.IsAllowed(
            state, "USER-1", AuthPermission.AssetsUpdate, branchA, hierarchy, Now));
        Assert.False(authorization.IsAllowed(
            state, "USER-1", AuthPermission.AssetsUpdate, branchB, hierarchy, Now));
        Assert.True(authorization.IsAllowed(
            state, "USER-1", AuthPermission.DocumentsManage, branchB, hierarchy, Now));
        Assert.False(authorization.IsAllowed(
            state, "USER-1", AuthPermission.DocumentsManage, branchA, hierarchy, Now));
    }

    [Fact]
    public void Missing_required_scope_fails_closed()
    {
        var authorization = new ScopeAuthorizationService();

        Assert.False(authorization.IsAllowed(
            StateWithMultiRoleUser(),
            "USER-1",
            AuthPermission.AssetsRead,
            ResourceScope.Required("OU-A", null, "CAT-A"),
            DemoHierarchy(),
            Now));
    }

    [Fact]
    public void Move_requires_source_and_destination_to_match_the_same_assignment()
    {
        var authorization = new ScopeAuthorizationService();
        var state = StateWithMultiRoleUser();
        var hierarchy = DemoHierarchy();

        Assert.True(authorization.IsMoveAllowed(
            state,
            "USER-1",
            AuthPermission.AssetsMove,
            ResourceScope.Required("OU-A", "LOC-A", "CAT-A"),
            ResourceScope.Required("OU-A-CHILD", "LOC-A-CHILD", "CAT-A-CHILD"),
            hierarchy,
            Now));
        Assert.False(authorization.IsMoveAllowed(
            state,
            "USER-1",
            AuthPermission.AssetsMove,
            ResourceScope.Required("OU-A", "LOC-A", "CAT-A"),
            ResourceScope.Required("OU-B", "LOC-B", "CAT-B"),
            hierarchy,
            Now));
    }

    private static AuthSystemState StateWithMultiRoleUser()
    {
        var state = new AuthSystemState();
        state.Users.Add(AuthTestData.User("USER-1", "multi_role_demo", "HASH"));
        state.RoleAssignments.AddRange(
        [
            AuthTestData.Assignment("ASSIGN-ASSET", "USER-1", AuthRole.AssetEditor, Now.AddDays(-1)),
            AuthTestData.Assignment("ASSIGN-DOC", "USER-1", AuthRole.DocumentManager, Now.AddDays(-1))
        ]);
        foreach (var (assignment, suffix) in new[] { ("ASSIGN-ASSET", "A"), ("ASSIGN-DOC", "B") })
        {
            state.RoleAssignmentScopes.AddRange(
            [
                new AuthScopeGrant(Guid.NewGuid().ToString("N"), assignment, AuthScopeType.OrgUnit, $"OU-{suffix}", true, 1),
                new AuthScopeGrant(Guid.NewGuid().ToString("N"), assignment, AuthScopeType.Location, $"LOC-{suffix}", true, 1),
                new AuthScopeGrant(Guid.NewGuid().ToString("N"), assignment, AuthScopeType.CategorySubtree, $"CAT-{suffix}", true, 1)
            ]);
        }

        return state;
    }

    private static ScopeHierarchySet DemoHierarchy() => new(
        ScopeHierarchy.Create(AuthScopeType.OrgUnit,
        [
            new ScopeNode("OU-A", null), new ScopeNode("OU-A-CHILD", "OU-A"),
            new ScopeNode("OU-B", null)
        ]),
        ScopeHierarchy.Create(AuthScopeType.Location,
        [
            new ScopeNode("LOC-A", null), new ScopeNode("LOC-A-CHILD", "LOC-A"),
            new ScopeNode("LOC-B", null)
        ]),
        ScopeHierarchy.Create(AuthScopeType.CategorySubtree,
        [
            new ScopeNode("CAT-A", null), new ScopeNode("CAT-A-CHILD", "CAT-A"),
            new ScopeNode("CAT-B", null)
        ]));
}
