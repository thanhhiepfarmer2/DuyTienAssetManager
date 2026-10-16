using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server;
using DuyTien.AssetManagement.Server.Assets;
using DuyTien.AssetManagement.Server.Categories;
using DuyTien.AssetManagement.Server.Inventory;
using DuyTien.AssetManagement.Server.Media;
using DuyTien.AssetManagement.Server.Reports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace DuyTien.AssetManagement.ArchitectureTests;

// Real Kestrel/compiled Razor across all web modules, synthetic stores + Google HTTP handler.
// This is HTTP rendered evidence; no browser DOM/navigation/layout or Google live claim.
public sealed class WebIntegrationTests
{
    [Fact]
    public async Task Anonymous_home_help_login_and_business_navigation_boundaries_are_consistent()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = h.Client();
        var home = await c.GetStringAsync("/"); Assert.DoesNotContain("href=\"/Items\"", home); Assert.DoesNotContain("href=\"/Stock\"", home); Assert.Contains("DRAFT", home); Assert.DoesNotContain("bổ sung theo từng Task sau TASK-003", home);
        var help = WebUtility.HtmlDecode(await c.GetStringAsync("/Help")); Assert.Contains("Hướng dẫn sử dụng", help); Assert.Contains("Ngoại tuyến — chỉ đọc", help); Assert.DoesNotContain("LocalOffline trên máy Windows", help); Assert.DoesNotContain("LocalSetup", help);
        using (var r = await c.GetAsync("/Assets")) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var login = await c.GetStringAsync("/Account/Login");
        using var invalid = await c.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Field(login, "__RequestVerificationToken"), ["Username"] = "", ["Password"] = "" }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode); Assert.DoesNotContain("Set-Cookie: dt_session", invalid.ToString());
    }
    [Fact]
    public async Task Public_status_never_triggers_Google_readiness_or_claims_live_verified()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = h.Client(); var before = h.R.F.Handler.Uris.Count;
        using var response = await c.GetAsync("/api/system/status"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal("WEB_DEMO_LOCAL", json.RootElement.GetProperty("state").GetString());
        Assert.False(json.RootElement.GetProperty("sheets").GetProperty("liveConnectionVerified").GetBoolean()); Assert.False(json.RootElement.GetProperty("drive").GetProperty("liveConnectionVerified").GetBoolean());
        Assert.Equal(before, h.R.F.Handler.Uris.Count);
    }
    [Theory]
    [InlineData("/")]
    [InlineData("/Categories")]
    [InlineData("/Assets")]
    [InlineData("/Documents")]
    [InlineData("/Items")]
    [InlineData("/Stock")]
    [InlineData("/Reports")]
    [InlineData("/Admin/Users")]
    [InlineData("/Admin/Authorization")]
    [InlineData("/Admin/SecurityEvents")]
    [InlineData("/Categories/Import")]
    [InlineData("/Items/Import")]
    public async Task Admin_can_reach_all_existing_web_modules_with_same_session_and_full_registry(string path)
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin"); using var response = await c.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var html = await response.Content.ReadAsStringAsync(); Assert.Contains("main-navigation", html); Assert.Contains("href=\"/Help\"", html);
        Assert.DoesNotContain("PRIVATE-", html); Assert.DoesNotContain("fixture-token", html);
    }
    [Fact]
    public async Task Cross_category_asset_item_document_photo_report_flow_survives_registry_relocate()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin");
        var category = await c.GetStringAsync("/Categories/Detail/" + h.R.F.Child); Assert.Contains("CategoryId=" + h.R.F.Child, category); Assert.Contains("/Assets?", category); Assert.Contains("/Items?", category);
        var asset = await c.GetStringAsync("/Assets/Detail/" + h.R.Asset.AssetId); Assert.Contains("/Assets/Photos/" + h.R.Asset.AssetId, asset);
        var photos = await c.GetStringAsync("/Assets/Photos/" + h.R.Asset.AssetId); Assert.Contains("AVAILABLE", photos);
        var req = h.R.Archive(); var p = await h.R.Tree.PreviewAsync("ADMIN", req); await h.R.Commit(req, p);
        var moved = await c.GetStringAsync("/Assets/Detail/" + h.R.Asset.AssetId); Assert.Contains("Đích giả", WebUtility.HtmlDecode(moved)); Assert.Contains(h.R.Asset.ObjectCode, moved);
        var item = (await h.R.Inventory.ReadAsync()).Items.Single(); Assert.Contains(h.R.F.Other, await c.GetStringAsync("/Items/Detail/" + item.ItemId));
        Assert.Contains("Đích giả", WebUtility.HtmlDecode(await c.GetStringAsync("/Documents/Detail/" + h.R.DocumentId)));
        var file = (await h.R.Media.ReadAsync()).Files.First(f => f.Kind == "PHOTO"); using var preview = await c.GetAsync("/Files/Content/" + file.FileId + "?preview=true"); Assert.Equal(HttpStatusCode.OK, preview.StatusCode); Assert.Equal(MediaBytes.Png().Bytes, await preview.Content.ReadAsByteArrayAsync());
        Assert.Contains("DRAFT", await c.GetStringAsync("/Reports?Code=R02&CategoryId=" + h.R.F.Other));
    }
    [Fact]
    public async Task Snapshot_get_loads_current_nullable_values_and_post_failures_keep_submitted_form_key_and_version()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin"); var item = (await h.R.Inventory.ReadAsync()).Items.Single();
        await h.R.InventoryService.WriteStockAsync("ADMIN", new(item.ItemId, "LOC-A", item.PrimaryUnit, 4.5m, 2.5m, h.R.F.AuthFixture.Clock.GetUtcNow(), "GIẢ đang lưu", 1, "GIẢ"), "known");
        var html = await c.GetStringAsync("/Stock?ItemId=" + item.ItemId + "&LocationId=LOC-A&Unit=" + Uri.EscapeDataString(item.PrimaryUnit) + "&ExpectedVersion=999");
        Assert.Equal("4.5", Field(html, "Quantity")); Assert.Equal("2.5", Field(html, "Minimum")); Assert.Equal("2", Field(html, "ExpectedVersion")); Assert.Equal("GIẢ đang lưu", Field(html, "Source"));
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["RequestKey"] = Field(html, "RequestKey"), ["ItemId"] = item.ItemId, ["LocationId"] = "LOC-A", ["Unit"] = item.PrimaryUnit, ["Quantity"] = "6.25", ["Minimum"] = "2.5", ["AsOf"] = h.R.F.AuthFixture.Clock.GetUtcNow().ToString("O"), ["Source"] = "GIẢ yêu cầu mới", ["Reason"] = "GIẢ", ["ExpectedVersion"] = "2" };
        h.R.F.Handler.FailBeforeCommitOnce = true;
        using (var failed = await c.PostAsync("/Stock", new FormUrlEncodedContent(fields)))
        { Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode); var body = await failed.Content.ReadAsStringAsync(); Assert.Equal("6.25", Field(body, "Quantity")); Assert.Equal(fields["RequestKey"], Field(body, "RequestKey")); Assert.Equal("2", Field(body, "ExpectedVersion")); fields["__RequestVerificationToken"] = Field(body, "__RequestVerificationToken"); }
        using (var saved = await c.PostAsync("/Stock", new FormUrlEncodedContent(fields))) Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(6.25m, (await h.R.Inventory.ReadAsync()).Balances.Single().Quantity);
        using (var stale = await c.PostAsync("/Stock", new FormUrlEncodedContent(withKey("different")))) { Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal("2", Field(await stale.Content.ReadAsStringAsync(), "ExpectedVersion")); }
        Dictionary<string, string> withKey(string key) { var copy = new Dictionary<string, string>(fields); copy["RequestKey"] = key; return copy; }
    }
    [Theory]
    [InlineData("vi-VN", "6.25", true)]
    [InlineData("en-US", "6.25", true)]
    [InlineData("vi-VN", "6,25", false)]
    [InlineData("en-US", "-1", false)]
    public async Task Numeric_wire_values_do_not_depend_on_culture_or_silently_become_thousands(string culture, string quantity, bool valid)
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin"); c.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        var item = (await h.R.Inventory.ReadAsync()).Items.Single(); var html = await c.GetStringAsync("/Stock");
        var data = new Dictionary<string, string> { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["RequestKey"] = Field(html, "RequestKey"), ["ItemId"] = item.ItemId, ["LocationId"] = "LOC-A", ["Unit"] = item.PrimaryUnit, ["Quantity"] = quantity, ["Minimum"] = "", ["AsOf"] = h.R.F.AuthFixture.Clock.GetUtcNow().ToString("O"), ["Source"] = "GIẢ", ["Reason"] = "GIẢ", ["ExpectedVersion"] = "1" };
        using var response = await c.PostAsync("/Stock", new FormUrlEncodedContent(data)); Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var saved = (await h.R.Inventory.ReadAsync()).Balances.Single(); Assert.Equal(valid ? 6.25m : null, saved.Quantity);
    }
    [Fact]
    public async Task Viewer_hidden_detail_stock_and_management_fail_without_leaking_registry()
    {
        await using var h = await WebIntegrationHost.Start(); await h.R.Grant(AuthRole.Viewer, h.R.F.Other, true); using var c = await h.Cookie("user");
        var home = await c.GetStringAsync("/"); Assert.DoesNotContain("href=\"/Admin/Users\"", home); Assert.DoesNotContain("href=\"/Items/Import\"", home);
        using (var r = await c.GetAsync("/Admin/Users")) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        using (var r = await c.GetAsync("/Assets/Detail/" + h.R.Asset.AssetId)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        var item = (await h.R.Inventory.ReadAsync()).Items.Single(); using (var r = await c.GetAsync("/Stock?ItemId=" + item.ItemId + "&LocationId=LOC-A&Unit=" + Uri.EscapeDataString(item.PrimaryUnit) + "&ExpectedVersion=1")) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("Hồ sơ GIẢ TASK014", WebUtility.HtmlDecode(await c.GetStringAsync("/Documents")));
    }
    [Fact]
    public async Task Razor_preview_shows_complete_registry_and_antiforgery_blocks_commit_without_writes()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin"); var html = await c.GetStringAsync("/Categories/Manage/" + h.R.F.Root);
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["RequestKey"] = Field(html, "RequestKey"), ["Form.Kind"] = "ARCHIVE_CATEGORY", ["Form.Reason"] = "GIẢ", ["Form.CategoryId"] = h.R.F.Root, ["Form.DestinationCategoryId"] = h.R.F.Other, ["Form.DeletePlan"] = "RELOCATE" };
        using (var response = await c.PostAsync("/Categories/Manage/" + h.R.F.Root + "?handler=Preview", new FormUrlEncodedContent(fields)))
        { Assert.Equal(HttpStatusCode.OK, response.StatusCode); html = await response.Content.ReadAsStringAsync(); Assert.Contains("Đối tượng liên quan trong phạm vi", WebUtility.HtmlDecode(html)); Assert.Contains("ITEM", html); Assert.Contains("PHOTO", html); Assert.Contains("PDF", html); }
        fields["PreviewHash"] = Field(html, "PreviewHash"); fields["ExpectedTreeRevision"] = Field(html, "ExpectedTreeRevision"); fields["ExpectedAssetRevision"] = Field(html, "ExpectedAssetRevision"); fields["ConfirmImpact"] = "true"; fields.Remove("__RequestVerificationToken"); var posts = h.R.F.Handler.Posts;
        using (var denied = await c.PostAsync("/Categories/Manage/" + h.R.F.Root + "?handler=Commit", new FormUrlEncodedContent(fields))) Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode); Assert.Equal(posts, h.R.F.Handler.Posts);
        fields["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"); using (var committed = await c.PostAsync("/Categories/Manage/" + h.R.F.Root + "?handler=Commit", new FormUrlEncodedContent(fields))) Assert.Equal(HttpStatusCode.OK, committed.StatusCode); Assert.Equal(posts + 1, h.R.F.Handler.Posts);
    }
    [Fact]
    public async Task Category_import_cannot_remove_document_owner_and_partial_registry_blocks_import_reads()
    {
        var r = await TreeRegistryRig.Start(); await r.MediaService.CreateDocumentAsync("ADMIN", new(new("GIẢ trên đích", "FAKE.OTHER", r.F.Other), 0), "other-doc");
        var document = CategoryFixture.Doc(CategoryFixture.Rows, hash: "different-source-GIA"); var p = await r.Catalog.PreviewAsync("ADMIN", document); var posts = r.F.Handler.Posts;
        await Assert.ThrowsAsync<CategoryConflictException>(() => r.Catalog.ImportAsync("ADMIN", document, p.Revision, p.PreviewHash, "unsafe-import", true)); Assert.Equal(posts, r.F.Handler.Posts);
        r.F.Handler.Tables.Remove("Files"); await Assert.ThrowsAsync<DuyTien.AssetManagement.Application.ExternalStorage.ExternalStorageException>(() => r.Catalog.PreviewAsync("ADMIN", document)); Assert.Equal(posts, r.F.Handler.Posts);
    }
    private static string Field(string html, string name)
    { var match = Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\""); Assert.True(match.Success, "Missing field " + name); return WebUtility.HtmlDecode(match.Groups[1].Value); }
}
internal sealed class WebIntegrationHost(WebApplication app, TreeRegistryRig rig, string address) : IAsyncDisposable
{
    public TreeRegistryRig R { get; } = rig;
    public HttpClient Client() { var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(address) }; client.DefaultRequestHeaders.Accept.Add(new("text/html")); return client; }
    public async Task<HttpClient> Cookie(string username)
    { var client = Client(); var result = await app.Services.GetRequiredService<AuthApplicationService>().LoginAsync(new(username, "fake-password", AuthClientType.Web, false, "web-fixture")); Assert.Equal(LoginStatus.Success, result.Status); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + result.AccessToken); return client; }
    public static async Task<WebIntegrationHost> Start()
    {
        var r = await TreeRegistryRig.Start(); var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ApplicationName = typeof(AssetWeb).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory }); builder.Logging.ClearProviders(); builder.WebHost.UseKestrel(); builder.Services.AddRazorPages();
        builder.Services.AddSingleton(r.F.Options); builder.Services.AddSingleton(r.Catalog); builder.Services.AddSingleton(r.Tree); builder.Services.AddSingleton(r.AssetService); builder.Services.AddSingleton(r.InventoryService); builder.Services.AddSingleton(r.MediaService);
        builder.Services.AddSingleton(new CategoryPreviewCache(r.F.AuthFixture.Clock)); builder.Services.AddSingleton(new MediaUploadOptions()); builder.Services.AddSingleton(new ReportService(r.Assets, r.Categories, r.Inventory, r.F.AuthFixture.Auth, r.F.Writer, r.F.AuthFixture.Clock));
        builder.Services.AddSingleton<IAuthSystemStore>(r.F.AuthFixture.Auth); builder.Services.AddSingleton<IPasswordHashService>(new TestPasswordHashService()); builder.Services.AddSingleton(new AuthPolicyOptions()); builder.Services.AddSingleton<TimeProvider>(r.F.AuthFixture.Clock); builder.Services.AddSingleton<AuthApplicationService>();
        builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); builder.Services.AddAuthorization();
        var app = builder.Build(); app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions().SetDefaultCulture("vi-VN").AddSupportedCultures("vi-VN", "en-US").AddSupportedUICultures("vi-VN", "en-US")); app.Urls.Add("http://127.0.0.1:0"); app.UseTask005SessionAuthentication(); app.UseAuthorization(); app.MapLocalSystemStatus(); app.MapTask005AuthApi(); app.MapTask007AssetApi(); app.MapTask006CategoryApi(); app.MapTask008TreeApi(); app.MapTask009MediaApi(); app.MapTask010InventoryApi(); app.MapTask011ReportApi(); app.MapRazorPages(); await app.StartAsync(); return new(app, r, app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
    }
    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
}
