using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AuthSessionTimeoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, 30, 8)]
    [InlineData(true, 1440, 168)]
    public async Task Web_issues_the_accepted_idle_and_absolute_deadlines(bool remember, int idleMinutes, int absoluteHours)
    {
        var (service, store, clock) = Fixture();
        var login = await service.LoginAsync(new LoginRequest("timeout_demo", "abcdefgh", AuthClientType.Web, remember, "test"));
        Assert.Equal(LoginStatus.Success, login.Status);
        var session = Assert.Single(store.Snapshot.Sessions);
        Assert.Equal(clock.GetUtcNow().AddMinutes(idleMinutes), session.IdleExpiresAt);
        Assert.Equal(clock.GetUtcNow().AddHours(absoluteHours), session.AbsoluteExpiresAt);
        Assert.Equal(session.AbsoluteExpiresAt, login.AbsoluteExpiresAt);
        Assert.Equal(remember, session.RememberMe);
    }

    [Theory]
    [InlineData(false, -1, true)]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, -1, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    public async Task Web_idle_boundary_rejects_expiry_without_reviving_the_session(bool remember, long ticks, bool valid)
    {
        var (service, store, clock) = Fixture();
        var login = await service.LoginAsync(new LoginRequest("timeout_demo", "abcdefgh", AuthClientType.Web, remember, "test"));
        var before = Assert.Single(store.Snapshot.Sessions);
        clock.Advance(before.IdleExpiresAt - clock.GetUtcNow() + TimeSpan.FromTicks(ticks));
        var authenticated = await service.ValidateAccessTokenAsync(login.AccessToken);
        Assert.Equal(valid, authenticated is not null);
        if (!valid)
        {
            Assert.Equal(before, Assert.Single(store.Snapshot.Sessions));
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Null(await service.ValidateAccessTokenAsync(login.AccessToken));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Web_sliding_idle_never_extends_the_original_absolute_deadline(bool remember)
    {
        var (service, store, clock) = Fixture();
        var login = await service.LoginAsync(new LoginRequest("timeout_demo", "abcdefgh", AuthClientType.Web, remember, "test"));
        var absolute = Assert.Single(store.Snapshot.Sessions).AbsoluteExpiresAt;
        var step = remember ? TimeSpan.FromHours(12) : TimeSpan.FromMinutes(20);
        var lastValid = absolute.AddSeconds(-1);
        while (clock.GetUtcNow().Add(step) < lastValid)
        {
            clock.Advance(step);
            Assert.NotNull(await service.ValidateAccessTokenAsync(login.AccessToken));
            Assert.Equal(absolute, Assert.Single(store.Snapshot.Sessions).AbsoluteExpiresAt);
        }
        clock.Advance(lastValid - clock.GetUtcNow());
        Assert.NotNull(await service.ValidateAccessTokenAsync(login.AccessToken));
        var final = Assert.Single(store.Snapshot.Sessions);
        Assert.Equal(absolute, final.AbsoluteExpiresAt);
        Assert.Equal(absolute, final.IdleExpiresAt);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(await service.ValidateAccessTokenAsync(login.AccessToken));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task Refresh_idle_boundary_uses_fourteen_days_without_shortening_policy(long ticks, bool valid)
    {
        var (service, store, clock) = Fixture();
        var login = await service.LoginAsync(new LoginRequest("timeout_demo", "abcdefgh", AuthClientType.Mobile, false, "test"));
        var marker = Assert.Single(store.Snapshot.RefreshTokens);
        Assert.Equal(Now.AddDays(14), marker.IdleExpiresAt);
        Assert.Equal(Now.AddDays(30), marker.AbsoluteExpiresAt);
        clock.Advance(TimeSpan.FromDays(14) + TimeSpan.FromTicks(ticks));
        var result = await service.RefreshAsync(login.RefreshToken);
        Assert.Equal(valid ? RefreshStatus.Success : RefreshStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Refresh_rotation_can_slide_idle_but_never_extend_thirty_day_absolute_deadline()
    {
        var (service, store, clock) = Fixture();
        var login = await service.LoginAsync(new LoginRequest("timeout_demo", "abcdefgh", AuthClientType.Mobile, false, "test"));
        var token = login.RefreshToken;
        var absolute = Now.AddDays(30);
        for (var index = 0; index < 4; index++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            var refreshed = await service.RefreshAsync(token);
            Assert.Equal(RefreshStatus.Success, refreshed.Status);
            token = refreshed.RefreshToken;
            Assert.Equal(absolute, store.Snapshot.RefreshTokens.Single(marker => marker.State == RefreshTokenState.Active).AbsoluteExpiresAt);
        }
        clock.Advance(absolute.AddSeconds(-1) - clock.GetUtcNow());
        var last = await service.RefreshAsync(token);
        Assert.Equal(RefreshStatus.Success, last.Status);
        var active = store.Snapshot.RefreshTokens.Single(marker => marker.State == RefreshTokenState.Active);
        Assert.Equal(absolute, active.IdleExpiresAt);
        Assert.Equal(absolute, active.AbsoluteExpiresAt);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(RefreshStatus.Invalid, (await service.RefreshAsync(last.RefreshToken)).Status);
    }

    private static (AuthApplicationService Service, InMemoryAuthSystemStore Store, ManualTimeProvider Clock) Fixture()
    {
        var state = new AuthSystemState();
        var hasher = new TestPasswordHashService();
        state.Users.Add(AuthTestData.User("TIMEOUT-USER", "timeout_demo", hasher.Hash("TIMEOUT-USER", "abcdefgh")));
        var store = new InMemoryAuthSystemStore(state);
        var clock = new ManualTimeProvider(Now);
        return (new AuthApplicationService(store, hasher, new AuthPolicyOptions(), clock), store, clock);
    }
}
