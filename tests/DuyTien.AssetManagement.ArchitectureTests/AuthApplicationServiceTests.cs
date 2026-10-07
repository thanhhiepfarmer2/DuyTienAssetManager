using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AuthApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Bootstrap_is_idempotent_and_creates_effective_admin_with_explicit_all()
    {
        var store = new InMemoryAuthSystemStore();
        var service = Service(store);

        var first = await service.BootstrapAdminAsync("12345678", "operator");
        var second = await service.BootstrapAdminAsync("different-but-valid", "operator");

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Single(store.Snapshot.Users);
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(1, AuthAuthorizationRules.CountActiveAdmins(store.Snapshot, Now));
        Assert.True(store.Snapshot.Users.Single().MustChangePassword);
    }

    [Fact]
    public async Task Last_admin_protection_counts_only_effective_assignment_with_all_scopes()
    {
        var store = new InMemoryAuthSystemStore();
        var service = Service(store);
        var first = await service.BootstrapAdminAsync("12345678", "operator");
        var second = AuthTestData.User("ADMIN-2", "admin_second_demo", "HASH");
        await store.MutateAsync(state =>
        {
            state.Users.Add(second);
            var future = AuthTestData.Assignment(
                "ASSIGN-FUTURE", second.UserId, AuthRole.Admin, Now.AddMinutes(1));
            state.RoleAssignments.Add(future);
            AuthTestData.AddAllScopes(state, future.AssignmentId);
            return true;
        });

        await Assert.ThrowsAsync<LastActiveAdminException>(
            () => service.SetUserLockedAsync("operator", first.UserId, true));

        await store.MutateAsync(state =>
        {
            var index = state.RoleAssignments.FindIndex(x => x.AssignmentId == "ASSIGN-FUTURE");
            state.RoleAssignments[index] = state.RoleAssignments[index] with
            {
                EffectiveFrom = Now.AddMinutes(-1)
            };
            return true;
        });

        await service.SetUserLockedAsync("operator", first.UserId, true);
        Assert.Equal(AuthUserState.Locked, store.Snapshot.Users.Single(x => x.UserId == first.UserId).State);
    }

    [Fact]
    public async Task Fifth_failed_login_sets_temporary_lockout()
    {
        var state = new AuthSystemState();
        state.Users.Add(AuthTestData.User("USER-1", "locked_demo", "HASH::USER-1::correct-pass"));
        var store = new InMemoryAuthSystemStore(state);
        var service = Service(store);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var result = await service.LoginAsync(new LoginRequest(
                "locked_demo", "wrong-pass", AuthClientType.Web, false, "127.0.0.1"));
            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        }

        var user = store.Snapshot.Users.Single();
        Assert.Equal(5, user.FailedAccessCount);
        Assert.Equal(Now.AddMinutes(15), user.LockoutEndAt);
    }

    [Fact]
    public async Task Refresh_rotation_keeps_marker_and_reuse_revokes_the_family()
    {
        var state = new AuthSystemState();
        state.Users.Add(AuthTestData.User("USER-1", "mobile_demo", "HASH::USER-1::12345678"));
        var store = new InMemoryAuthSystemStore(state);
        var service = Service(store);
        var login = await service.LoginAsync(new LoginRequest(
            "mobile_demo", "12345678", AuthClientType.Mobile, false, "device-demo"));

        var rotated = await service.RefreshAsync(login.RefreshToken!);
        var reused = await service.RefreshAsync(login.RefreshToken!);

        Assert.Equal(RefreshStatus.Success, rotated.Status);
        Assert.Equal(RefreshStatus.ReuseDetected, reused.Status);
        Assert.Contains(store.Snapshot.RefreshTokens, marker => marker.State == RefreshTokenState.Rotated);
        Assert.All(store.Snapshot.Sessions, session => Assert.NotNull(session.RevokedAt));
        Assert.All(store.Snapshot.RefreshTokens, marker => Assert.NotEqual(RefreshTokenState.Active, marker.State));
    }

    [Fact]
    public async Task Sixth_login_revokes_oldest_and_keeps_five_active_sessions()
    {
        var state = new AuthSystemState();
        state.Users.Add(AuthTestData.User("USER-1", "session_demo", "HASH::USER-1::12345678"));
        var store = new InMemoryAuthSystemStore(state);
        var time = new ManualTimeProvider(Now);
        var service = new AuthApplicationService(
            store, new TestPasswordHashService(), new AuthPolicyOptions(), time);

        for (var index = 0; index < 6; index++)
        {
            var result = await service.LoginAsync(new LoginRequest(
                "session_demo", "12345678", AuthClientType.Web, false, $"client-{index}"));
            Assert.Equal(LoginStatus.Success, result.Status);
            time.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(5, store.Snapshot.Sessions.Count(session => session.RevokedAt is null));
        Assert.Equal("SESSION_LIMIT", store.Snapshot.Sessions.Single(session => session.RevokedAt is not null).RevokedReason);
    }

    [Fact]
    public async Task Password_change_accepts_simple_eight_characters_and_revokes_existing_session()
    {
        var state = new AuthSystemState();
        state.Users.Add(AuthTestData.User("USER-1", "password_demo", "HASH::USER-1::old-pass"));
        var store = new InMemoryAuthSystemStore(state);
        var service = Service(store);
        var login = await service.LoginAsync(new LoginRequest(
            "password_demo", "old-pass", AuthClientType.Web, false, "client"));

        var changed = await service.ChangePasswordAsync("USER-1", "old-pass", "username");

        Assert.True(changed);
        Assert.Null(await service.ValidateAccessTokenAsync(login.AccessToken));
        Assert.False(store.Snapshot.Users.Single().MustChangePassword);
        var relogin = await service.LoginAsync(new LoginRequest(
            "password_demo", "username", AuthClientType.Web, false, "client-new"));
        Assert.Equal(LoginStatus.Success, relogin.Status);
    }

    [Fact]
    public void Active_admin_requires_one_effective_assignment_with_all_three_explicit_scopes()
    {
        var state = new AuthSystemState();
        var user = AuthTestData.User("ADMIN-1", "admin_demo", "HASH");
        state.Users.Add(user);
        var assignment = AuthTestData.Assignment(
            "ASSIGN-1", user.UserId, AuthRole.Admin, Now.AddMinutes(-1));
        state.RoleAssignments.Add(assignment);
        state.RoleAssignmentScopes.AddRange(
        [
            new AuthScopeGrant("S1", assignment.AssignmentId, AuthScopeType.OrgUnit, AuthScopeGrant.All, true, 1),
            new AuthScopeGrant("S2", assignment.AssignmentId, AuthScopeType.Location, AuthScopeGrant.All, true, 1)
        ]);

        Assert.Equal(0, AuthAuthorizationRules.CountActiveAdmins(state, Now));
        state.RoleAssignmentScopes.Add(new AuthScopeGrant(
            "S3", assignment.AssignmentId, AuthScopeType.CategorySubtree, AuthScopeGrant.All, true, 1));
        Assert.Equal(1, AuthAuthorizationRules.CountActiveAdmins(state, Now));

        state.RoleAssignments[0] = assignment with { State = RoleAssignmentState.Revoked };
        Assert.Equal(0, AuthAuthorizationRules.CountActiveAdmins(state, Now));
        state.RoleAssignments[0] = assignment with { EffectiveTo = Now };
        Assert.Equal(0, AuthAuthorizationRules.CountActiveAdmins(state, Now));
    }

    private static AuthApplicationService Service(InMemoryAuthSystemStore store) =>
        new(
            store,
            new TestPasswordHashService(),
            new AuthPolicyOptions(),
            new ManualTimeProvider(Now));
}

internal static class AuthTestData
{
    public static AuthUser User(string id, string username, string hash) => new(
        id,
        username,
        username.ToUpperInvariant(),
        username,
        hash,
        AuthUserState.Active,
        false,
        0,
        null,
        null,
        1,
        Now,
        Now,
        "test",
        Now,
        "test",
        1);

    public static AuthRoleAssignment Assignment(
        string id,
        string userId,
        string role,
        DateTimeOffset effectiveFrom) => new(
            id,
            userId,
            role,
            null,
            "MANUAL",
            RoleAssignmentState.Active,
            effectiveFrom,
            null,
            Now,
            "test",
            1);

    public static void AddAllScopes(AuthSystemState state, string assignmentId)
    {
        foreach (var type in Enum.GetValues<AuthScopeType>())
        {
            state.RoleAssignmentScopes.Add(new AuthScopeGrant(
                Guid.NewGuid().ToString("N"), assignmentId, type, AuthScopeGrant.All, true, 1));
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
}
