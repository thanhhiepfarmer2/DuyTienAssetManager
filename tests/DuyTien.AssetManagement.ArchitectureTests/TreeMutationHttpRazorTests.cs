using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Categories;
using DuyTien.AssetManagement.Server.Assets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace DuyTien.AssetManagement.ArchitectureTests;

// Kestrel + compiled Razor only; not browser UAT. All reference/entity data is synthetic.
public sealed class TreeMutationHttpRazorTests
{
    [Fact]
    public async Task Api_requires_bearer_and_blocks_viewer_and_unknown_overposted_approval_fields()
    {
        await using var h = await Host.Start(); using var c = h.Client();
        using (var r = await c.PostAsJsonAsync("/api/tree-changes/preview", Request())) Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        await h.F.Base.Grant(AuthRole.Viewer); c.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("user", AuthClientType.Mobile));
        using (var r = await c.PostAsJsonAsync("/api/tree-changes/preview", Request())) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        c.DefaultRequestHeaders.Authorization = null; c.DefaultRequestHeaders.Add("Cookie", "dt_session=" + await h.Login("admin", AuthClientType.Web));
        using (var r = await c.PostAsJsonAsync("/api/tree-changes/preview", Request())) Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        c.DefaultRequestHeaders.Remove("Cookie"); c.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("admin", AuthClientType.Mobile));
        using (var r = await c.PostAsJsonAsync("/api/tree-changes/preview", new {kind="MOVE_CATEGORY",reason="fake",categoryId="CAT-MACHINE",destinationCategoryId="CAT-OTHER",approved=true})) Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
    [Fact]
    public async Task Api_preview_confirm_commit_retry_conflict_and_scoped_history_use_current_complete_tree()
    {
        await using var h = await Host.Start(); var asset = (await h.F.AssetService.CreateAsync("ADMIN", h.F.AssetRequest(), "create")).Asset; using var c = h.Client(); c.DefaultRequestHeaders.Authorization = new("Bearer", await h.Login("admin", AuthClientType.Mobile));
        using var response = await c.PostAsJsonAsync("/api/tree-changes/preview", Request()); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var p = (await response.Content.ReadFromJsonAsync<TreeMutationPreview>())!; Assert.Equal(2,p.AffectedCategoryCount); Assert.Equal(1,p.AffectedAssetCount); Assert.Equal("no-store",response.Headers.CacheControl!.ToString());
        c.DefaultRequestHeaders.Add("Idempotency-Key","api-joint"); var body=new TreeMutationWeb.CommitRequest(Request(),p.TreeRevision,p.AssetRevision,p.PreviewHash,false);
        using (var r=await c.PostAsJsonAsync("/api/tree-changes/commit",body)) Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode); Assert.Equal(0,h.F.Store.JointWrites);
        body=body with {ConfirmImpact=true}; using (var r=await c.PostAsJsonAsync("/api/tree-changes/commit",body)) Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        using (var r=await c.PostAsJsonAsync("/api/tree-changes/commit",body)){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.True((await r.Content.ReadFromJsonAsync<TreeMutationResult>())!.Replayed);}
        using (var r=await c.PostAsJsonAsync("/api/tree-changes/commit",body with {Request=Request() with {Reason="different"}})) Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);
        using (var r=await c.GetAsync("/api/categories/CAT-CHILD/history")){Assert.Equal(HttpStatusCode.OK,r.StatusCode); var entries=(await r.Content.ReadFromJsonAsync<CategoryHistoryEntry[]>())!;Assert.Contains(entries,x=>x.FullCode=="TST.MCN");Assert.Contains(entries,x=>x.FullCode=="ZZZ.TST.MCN");}
        Assert.Equal(asset.ObjectCode,(await h.F.AssetService.DetailAsync("ADMIN",asset.AssetId)).Asset.ObjectCode);
    }
    [Fact]
    public async Task Razor_manage_get_missing_csrf_preview_and_confirm_render_complete_draft_and_history()
    {
        await using var h=await Host.Start();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));c.DefaultRequestHeaders.Accept.Add(new("text/html"));
        var url="/Categories/Manage/CAT-MACHINE"; var html=await c.GetStringAsync(url);Assert.Contains("DRAFT",html,StringComparison.Ordinal);Assert.Contains("Form.CategoryId",html,StringComparison.Ordinal);Assert.Contains("data-tree-status",html,StringComparison.Ordinal);
        var values=Values(html);using(var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values.Where(p=>p.Key!="__RequestVerificationToken"))))Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        using(var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);html=await r.Content.ReadAsStringAsync();Assert.Contains("Báo cáo",WebUtility.HtmlDecode(html),StringComparison.Ordinal);Assert.Contains("Form.Reason",html,StringComparison.Ordinal);}
        values=Values(html);values["PreviewHash"]=Field(html,"PreviewHash");values["ExpectedTreeRevision"]=Field(html,"ExpectedTreeRevision");values["ExpectedAssetRevision"]=Field(html,"ExpectedAssetRevision");values["ConfirmImpact"]="true";
        using(var r=await c.PostAsync(url+"?handler=Commit",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains("Đã lưu thay đổi vào phiên bản chưa duyệt",WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()),StringComparison.Ordinal);Assert.All(h.F.Store.State.Categories.ActiveNodes,n=>Assert.Equal("DRAFT",n.State));}
        using(var r=await c.GetAsync("/Categories/Detail/CAT-CHILD")){Assert.Equal(HttpStatusCode.OK,r.StatusCode);var text=WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());Assert.Contains("Lịch sử",text,StringComparison.Ordinal);Assert.Contains("ZZZ.TST.MCN",text,StringComparison.Ordinal);}
        Assert.Equal(1,h.F.Store.JointWrites);
    }
    [Fact]
    public async Task Razor_old_preview_is_409_and_missing_reason_cycle_are_visible_without_write()
    {
        await using var h=await Host.Start();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Categories/Manage/CAT-MACHINE";var html=await c.GetStringAsync(url);var values=Values(html);values["Form.Reason"]="";
        using(var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);Assert.Contains("Lý do",WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()),StringComparison.Ordinal);}
        values=Values(html); values["Form.DestinationCategoryId"]="CAT-CHILD";using(var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains("Cycle",await r.Content.ReadAsStringAsync(),StringComparison.Ordinal);Assert.DoesNotContain("handler=Commit",await r.Content.ReadAsStringAsync(),StringComparison.Ordinal);}
        values=Values(html);using(var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values))) html=await r.Content.ReadAsStringAsync();
        await h.F.AssetService.CreateAsync("ADMIN",h.F.AssetRequest(),"new-asset");values=Values(html);values["PreviewHash"]=Field(html,"PreviewHash");values["ExpectedTreeRevision"]=Field(html,"ExpectedTreeRevision");values["ExpectedAssetRevision"]=Field(html,"ExpectedAssetRevision");values["ConfirmImpact"]="true";
        using(var r=await c.PostAsync(url+"?handler=Commit",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);Assert.Contains("preview",await r.Content.ReadAsStringAsync(),StringComparison.OrdinalIgnoreCase);}
        Assert.Equal(0,h.F.Store.JointWrites);
    }
    [Fact]
    public async Task Razor_bulk_move_asset_checkbox_choices_preview_commit_and_viewer_denial()
    {
        await using var h=await Host.Start();var a=(await h.F.AssetService.CreateAsync("ADMIN",h.F.AssetRequest(),"create")).Asset;using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Assets/Move/"+a.AssetId;var html=await c.GetStringAsync(url);Assert.Contains("name=\"Form.AssetIds\"",html,StringComparison.Ordinal);Assert.Contains(a.ObjectCode,html,StringComparison.Ordinal);
        var values=new Dictionary<string,string>{["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]=Field(html,"RequestKey"),["Form.Kind"]="MOVE_ASSETS",["Form.Reason"]="Fixture giả",["Form.AssetIds"]=a.AssetId,["Form.DestinationCategoryId"]="CAT-OTHER",["Form.DestinationOrgUnitId"]="OU-B",["Form.DestinationLocationId"]="LOC-B"};
        using(var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);html=await r.Content.ReadAsStringAsync();}
        values["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken");values["RequestKey"]=Field(html,"RequestKey");values["PreviewHash"]=Field(html,"PreviewHash");values["ExpectedTreeRevision"]=Field(html,"ExpectedTreeRevision");values["ExpectedAssetRevision"]=Field(html,"ExpectedAssetRevision");values["ConfirmImpact"]="true";
        using(var r=await c.PostAsync(url+"?handler=Commit",new FormUrlEncodedContent(values))) Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        var saved=h.F.Store.State.Assets.Assets.Single();Assert.Equal(a.ObjectCode,saved.ObjectCode);Assert.Equal("LOC-B",saved.LocationId);Assert.Equal("CAT-OTHER",saved.CategoryId);
        await h.F.Base.Grant(AuthRole.Viewer);c.DefaultRequestHeaders.Remove("Cookie");c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("user",AuthClientType.Web));c.DefaultRequestHeaders.Accept.Add(new("text/html"));using(var r=await c.GetAsync("/Assets/Move")){Assert.Equal(HttpStatusCode.Redirect,r.StatusCode);Assert.Equal("/Account/AccessDenied",r.Headers.Location!.OriginalString);}
    }
    [Fact]
    public async Task Razor_upstream_error_retains_form_and_does_not_show_fake_commit_or_private_error()
    {
        await using var h=await Host.Start();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Categories/Manage/CAT-MACHINE";var html=await c.GetStringAsync(url);var values=Values(html);h.F.Store.FailRead=true;
        using var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values));Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);var body=await r.Content.ReadAsStringAsync();Assert.Equal(values["Form.Reason"],Field(body,"Form.Reason"));Assert.Equal(values["RequestKey"],Field(body,"RequestKey"));Assert.Contains("value=\"CAT-MACHINE\" selected=\"selected\"",body,StringComparison.Ordinal);Assert.Contains("value=\"CAT-OTHER\" selected=\"selected\"",body,StringComparison.Ordinal);Assert.DoesNotContain("PRIVATE-TREE-CANARY",body,StringComparison.Ordinal);Assert.DoesNotContain("alert-success",body,StringComparison.Ordinal);Assert.Equal(0,h.F.Store.JointWrites);
    }
    [Fact]
    public async Task Razor_bulk_error_preserves_selected_ids_and_destination_without_reading_private_labels()
    {
        await using var h=await Host.Start();var a=(await h.F.AssetService.CreateAsync("ADMIN",h.F.AssetRequest(),"create")).Asset;using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Assets/Move/"+a.AssetId;var html=await c.GetStringAsync(url);
        var values=new Dictionary<string,string>{["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]=Field(html,"RequestKey"),["Form.Kind"]="MOVE_ASSETS",["Form.Reason"]="Fixture giữ form",["Form.AssetIds"]=a.AssetId,["Form.DestinationCategoryId"]="CAT-OTHER",["Form.DestinationOrgUnitId"]="OU-B",["Form.DestinationLocationId"]="LOC-B"};h.F.Store.FailRead=true;
        using var r=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values));Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);var body=await r.Content.ReadAsStringAsync();Assert.Equal(a.AssetId,Field(body,"Form.AssetIds"));Assert.Equal(values["Form.Reason"],Field(body,"Form.Reason"));Assert.Contains("value=\"OU-B\" selected=\"selected\"",body,StringComparison.Ordinal);Assert.Contains("value=\"LOC-B\" selected=\"selected\"",body,StringComparison.Ordinal);Assert.Equal(0,h.F.Store.JointWrites);
    }
    [Fact]
    public async Task Razor_confirmed_commit_then_refresh_failure_preserves_true_success_without_retry_button()
    {
        await using var h=await Host.Start();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Categories/Manage/CAT-MACHINE";var html=await c.GetStringAsync(url);var values=Values(html);
        using(var preview=await c.PostAsync(url+"?handler=Preview",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.OK,preview.StatusCode);html=await preview.Content.ReadAsStringAsync();}
        values=Values(html);values["PreviewHash"]=Field(html,"PreviewHash");values["ExpectedTreeRevision"]=Field(html,"ExpectedTreeRevision");values["ExpectedAssetRevision"]=Field(html,"ExpectedAssetRevision");values["ConfirmImpact"]="true";h.F.Store.FailReadAfterJointCommit=true;
        using var r=await c.PostAsync(url+"?handler=Commit",new FormUrlEncodedContent(values));Assert.Equal(HttpStatusCode.OK,r.StatusCode);var body=WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());Assert.Contains("Commit đã được xác nhận",body,StringComparison.Ordinal);Assert.Contains("alert-success",body,StringComparison.Ordinal);Assert.DoesNotContain("handler=Commit",body,StringComparison.Ordinal);Assert.DoesNotContain("PRIVATE-TREE-CANARY",body,StringComparison.Ordinal);Assert.Equal(1,h.F.Store.JointWrites);Assert.Equal("CAT-OTHER",h.F.Store.State.Categories.ActiveNodes.Single(n=>n.CategoryId=="CAT-MACHINE").ParentId);
    }
    private static TreeMutationRequest Request()=>new("MOVE_CATEGORY","Fixture giả","CAT-MACHINE","CAT-OTHER");
    private static Dictionary<string,string> Values(string html)=>new(){["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]=Field(html,"RequestKey"),["Form.Kind"]="MOVE_CATEGORY",["Form.Reason"]="Fixture giả",["Form.CategoryId"]="CAT-MACHINE",["Form.DestinationCategoryId"]="CAT-OTHER",["Form.Name"]="",["Form.SegmentCode"]="",["Form.Definition"]="",["Form.DeletePlan"]="EMPTY_ONLY"};
    private static string Field(string html,string name){var match=Regex.Match(html,"name=\""+name+"\"[^>]*value=\"([^\"]+)\"");Assert.True(match.Success,"Missing form field: "+name);return WebUtility.HtmlDecode(match.Groups[1].Value);}
    private sealed class Host(WebApplication app,TreeMutationFixture fixture,string address):IAsyncDisposable
    {
        public TreeMutationFixture F {get;}=fixture;
        public HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new(address)};
        public async Task<string> Login(string name,AuthClientType type){var result=await app.Services.GetRequiredService<AuthApplicationService>().LoginAsync(new(name,"fake-password",type,false,"tree-http-test"));Assert.Equal(LoginStatus.Success,result.Status);return result.AccessToken!;}
        public static async Task<Host> Start()
        {
            var f=await TreeMutationFixture.Start();var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",ApplicationName=typeof(TreeMutationWeb).Assembly.GetName().Name,ContentRootPath=AppContext.BaseDirectory});builder.WebHost.UseKestrel();builder.Logging.ClearProviders();
            builder.Services.AddRazorPages();builder.Services.AddSingleton(f.Service);builder.Services.AddSingleton(f.Catalog);builder.Services.AddSingleton(f.AssetService);builder.Services.AddSingleton<CategoryPreviewCache>();builder.Services.AddSingleton<IAuthSystemStore>(f.Base.Auth);builder.Services.AddSingleton<IPasswordHashService>(new TestPasswordHashService());builder.Services.AddSingleton(new AuthPolicyOptions());builder.Services.AddSingleton<TimeProvider>(f.Base.Clock);builder.Services.AddSingleton<AuthApplicationService>();
            builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions,OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName,_=>{});builder.Services.AddAuthorization();var app=builder.Build();app.Urls.Add("http://127.0.0.1:0");app.UseTask005SessionAuthentication();app.UseAuthorization();app.MapTask008TreeApi();app.MapTask006CategoryApi();app.MapTask007AssetApi();app.MapRazorPages();await app.StartAsync();return new(app,f,app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        }
        public async ValueTask DisposeAsync(){await app.StopAsync();await app.DisposeAsync();}
    }
}
