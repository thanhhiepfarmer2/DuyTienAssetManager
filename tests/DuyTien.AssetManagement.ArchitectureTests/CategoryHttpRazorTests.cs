using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class CategoryHttpRazorTests
{
    [Fact]
    public async Task Upstream_fault_returns_safe_503_and_Razor_cookie_recovers_without_relogin()
    {
        await using var h = await Host.Start(); using var client = h.Client();
        client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web));
        h.F.Store.FailRead = true;
        using (var failure = await client.GetAsync("/Categories"))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
            Assert.Null(failure.Headers.Location);
            Assert.DoesNotContain("PRIVATE-CATEGORY-FAULT-CANARY", await failure.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        h.F.Store.FailRead = false;
        using var recovered = await client.GetAsync("/Categories"); Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }
    [Fact]
    public async Task Api_requires_bearer_and_filters_viewer_tree_search_detail_and_mutations()
    {
        await using var h = await Host.Start(); using var client = h.Client();
        using (var anonymous = await client.GetAsync("/api/categories/tree")) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var root = h.F.Store.State.ActiveNodes.Single(n => n.SegmentCode == "ROT").CategoryId;
        var child = h.F.Store.State.ActiveNodes.Single(n => n.SegmentCode == "CHD").CategoryId;
        await h.F.Grant("USER", AuthRole.Viewer, child, true); var token = await h.Login("user", AuthClientType.Mobile);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using (var list = await client.GetAsync("/api/categories/tree")) { Assert.Equal(HttpStatusCode.OK, list.StatusCode); var json = await list.Content.ReadAsStringAsync(); Assert.DoesNotContain("Nhánh bí mật", json, StringComparison.Ordinal); using var d = JsonDocument.Parse(json); Assert.Equal(2, d.RootElement.GetProperty("total").GetInt32()); }
        using (var search = await client.GetAsync("/api/categories?q=ROT")) { using var d = JsonDocument.Parse(await search.Content.ReadAsStringAsync()); Assert.Equal(0, d.RootElement.GetProperty("total").GetInt32()); }
        using (var hidden = await client.GetAsync("/api/categories/" + root)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var export = await client.GetAsync("/api/categories/export")) Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
        using (var preview = await client.PostAsync("/api/categories/imports/preview", Upload())) Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
        client.DefaultRequestHeaders.Authorization = null; client.DefaultRequestHeaders.Add("Cookie", $"dt_session={token}");
        using (var cookie = await client.GetAsync("/api/categories/tree")) Assert.Equal(HttpStatusCode.Forbidden, cookie.StatusCode);
    }
    [Fact]
    public async Task Api_preview_commit_retry_reads_persisted_draft_without_copy()
    {
        await using var h = await Host.Start(); using var client = h.Client(); client.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("admin", AuthClientType.Mobile));
        var rows = CategoryFixture.Rows.Select(r => r with { Name = r.Name + " HTTP" }).ToArray();
        using var preview = await client.PostAsync("/api/categories/imports/preview", Upload(rows)); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var doc = JsonDocument.Parse(await preview.Content.ReadAsStringAsync()); var id = doc.RootElement.GetProperty("previewId").GetString();
        using var first = await client.PostAsJsonAsync("/api/categories/imports/commit", new { previewId = id, confirmDuplicateNames = true }); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var repeated = await client.PostAsJsonAsync("/api/categories/imports/commit", new { previewId = id, confirmDuplicateNames = true }); Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        using var result = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync()); Assert.True(result.RootElement.GetProperty("alreadyImported").GetBoolean()); Assert.Equal(2, h.F.Store.Writes);
        using var tree = await client.GetAsync("/api/categories/tree"); Assert.Contains("HTTP", await tree.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
    [Fact]
    public async Task Razor_renders_tree_detail_empty_and_encoded_metadata_and_denies_viewer_import()
    {
        await using var h = await Host.Start(); await h.F.Grant("USER", AuthRole.Viewer, "ALL", true); using var client = h.Client(); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("user", AuthClientType.Web)); client.DefaultRequestHeaders.Accept.Add(new("text/html"));
        using (var tree = await client.GetAsync("/Categories")) { Assert.Equal(HttpStatusCode.OK, tree.StatusCode); var html = await tree.Content.ReadAsStringAsync(); Assert.Contains("DRAFT", html, StringComparison.Ordinal); Assert.Contains("/Categories/Detail/", html, StringComparison.Ordinal); }
        using (var detail = await client.GetAsync("/Categories/Detail/" + h.F.Store.State.ActiveNodes.Last().CategoryId)) { Assert.Equal(HttpStatusCode.OK, detail.StatusCode); Assert.Contains("metadata", await detail.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase); }
        using (var empty = await client.GetAsync("/Categories?Q=does-not-exist")) { Assert.Equal(HttpStatusCode.OK, empty.StatusCode); Assert.Contains("role=\"status\"", await empty.Content.ReadAsStringAsync(), StringComparison.Ordinal); }
        using (var blocked = await client.GetAsync("/Categories/Import")) { Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode); Assert.Equal("/Account/AccessDenied", blocked.Headers.Location!.OriginalString); }
        var unsafeDoc = CategoryFixture.Doc(CategoryFixture.Rows.Select(r => r with { Definition = "<script>alert(1)</script>" }).ToArray(), "xss-fixture"); await h.F.Import(unsafeDoc);
        using (var encoded = await client.GetAsync("/Categories/Detail/" + h.F.Store.State.ActiveNodes.Last().CategoryId)) { var html = await encoded.Content.ReadAsStringAsync(); Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal); Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal); }
    }
    [Fact]
    public async Task Razor_antiforgery_blocks_missing_token_and_valid_form_previews_then_commits()
    {
        await using var h = await Host.Start(); using var client = h.Client(); client.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web));
        using (var blocked = await client.PostAsync("/Categories/Import?handler=Preview", Upload(field: "Upload"))) Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Equal(1, h.F.Store.Writes);
        var get = await client.GetStringAsync("/Categories/Import"); var csrf = Field(get, "__RequestVerificationToken");
        using (var missing = await client.PostAsync("/Categories/Import?handler=Preview", new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", csrf } })))
        { Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode); Assert.Contains("10 MB", await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal); }
        using var form = Upload(CategoryFixture.Rows.Select(r => r with { Name = r.Name + " UI" }).ToArray(), "Upload"); form.Add(new StringContent(csrf), "__RequestVerificationToken");
        using var preview = await client.PostAsync("/Categories/Import?handler=Preview", form); Assert.Equal(HttpStatusCode.OK, preview.StatusCode); var html = await preview.Content.ReadAsStringAsync(); var id = Field(html, "PreviewId");
        var values = new Dictionary<string, string> { { "__RequestVerificationToken", Field(html, "__RequestVerificationToken") }, { "PreviewId", id }, { "ConfirmDuplicateNames", "true" } };
        using var commit = await client.PostAsync("/Categories/Import?handler=Commit", new FormUrlEncodedContent(values)); Assert.Equal(HttpStatusCode.OK, commit.StatusCode); Assert.Equal(2, h.F.Store.Writes);
    }
    [Fact]
    public async Task Preview_cache_is_actor_bound_and_expires_without_changing_auth_policy()
    {
        var f = new CategoryFixture(); var cache = new CategoryPreviewCache(f.Clock); var d = CategoryFixture.Doc(); var p = await f.Service.PreviewAsync("ADMIN", d); var key = cache.Add("ADMIN", d, p);
        Assert.Throws<CategoryConflictException>(() => cache.Get(key, "USER")); f.Clock.Advance(TimeSpan.FromMinutes(15)); Assert.Throws<CategoryConflictException>(() => cache.Get(key, "ADMIN"));
    }
    private static string Field(string html, string name)
    { var m = Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]+)\""); Assert.True(m.Success, "Expected form field missing: " + name); return WebUtility.HtmlDecode(m.Groups[1].Value); }
    private static MultipartFormDataContent Upload(IReadOnlyList<CategoryInput>? rows = null, string field = "file")
    {
        var data = (rows ?? CategoryFixture.Rows).Select(r => new { source_sheet = r.SourceSheet, source_row = r.SourceRow, source_stt = r.SourceStt, source_parent_stt = r.ParentSourceStt, level = r.Level, name = r.Name, segment_code = r.SegmentCode, definition = r.Definition });
        var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(data)), field, "fake-categories.json"); return form;
    }
    private sealed class Host(WebApplication app, CategoryFixture fixture, string address) : IAsyncDisposable
    {
        public CategoryFixture F { get; } = fixture;
        public HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        public async Task<string> Login(string username, AuthClientType type)
        { var auth = app.Services.GetRequiredService<AuthApplicationService>(); var result = await auth.LoginAsync(new(username, "fake-password", type, false, "category-test")); Assert.Equal(LoginStatus.Success, result.Status); return result.AccessToken!; }
        public static async Task<Host> Start()
        {
            var f = new CategoryFixture(); await f.Import();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ApplicationName = typeof(CategoryWeb).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory }); builder.WebHost.UseKestrel(); builder.Logging.ClearProviders();
            builder.Services.AddRazorPages(); builder.Services.AddSingleton<IAuthSystemStore>(f.Auth); builder.Services.AddSingleton<IPasswordHashService, newHash>(); builder.Services.AddSingleton(new AuthPolicyOptions()); builder.Services.AddSingleton<TimeProvider>(f.Clock); builder.Services.AddSingleton<AuthApplicationService>(); builder.Services.AddSingleton(f.Service); builder.Services.AddSingleton<CategoryPreviewCache>();
            builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName, _ => { }); builder.Services.AddAuthorization();
            var app = builder.Build(); app.Urls.Add("http://127.0.0.1:0"); app.UseTask005SessionAuthentication(); app.UseAuthorization(); app.MapTask006CategoryApi(); app.MapRazorPages(); await app.StartAsync();
            return new(app, f, app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
        private sealed class newHash : IPasswordHashService
        { private readonly TestPasswordHashService inner = new(); public string Hash(string id, string p) => inner.Hash(id, p); public PasswordHashVerification Verify(string id, string h, string p) => inner.Verify(id, h, p); }
    }
}
