using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Assets;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace DuyTien.AssetManagement.ArchitectureTests;

// Real local Kestrel/compiled Razor + synthetic stores. This is HTTP evidence, not browser UAT.
public sealed class AssetCrudHttpRazorTests
{
    [Fact]
    public async Task Api_anonymous_cookie_and_viewer_writes_are_blocked_and_scoped_count_detail_search_are_safe()
    {
        await using var h = await Host.Start(); using var client = h.Client();
        using (var r = await client.GetAsync("/api/assets")) Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        await h.F.Grant(AuthRole.Viewer, "OU-A", "LOC-A", "CAT-MACHINE", true); var visible = (await h.F.Create()).Asset;
        var hidden = (await h.F.Create(h.F.Input() with { OrgUnitId = "OU-B", LocationId = "LOC-B", Name = "HIDDEN-FIXTURE" })).Asset;
        client.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("user", AuthClientType.Mobile));
        using (var r = await client.GetAsync("/api/assets")) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); var body = await r.Content.ReadAsStringAsync(); Assert.DoesNotContain("HIDDEN-FIXTURE", body, StringComparison.Ordinal); Assert.Equal(1, JsonDocument.Parse(body).RootElement.GetProperty("total").GetInt32()); }
        using (var r = await client.GetAsync("/api/assets/" + hidden.AssetId)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        using (var r = await client.GetAsync("/api/assets/form-choices")) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        using (var r = await client.GetAsync("/api/assets?q=HIDDEN")) Assert.Equal(0, JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("total").GetInt32());
        client.DefaultRequestHeaders.Add("Idempotency-Key", "viewer");
        using (var r = await client.PostAsJsonAsync("/api/assets", h.F.Request())) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        using (var r = await client.PatchAsJsonAsync("/api/assets/" + visible.AssetId, h.F.Request(version: 1))) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        using (var r = await SendDelete(client, visible.AssetId, 1)) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        using (var r = await client.PostAsJsonAsync("/api/assets/" + visible.AssetId + "/restore", new AssetLifecycleRequest(1, "fake"))) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        client.DefaultRequestHeaders.Authorization = null; client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web));
        using (var r = await client.GetAsync("/api/assets")) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }
    [Fact]
    public async Task Api_create_retry_update_stale_delete_restore_use_idempotency_and_keep_code()
    {
        await using var h = await Host.Start(); using var client = h.Client(); client.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("admin", AuthClientType.Mobile)); client.DefaultRequestHeaders.Add("Idempotency-Key", "create");
        using var created = await client.PostAsJsonAsync("/api/assets", h.F.Request()); Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.Equal("no-store", created.Headers.CacheControl!.ToString());
        var a = (await created.Content.ReadFromJsonAsync<AssetWriteResult>())!.Asset;
        using (var r = await client.PostAsJsonAsync("/api/assets", h.F.Request())) { Assert.Equal(HttpStatusCode.Created, r.StatusCode); Assert.True((await r.Content.ReadFromJsonAsync<AssetWriteResult>())!.Replayed); }
        using (var r = await client.PostAsJsonAsync("/api/assets", h.F.Request(h.F.Input() with { Name = "Changed" }))) Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        SetKey(client, "update"); using (var r = await client.PatchAsJsonAsync("/api/assets/" + a.AssetId, h.F.Request(h.F.Input() with { Name = "Đã sửa", LocationId = "LOC-A-CHILD" }, 1))) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(a.ObjectCode, (await r.Content.ReadFromJsonAsync<AssetWriteResult>())!.Asset.ObjectCode); }
        SetKey(client, "stale"); using (var r = await client.PatchAsJsonAsync("/api/assets/" + a.AssetId, h.F.Request(version: 1))) Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        SetKey(client, "delete"); using (var r = await SendDelete(client, a.AssetId, 2)) Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using (var r = await client.GetAsync("/api/assets")) Assert.Equal(0, (await r.Content.ReadFromJsonAsync<AssetList>())!.Total);
        using (var r = await client.GetAsync("/api/assets?includeDeleted=true")) Assert.Equal(1, (await r.Content.ReadFromJsonAsync<AssetList>())!.Total);
        SetKey(client, "restore"); using (var r = await client.PostAsJsonAsync("/api/assets/" + a.AssetId + "/restore", new AssetLifecycleRequest(3, "fake"))) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(a.ObjectCode, (await r.Content.ReadFromJsonAsync<AssetWriteResult>())!.Asset.ObjectCode); }
    }
    [Fact]
    public async Task Api_reports_field_errors_missing_key_overposted_identity_and_invalid_paging()
    {
        await using var h = await Host.Start(); using var client = h.Client(); client.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("admin", AuthClientType.Mobile));
        using (var r = await client.PostAsJsonAsync("/api/assets", h.F.Request())) Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        SetKey(client, "invalid"); using (var r = await client.PostAsJsonAsync("/api/assets", h.F.Request(h.F.Input() with { Name = "" }))) { Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode); Assert.Contains("Name", await r.Content.ReadAsStringAsync(), StringComparison.Ordinal); }
        using (var r = await client.GetAsync("/api/assets?pageSize=101")) Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        var json = JsonSerializer.Serialize(h.F.Request(), new JsonSerializerOptions(JsonSerializerDefaults.Web)).Replace("\"name\":", "\"objectCode\":\"OVERPOSTED\",\"name\":", StringComparison.Ordinal);
        using (var r = await client.PostAsync("/api/assets", new StringContent(json, System.Text.Encoding.UTF8, "application/json"))) Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(0, h.F.Memory.Writes);
    }
    [Fact]
    public async Task Api_optional_null_text_fields_remain_empty_without_inventing_company_data()
    {
        await using var h = await Host.Start(); using var client = h.Client(); client.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("admin", AuthClientType.Mobile)); SetKey(client, "optional-null");
        var input = h.F.Input() with { Serial = null!, Model = null!, Manufacturer = null!, CustodianRef = null!, Notes = null!, RecordedOn = null };
        using var response = await client.PostAsJsonAsync("/api/assets", h.F.Request(input)); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AssetWriteResult>())!.Asset;
        Assert.Equal("", result.Serial); Assert.Equal("", result.Model); Assert.Equal("", result.Manufacturer); Assert.Equal("", result.CustodianRef); Assert.Equal("", result.Notes); Assert.Null(result.RecordedOn);
    }
    [Fact]
    public async Task Razor_create_requires_antiforgery_shows_validation_preserves_inputs_and_creates_draft()
    {
        await using var h = await Host.Start(); using var client = h.Client(); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web));
        var html = await client.GetStringAsync("/Assets/Create"); Assert.Contains("DRAFT_DEMO", html, StringComparison.Ordinal); Assert.Contains("name=\"Form.Name\"", html, StringComparison.Ordinal); Assert.Contains("data-save-status", html, StringComparison.Ordinal);
        using (var r = await client.PostAsync("/Assets/Create", new FormUrlEncodedContent(Values(html, name: "Máy giả", csrf: false)))) Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        using (var r = await client.PostAsync("/Assets/Create", new FormUrlEncodedContent(Values(html, name: "")))) { Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); var body = await r.Content.ReadAsStringAsync(); Assert.Contains("Form.Name", body, StringComparison.Ordinal); Assert.Contains("CAT-CHILD", body, StringComparison.Ordinal); }
        using (var r = await client.PostAsync("/Assets/Create", new FormUrlEncodedContent(Values(html, name: "Máy browser giả")))) { Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); Assert.StartsWith("/Assets/Detail/AST-", r.Headers.Location!.OriginalString, StringComparison.Ordinal); }
        Assert.Single(h.F.Memory.State.Assets); Assert.Equal("DRAFT_DEMO", h.F.Memory.State.Assets.Single().DataState);
    }
    [Fact]
    public async Task Razor_detail_encoded_fields_update_conflict_reload_and_delete_restore_keep_identity()
    {
        await using var h = await Host.Start(); var a = (await h.F.Create(h.F.Input() with { Notes = "<script>alert(1)</script>\nDòng hai", Aliases = ["OLD-001"] })).Asset;
        using var client = h.Client(); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web)); client.DefaultRequestHeaders.Accept.Add(new("text/html")); var url = "/Assets/Detail/" + a.AssetId;
        var html = await client.GetStringAsync(url); Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal); Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal); Assert.Contains("OLD-001", html, StringComparison.Ordinal);
        var values = Values(html, "Đã sửa Razor"); values["ExpectedVersion"] = "1"; values["Form.LocationId"] = "LOC-A-CHILD";
        using (var r = await client.PostAsync(url + "?handler=Save", new FormUrlEncodedContent(values))) Assert.True(r.StatusCode == HttpStatusCode.Redirect, "HTTP " + (int)r.StatusCode + ": " + string.Join("; ", Regex.Matches(await r.Content.ReadAsStringAsync(), "<li>(.*?)</li>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))));
        values["RequestKey"] = "stale-second-editor"; using (var r = await client.PostAsync(url + "?handler=Save", new FormUrlEncodedContent(values))) { Assert.Equal(HttpStatusCode.Conflict, r.StatusCode); Assert.Contains("Tải", WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()), StringComparison.Ordinal); }
        Assert.Equal("Đã sửa Razor", h.F.Memory.State.Assets.Single().Name);
        html = await client.GetStringAsync(url); var lifecycle = Lifecycle(html, "2");
        using (var r = await client.PostAsync(url + "?handler=Delete", new FormUrlEncodedContent(lifecycle))) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        html = await client.GetStringAsync(url); Assert.Contains("Restore", html, StringComparison.Ordinal); lifecycle = Lifecycle(html, "3");
        using (var r = await client.PostAsync(url + "?handler=Restore", new FormUrlEncodedContent(lifecycle))) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(a.ObjectCode, h.F.Memory.State.Assets.Single().ObjectCode); Assert.Equal(a.AssetId, h.F.Memory.State.Assets.Single().AssetId);
    }
    [Fact]
    public async Task Razor_viewer_read_only_empty_search_and_upstream_fault_do_not_expose_private_data()
    {
        await using var h = await Host.Start(); await h.F.Grant(AuthRole.Viewer); var a = (await h.F.Create()).Asset;
        using var client = h.Client(); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("user", AuthClientType.Web)); client.DefaultRequestHeaders.Accept.Add(new("text/html"));
        using (var r = await client.GetAsync("/Assets/Create")) { Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); Assert.Equal("/Account/AccessDenied", r.Headers.Location!.OriginalString); }
        var html = await client.GetStringAsync("/Assets/Detail/" + a.AssetId); Assert.DoesNotContain("data-asset-form", html, StringComparison.Ordinal);
        using (var r = await client.PostAsync("/Assets/Detail/" + a.AssetId + "?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["ExpectedVersion"] = "1", ["RequestKey"] = "viewer-ui", ["Reason"] = "fake" }))) { Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); Assert.Equal("/Account/AccessDenied", r.Headers.Location!.OriginalString); }
        using (var r = await client.GetAsync("/Assets?Q=not-found")) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); var body = await r.Content.ReadAsStringAsync(); Assert.Contains("role=\"status\"", body, StringComparison.Ordinal); Assert.Contains("name=\"CategoryId\"", body, StringComparison.Ordinal); }
        h.F.Memory.FailRead = true; using (var r = await client.GetAsync("/Assets")) { Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode); Assert.DoesNotContain("PRIVATE-ASSET-CANARY", await r.Content.ReadAsStringAsync(), StringComparison.Ordinal); Assert.Null(r.Headers.Location); }
    }
    [Fact]
    public async Task Razor_failed_save_retains_attempted_fields_and_request_key_without_false_success()
    {
        await using var h = await Host.Start(); var a = (await h.F.Create()).Asset;
        using var client = h.Client(); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web));
        var url = "/Assets/Detail/" + a.AssetId; var html = await client.GetStringAsync(url); var values = Values(html, "UNSAVED-FIXTURE"); values["ExpectedVersion"] = "1";
        h.F.Memory.FailRead = true;
        using var r = await client.PostAsync(url + "?handler=Save", new FormUrlEncodedContent(values)); Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync(); Assert.Contains("UNSAVED-FIXTURE", body, StringComparison.Ordinal); Assert.Contains("CAT-CHILD", body, StringComparison.Ordinal); Assert.Equal(values["RequestKey"], Field(body, "RequestKey")); Assert.DoesNotContain("PRIVATE-ASSET-CANARY", body, StringComparison.Ordinal); Assert.Equal(1, h.F.Memory.Writes);
    }
    [Fact]
    public async Task Razor_lifecycle_missing_reason_shows_action_error_not_unrelated_edit_required_fields()
    {
        await using var h = await Host.Start(); var a = (await h.F.Create()).Asset; using var client = h.Client(); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web));
        var url = "/Assets/Detail/" + a.AssetId; var html = await client.GetStringAsync(url); var form = Lifecycle(html, "1"); form["Reason"] = "";
        using var response = await client.PostAsync(url + "?handler=Delete", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); Assert.Contains("Reason", body, StringComparison.Ordinal); Assert.DoesNotContain(Regex.Matches(body, "<li>(.*?)</li>").Select(m => m.Groups[1].Value), message => message.Contains("Nhập tên máy demo.", StringComparison.Ordinal)); Assert.False(h.F.Memory.State.Assets.Single().IsDeleted);
    }
    [Fact]
    public async Task Revoked_role_invalidates_token_on_new_asset_endpoint_without_policy_changes()
    {
        await using var h = await Host.Start(); await h.F.Grant(AuthRole.AssetEditor); using var client = h.Client(); client.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("user", AuthClientType.Mobile));
        using (var r = await client.GetAsync("/api/assets")) Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var auth = h.App.Services.GetRequiredService<AuthApplicationService>(); await auth.ReplaceRoleAssignmentsAsync("ADMIN", "USER", []);
        using (var r = await client.GetAsync("/api/assets")) Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
    private static Dictionary<string, string> Values(string html, string name, bool csrf = true)
    {
        var d = new Dictionary<string, string> { ["RequestKey"] = Field(html, "RequestKey"), ["TreeVersionId"] = Field(html, "TreeVersionId"), ["Form.Name"] = name, ["Form.CategoryId"] = "CAT-CHILD", ["Form.OrgUnitId"] = "OU-A", ["Form.LocationId"] = "LOC-A", ["Form.Condition"] = "UNKNOWN", ["Form.CustodianRef"] = "", ["Form.Serial"] = "", ["Form.Model"] = "", ["Form.Manufacturer"] = "", ["Form.Notes"] = "", ["Form.AliasesText"] = "", ["Form.RecordedOn"] = "" };
        if (csrf) d["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"); return d;
    }
    private static Dictionary<string, string> Lifecycle(string html, string version) => new() { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["RequestKey"] = Field(html, "RequestKey"), ["ExpectedVersion"] = version, ["Reason"] = "Fixture giả" };
    private static string Field(string html, string name) { var match = Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]+)\""); Assert.True(match.Success, "Missing form field: " + name); return WebUtility.HtmlDecode(match.Groups[1].Value); }
    private static void SetKey(HttpClient client, string key) { client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", key); }
    private static Task<HttpResponseMessage> SendDelete(HttpClient client, string id, long version) => client.SendAsync(new(HttpMethod.Delete, "/api/assets/" + id) { Content = JsonContent.Create(new AssetLifecycleRequest(version, "fake")) });
    private sealed class Host(WebApplication app, AssetFixture f, string address) : IAsyncDisposable
    {
        public WebApplication App { get; } = app; public AssetFixture F { get; } = f;
        public HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(address) };
        public async Task<string> Login(string name, AuthClientType type) { var result = await App.Services.GetRequiredService<AuthApplicationService>().LoginAsync(new(name, "fake-password", type, false, "asset-http-test")); Assert.Equal(LoginStatus.Success, result.Status); return result.AccessToken!; }
        public static async Task<Host> Start()
        {
            var f = await AssetFixture.Start(); var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ApplicationName = typeof(AssetWeb).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory }); builder.Logging.ClearProviders(); builder.WebHost.UseKestrel();
            builder.Services.AddRazorPages(); builder.Services.AddSingleton(f.Service); builder.Services.AddSingleton<IAuthSystemStore>(f.Auth); builder.Services.AddSingleton<IPasswordHashService>(new TestPasswordHashService()); builder.Services.AddSingleton(new AuthPolicyOptions()); builder.Services.AddSingleton<TimeProvider>(f.Clock); builder.Services.AddSingleton<AuthApplicationService>();
            builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); builder.Services.AddAuthorization();
            var app = builder.Build(); app.Urls.Add("http://127.0.0.1:0"); app.UseTask005SessionAuthentication(); app.UseAuthorization(); app.MapTask007AssetApi(); app.MapRazorPages(); await app.StartAsync(); return new(app, f, app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        }
        public async ValueTask DisposeAsync() { await App.StopAsync(); await App.DisposeAsync(); }
    }
}
