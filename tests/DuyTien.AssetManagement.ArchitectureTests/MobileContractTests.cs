using System.Net;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Mobile.Client;
using DuyTien.AssetManagement.Server.Assets;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Media;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DuyTien.AssetManagement.ArchitectureTests;

// Client's real serialization -> local Kestrel's existing APIs -> synthetic business stores/content.
// Forwarder maps a fake HTTPS origin to local loopback only inside tests. No TLS/Google/device proof.
public sealed class MobileContractTests
{
    [Fact]
    public async Task Login_list_filters_paging_detail_demo_create_update_conflict_and_explicit_rebase_match_backend()
    {
        await using var h = await MobileHost.Start(); var m = await h.Login("admin");
        var me = await m.Api.MeAsync(); Assert.Contains("Admin", me.Roles);
        var choices = await m.Api.ChoicesAsync(false); Assert.Equal("DRAFT_DEMO", choices.DataState);
        var draft = h.Draft(m); var created = await draft.SaveAsync(); Assert.Equal("DRAFT_DEMO", created.Asset.DataState);
        var rows = await m.Api.ListAsync(new(Q: "Máy", PageSize: 1)); Assert.Equal(1, rows.Total); Assert.Single(rows.Items);
        Assert.Empty((await m.Api.ListAsync(new(Page: 2, PageSize: 1))).Items);
        Assert.Equal(0, (await m.Api.ListAsync(new(OrgUnitId: "OU-B"))).Total);
        var detail = await m.Api.DetailAsync(created.Asset.AssetId); Assert.Equal("DRAFT", detail.CategoryState);
        var first = new MobileAssetDraft(m.Api, created.Asset.AssetId, h.Input() with { Notes = "Nội dung mới giả" }, 1, choices.TreeVersionId!);
        Assert.Equal(2, (await first.SaveAsync()).Asset.Version);
        var stale = new MobileAssetDraft(m.Api, created.Asset.AssetId, h.Input() with { Notes = "Form cũ phải giữ" }, 1, choices.TreeVersionId!);
        Assert.Equal(409, (await Assert.ThrowsAsync<MobileApiException>(() => stale.SaveAsync())).Status);
        Assert.Equal("Form cũ phải giữ", stale.Input.Notes); Assert.Equal(1, stale.Version);
        await stale.RebaseAsync(); Assert.Equal(2, stale.Version); Assert.Equal("Form cũ phải giữ", stale.Input.Notes);
        var updated = (await stale.SaveAsync()).Asset; Assert.Equal(3, updated.Version); Assert.Equal(created.Asset.ObjectCode, updated.ObjectCode);
    }
    [Fact]
    public async Task Lost_create_response_retries_same_payload_key_without_duplicate_and_prevents_changed_form_retry()
    {
        await using var h = await MobileHost.Start(); var m = await h.Login("admin"); var draft = h.Draft(m);
        m.Transport.DropResponseAt = "/api/assets";
        Assert.Equal(0, (await Assert.ThrowsAsync<MobileApiException>(() => draft.SaveAsync())).Status);
        Assert.True(draft.HasPending); Assert.Single(h.F.Owner.Store.State.Assets.Assets);
        var original = draft.Input; draft.Input = original with { Name = "Changed while uncertain" };
        Assert.Equal(409, (await Assert.ThrowsAsync<MobileApiException>(() => draft.SaveAsync())).Status);
        draft.Input = original; var retry = await draft.SaveAsync(); Assert.True(retry.Replayed); Assert.Single(h.F.Owner.Store.State.Assets.Assets);
        Assert.False(draft.HasPending); Assert.Equal(retry.Asset.AssetId, (await draft.SaveAsync()).Asset.AssetId);
    }
    [Fact]
    public async Task Viewer_has_only_scoped_list_total_search_detail_and_photo_access_and_cannot_create_or_upload()
    {
        await using var h = await MobileHost.Start(); var visible = await h.F.Asset();
        var hidden = (await h.F.Owner.AssetService.CreateAsync("ADMIN", h.F.Owner.AssetRequest() with { Asset = h.Input() with { OrgUnitId = "OU-B", LocationId = "LOC-B", Name = "HIDDEN GIẢ" } }, "hidden")).Asset;
        var hiddenPhoto = await h.F.Service.UploadPhotoAsync("ADMIN", hidden.AssetId, MediaBytes.Png(), 1, "", false, "hidden-photo");
        await h.F.Owner.Base.Grant(AuthRole.Viewer, "OU-A", "LOC-A", "CAT-MACHINE", true); var m = await h.Login("user");
        var list = await m.Api.ListAsync(new(PageSize: 1)); Assert.Equal(1, list.Total); Assert.Equal(visible.AssetId, list.Items.Single().Asset.AssetId);
        Assert.Empty((await m.Api.ListAsync(new(Q: "HIDDEN"))).Items);
        Assert.Equal(404, (await Assert.ThrowsAsync<MobileApiException>(() => m.Api.DetailAsync(hidden.AssetId))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<MobileApiException>(() => m.Session.PhotoAsync(hiddenPhoto.File.FileId))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<MobileApiException>(() => m.Api.ChoicesAsync(false))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<MobileApiException>(() => h.Draft(m).SaveAsync())).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<MobileApiException>(() => m.Api.UploadAsync(visible.AssetId, new(MediaBytes.Png().Bytes), 1, "", false, "denied"))).Status);
    }
    [Fact]
    public async Task Forced_password_change_and_eight_character_password_use_real_backend_contract()
    {
        await using var h = await MobileHost.Start(); await h.F.Owner.Base.Grant(AuthRole.AssetEditor);
        await h.F.Owner.Base.Auth.MutateAsync(state => { var index = state.Users.FindIndex(x => x.UserId == "USER"); state.Users[index] = state.Users[index] with { MustChangePassword = true }; return true; });
        var m = await h.Login("user"); Assert.True(m.Session.MustChangePassword); Assert.True((await m.Api.MeAsync()).MustChangePassword);
        Assert.Equal(403, (await Assert.ThrowsAsync<MobileApiException>(() => m.Api.ListAsync(new()))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<MobileApiException>(() => m.Session.ChangePasswordAsync("wrong-fake", "12345678"))).Status); Assert.True(m.Session.IsSignedIn);
        await m.Session.ChangePasswordAsync("fake-password", "12345678"); Assert.False(m.Session.IsSignedIn); Assert.Null(m.Secrets.Value);
        await m.Session.LoginAsync("user", "12345678"); Assert.False((await m.Api.MeAsync()).MustChangePassword);
    }
    [Fact]
    public async Task Real_auth_rotation_on_fake_time_and_logout_clear_secure_pair_and_server_session()
    {
        await using var h = await MobileHost.Start(); var m = await h.Login("admin");
        h.F.Owner.Base.Clock.Advance(TimeSpan.FromHours(1)); await m.Api.MeAsync();
        Assert.Equal(1, m.Transport.Refreshes); Assert.False(System.Text.Json.JsonSerializer.Deserialize<SessionEnvelope>(m.Secrets.Value!, MobileJson.Options)!.RefreshInFlight);
        await m.Session.LogoutAsync(); Assert.False(m.Session.IsSignedIn); Assert.Null(m.Secrets.Value);
        var state = await h.F.Owner.Base.Auth.ReadAsync(s => s.Clone()); Assert.All(state.Sessions, x => Assert.NotNull(x.RevokedAt));
    }
    [Fact]
    public async Task Real_backend_user_lock_revokes_client_and_deletes_pair_instead_of_refresh_retry_loop()
    {
        await using var h = await MobileHost.Start(); await h.F.Owner.Base.Grant(AuthRole.AssetEditor); var m = await h.Login("user");
        await h.App.Services.GetRequiredService<AuthApplicationService>().SetUserLockedAsync("ADMIN", "USER", true);
        Assert.Equal(401, (await Assert.ThrowsAsync<MobileApiException>(() => m.Api.ListAsync(new()))).Status);
        Assert.False(m.Session.IsSignedIn); Assert.Null(m.Secrets.Value); Assert.Equal(1, m.Transport.Refreshes);
    }
    [Fact]
    public async Task Pending_photo_failure_reconciles_one_file_and_preview_uses_bearer_gateway()
    {
        await using var h = await MobileHost.Start(); var a = await h.F.Asset(); var m = await h.Login("admin");
        var draft = new MobilePhotoDraft(m.Api, a.AssetId, a.Version) { Description = "Ảnh giả cần giữ", IsPrimary = true }; draft.Select(new(MediaBytes.Png().Bytes));
        h.F.Content.FailAfterCreate = true;
        var e = await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync()); Assert.Equal(503, e.Status);
        Assert.True(draft.HasPending); Assert.NotNull(draft.FileId); Assert.Equal("RETRY", draft.State); Assert.Equal(1, h.F.Content.Creates);
        var result = await draft.ReconcileAsync(); Assert.Equal("AVAILABLE", draft.State); Assert.False(draft.HasPending); Assert.Equal("Ảnh giả cần giữ", result.File.Description);
        Assert.Equal(MediaBytes.Png().Bytes, await m.Session.PhotoAsync(result.File.FileId)); Assert.Equal(1, h.F.Content.Creates);
        Assert.Single(await m.Api.PhotosAsync(a.AssetId));
    }
    [Fact]
    public async Task Lost_photo_response_retries_original_multipart_and_does_not_allocate_another_file()
    {
        await using var h = await MobileHost.Start(); var a = await h.F.Asset(); var m = await h.Login("admin");
        var draft = new MobilePhotoDraft(m.Api, a.AssetId, 1); var bytes = MediaBytes.Png().Bytes; draft.Select(new(bytes)); m.Transport.DropResponseAt = "/api/assets/" + a.AssetId + "/photos";
        Assert.Equal(0, (await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync())).Status);
        Assert.True(draft.HasPending); Assert.Equal(1, h.F.Content.Creates);
        Assert.Throws<MobileApiException>(() => draft.Select(new(bytes)));
        draft.Description = "Changed"; Assert.Equal(409, (await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync())).Status); draft.Description = "";
        var result = await draft.UploadAsync(); Assert.True(result.Replayed); Assert.Equal(1, h.F.Content.Creates); Assert.Single(h.F.Store.State.Files); Assert.Equal("AVAILABLE", draft.State);
        await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync());
    }
    [Fact]
    public async Task Service_failure_retains_draft_and_422_can_be_edited_without_inventing_rules()
    {
        await using var h = await MobileHost.Start(); var m = await h.Login("admin"); var draft = h.Draft(m);
        h.F.Owner.Store.FailRead = true;
        Assert.Equal(503, (await Assert.ThrowsAsync<MobileApiException>(() => draft.SaveAsync())).Status); Assert.True(draft.HasPending); Assert.Equal(h.Input().Name, draft.Input.Name); Assert.Equal(h.Input().Serial, draft.Input.Serial);
        h.F.Owner.Store.FailRead = false; Assert.NotNull((await draft.SaveAsync()).Asset);
        var bad = new MobileAssetDraft(m.Api, null, h.Input() with { LocationId = "LOC-B" }, 0, "TREE-FAKE");
        Assert.Equal(422, (await Assert.ThrowsAsync<MobileApiException>(() => bad.SaveAsync())).Status); Assert.False(bad.HasPending); Assert.Equal("LOC-B", bad.Input.LocationId);
        bad.Input = bad.Input with { LocationId = "LOC-A" }; Assert.NotNull((await bad.SaveAsync()).Asset);
    }
    [Fact]
    public async Task Stale_photo_owner_version_is_conflict_and_never_uploads_or_assumes_zero_version()
    {
        await using var h = await MobileHost.Start(); var a = await h.F.Asset(); var m = await h.Login("admin");
        var draft = new MobilePhotoDraft(m.Api, a.AssetId, 0); draft.Select(new(MediaBytes.Png().Bytes));
        Assert.Equal(409, (await Assert.ThrowsAsync<MobileApiException>(() => draft.UploadAsync())).Status); Assert.Equal("CONFLICT", draft.State); Assert.Equal(0, h.F.Content.Creates);
        await draft.RebaseAsync(); Assert.Equal(1, draft.ExpectedVersion); Assert.Equal("AVAILABLE", (await draft.UploadAsync()).File.State); Assert.Equal(1, h.F.Content.Creates);
    }
}
internal sealed class MobileHost(WebApplication app, ManagedMediaFixture fixture, Uri address) : IAsyncDisposable
{
    private readonly List<HttpClient> clients = [];
    public WebApplication App { get; } = app;
    public ManagedMediaFixture F { get; } = fixture;
    public AssetInput Input() => F.Owner.Base.Input();
    public MobileAssetDraft Draft(MobileConnection connection) => new(connection.Api, null, Input(), 0, "TREE-FAKE");
    public async Task<MobileConnection> Login(string username)
    {
        var secrets = new MobileMemorySecrets(); var transport = new MobileLoopbackTransport(address);
        var client = new HttpClient(transport); clients.Add(client); var session = new MobileSession(client, new("https://fixture.invalid/"), secrets, F.Owner.Base.Clock);
        await session.LoginAsync(username, "fake-password"); return new(session, new(session), secrets, transport);
    }
    public static async Task<MobileHost> Start()
    {
        var f = await ManagedMediaFixture.Start(); var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        b.WebHost.UseKestrel(); b.Logging.ClearProviders(); b.Services.AddSingleton(f.Service); b.Services.AddSingleton(f.Owner.AssetService); b.Services.AddSingleton(new MediaUploadOptions());
        b.Services.AddSingleton<IAuthSystemStore>(f.Owner.Base.Auth); b.Services.AddSingleton<IPasswordHashService>(new TestPasswordHashService()); b.Services.AddSingleton(new AuthPolicyOptions());
        b.Services.AddSingleton<TimeProvider>(f.Owner.Base.Clock); b.Services.AddSingleton<AuthApplicationService>();
        b.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); b.Services.AddAuthorization();
        var app = b.Build(); app.Urls.Add("http://127.0.0.1:0"); app.UseTask005SessionAuthentication(); app.UseAuthorization(); app.MapTask005AuthApi(); app.MapTask007AssetApi(); app.MapTask009MediaApi();
        await app.StartAsync(); return new(app, f, new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()));
    }
    public async ValueTask DisposeAsync() { foreach (var c in clients) c.Dispose(); await App.StopAsync(); await App.DisposeAsync(); }
}
internal sealed record MobileConnection(MobileSession Session, MobileAssets Api, MobileMemorySecrets Secrets, MobileLoopbackTransport Transport);
internal sealed class MobileLoopbackTransport(Uri loopback) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
{
    public string? DropResponseAt { get; set; }
    public int Refreshes { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/api/auth/refresh") Refreshes++;
        request.RequestUri = new Uri(loopback, request.RequestUri.PathAndQuery);
        var response = await base.SendAsync(request, ct);
        if (DropResponseAt == path) { DropResponseAt = null; response.Dispose(); throw new HttpRequestException("Synthetic response lost after commit"); }
        return response;
    }
}
