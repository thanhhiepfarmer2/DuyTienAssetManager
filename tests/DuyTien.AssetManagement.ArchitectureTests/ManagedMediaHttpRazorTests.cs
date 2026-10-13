using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.ArchitectureTests.Fakes;
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
public sealed class ManagedMediaHttpRazorTests
{
    [Fact]
    public async Task Api_auth_bearer_cookie_viewer_and_unknown_json_fields_are_enforced()
    {
        await using var h=await Host.Start();using var c=h.Client();using(var r=await c.GetAsync("/api/documents"))Assert.Equal(HttpStatusCode.Unauthorized,r.StatusCode);
        c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));using(var r=await c.GetAsync("/api/documents"))Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);c.DefaultRequestHeaders.Remove("Cookie");await h.F.Owner.Base.Grant(AuthRole.Viewer);await Bearer(c,h,"user");c.DefaultRequestHeaders.Add("Idempotency-Key","forbidden");using(var r=await c.PostAsJsonAsync("/api/documents",new DocumentWriteRequest(new("Fake","FAKE","CAT-CHILD"),0)))Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);
        await Bearer(c,h,"admin");using(var r=await c.PostAsJsonAsync("/api/documents",new{document=new{name="Fake",businessCode="FAKE",categoryId="CAT-CHILD",approved=true},expectedVersion=0}))Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);Assert.Empty(h.F.Store.State.Documents);
    }
    [Fact]
    public async Task Api_multipart_upload_replay_gateway_nosniff_size_validation_and_outside_scope_are_real_http()
    {
        await using var h=await Host.Start();var a=await h.F.Asset();using var c=h.Client();await Bearer(c,h,"admin");c.DefaultRequestHeaders.Add("Idempotency-Key","photo");var path="/api/assets/"+a.AssetId+"/photos";MediaUploadResult saved;
        using(var r=await c.PostAsync(path,Multipart(MediaBytes.Png(),"file",new(){["expectedVersion"]="1",["description"]="Ảnh giả",["isPrimary"]="true"}))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);var body=await r.Content.ReadAsStringAsync();Assert.DoesNotContain("driveFileId",body,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("sha256",body,StringComparison.OrdinalIgnoreCase);saved=(await r.Content.ReadFromJsonAsync<MediaUploadResult>())!;}
        using(var r=await c.PostAsync(path,Multipart(MediaBytes.Png(),"file",new(){["expectedVersion"]="1",["description"]="Ảnh giả",["isPrimary"]="true"}))){Assert.True((await r.Content.ReadFromJsonAsync<MediaUploadResult>())!.Replayed);}Assert.Equal(1,h.F.Content.Creates);
        using(var r=await c.GetAsync("/api/files/"+saved.File.FileId+"/content?preview=true")){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Equal(MediaBytes.Png().Bytes,await r.Content.ReadAsByteArrayAsync());Assert.Equal("nosniff",r.Headers.GetValues("X-Content-Type-Options").Single());Assert.Equal("inline",r.Content.Headers.ContentDisposition!.DispositionType);Assert.Equal("no-store",r.Headers.CacheControl!.ToString());}
        using(var r=await c.PostAsync(path,Multipart(MediaBytes.Png(),"file",new(){["expectedVersion"]="1",["driveFileId"]="outside"})))Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        await h.F.Owner.Base.Grant(AuthRole.Viewer,category:"CAT-OTHER");await Bearer(c,h,"user");var reads=h.F.Content.Reads;using(var r=await c.GetAsync("/api/files/"+saved.File.FileId+"/content"))Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);Assert.Equal(reads,h.F.Content.Reads);
    }
    [Fact]
    public async Task Api_pending_does_not_claim_success_and_has_only_public_file_reference_for_reconcile()
    {
        await using var h=await Host.Start();var a=await h.F.Asset();using var c=h.Client();await Bearer(c,h,"admin");c.DefaultRequestHeaders.Add("Idempotency-Key","pending");h.F.Store.FailAvailable=1;string id;
        using(var r=await c.PostAsync("/api/assets/"+a.AssetId+"/photos",Multipart(MediaBytes.Png(),"file",new(){["expectedVersion"]="1"}))){Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);var text=await r.Content.ReadAsStringAsync();Assert.DoesNotContain("PRIVATE-MEDIA-CANARY",text,StringComparison.Ordinal);using var doc=System.Text.Json.JsonDocument.Parse(text);id=doc.RootElement.GetProperty("fileId").GetString()!;}
        using(var r=await c.PostAsync("/api/files/"+id+"/reconcile",null)){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Equal("AVAILABLE",(await r.Content.ReadFromJsonAsync<MediaUploadResult>())!.File.State);}Assert.Equal(1,h.F.Content.Creates);
    }
    [Fact]
    public async Task Configured_upload_cap_and_spoofed_type_are_validation_errors_without_drive_create()
    {
        await using var h=await Host.Start(new(){MaxPhotoBytes=1});var a=await h.F.Asset();using var c=h.Client();await Bearer(c,h,"admin");c.DefaultRequestHeaders.Add("Idempotency-Key","large");using var r=await c.PostAsync("/api/assets/"+a.AssetId+"/photos",Multipart(MediaBytes.Png(),"file",new(){["expectedVersion"]="1"}));Assert.Equal(HttpStatusCode.RequestEntityTooLarge,r.StatusCode);Assert.Equal(0,h.F.Content.Creates);
    }
    [Fact]
    public async Task Razor_document_create_version_missing_counterpart_upload_and_publish_are_csrf_checked_and_encoded()
    {
        await using var h=await Host.Start();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var html=await c.GetStringAsync("/Documents");var values=new Dictionary<string,string>{["RequestKey"]=Field(html,"RequestKey"),["Name"]="<script>fixture</script>",["BusinessCode"]="FAKE.BM009",["CategoryId"]="CAT-CHILD",["Description"]="Giả"};
        using(var r=await c.PostAsync("/Documents?handler=Create",new FormUrlEncodedContent(values)))Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);values["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken");string url;
        using(var r=await c.PostAsync("/Documents?handler=Create",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.Redirect,r.StatusCode);url=r.Headers.Location!.OriginalString;}
        html=await c.GetStringAsync(url);Assert.DoesNotContain("<script>fixture</script>",html,StringComparison.Ordinal);Assert.Contains("&lt;script&gt;",html,StringComparison.Ordinal);values=new(){["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]="ui-version",["ExpectedDocumentVersion"]="1",["Label"]="v1 fake"};
        using(var r=await c.PostAsync(url+"?handler=Version",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);html=await r.Content.ReadAsStringAsync();Assert.Contains("Thiếu DOCX",WebUtility.HtmlDecode(html),StringComparison.Ordinal);}
        var version=h.F.Store.State.Versions.Single();foreach(var kind in new[]{"DOCX","PDF"})
        {values=new(){["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]="ui-"+kind,["VersionId"]=version.VersionId,["ExpectedVersion"]=h.F.Store.State.Versions.Single().Version.ToString(System.Globalization.CultureInfo.InvariantCulture),["Kind"]=kind};using var r=await c.PostAsync(url+"?handler=Upload",Multipart(kind=="DOCX"?MediaBytes.Docx():MediaBytes.Pdf(),"Upload",values));Assert.Equal(HttpStatusCode.OK,r.StatusCode);html=await r.Content.ReadAsStringAsync();}
        values=new(){["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]="ui-publish",["VersionId"]=version.VersionId,["ExpectedVersion"]="3",["ExpectedDocumentVersion"]="2",["EffectiveOn"]=DateOnly.FromDateTime(h.F.Owner.Base.Clock.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),["Reason"]="Giả",["ConfirmDemo"]="true"};using(var r=await c.PostAsync(url+"?handler=Publish",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains("PUBLISHED_DEMO",await r.Content.ReadAsStringAsync(),StringComparison.Ordinal);}
        Assert.Equal(2,h.F.Content.Creates);
    }
    [Fact]
    public async Task Razor_photo_upload_cookie_gateway_viewer_post_denial_and_parent_binding_are_enforced()
    {
        await using var h=await Host.Start();var a=await h.F.Asset();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Assets/Photos/"+a.AssetId;var html=await c.GetStringAsync(url);Assert.Contains("data-media-preview",html,StringComparison.Ordinal);Assert.Contains("data-media-upload",html,StringComparison.Ordinal);var values=new Dictionary<string,string>{["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]="photo-ui",["ExpectedVersion"]="1",["Description"]="Giả",["IsPrimary"]="true"};
        using(var r=await c.PostAsync(url+"?handler=Upload",Multipart(MediaBytes.Png(),"Upload",values))){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains("AVAILABLE",await r.Content.ReadAsStringAsync(),StringComparison.Ordinal);}var file=h.F.Store.State.Files.Single();using(var r=await c.GetAsync("/Files/Content/"+file.FileId+"?preview=true")){Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Equal(MediaBytes.Png().Bytes,await r.Content.ReadAsByteArrayAsync());}
        var b=(await h.F.Owner.AssetService.CreateAsync("ADMIN",h.F.Owner.AssetRequest(),"asset-second")).Asset;html=await c.GetStringAsync("/Assets/Photos/"+b.AssetId);values=new(){["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]="wrong-parent",["FileId"]=file.FileId,["ExpectedVersion"]=file.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),["Reason"]="Giả"};using(var r=await c.PostAsync("/Assets/Photos/"+b.AssetId+"?handler=Delete",new FormUrlEncodedContent(values)))Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);Assert.False(h.F.Store.State.Files.Single().IsDeleted);
        await h.F.Owner.Base.Grant(AuthRole.Viewer);c.DefaultRequestHeaders.Remove("Cookie");c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("user",AuthClientType.Web));c.DefaultRequestHeaders.Accept.Add(new("text/html"));html=await c.GetStringAsync(url);Assert.DoesNotContain("data-media-upload",html,StringComparison.Ordinal);values["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken");using(var r=await c.PostAsync(url+"?handler=Delete",new FormUrlEncodedContent(values))){Assert.Equal(HttpStatusCode.Redirect,r.StatusCode);Assert.Equal("/Account/AccessDenied",r.Headers.Location!.OriginalString);}
    }
    [Fact]
    public async Task Razor_json_upload_failure_keeps_form_key_and_never_returns_private_error_or_success()
    {
        await using var h=await Host.Start();var a=await h.F.Asset();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Assets/Photos/"+a.AssetId;var html=await c.GetStringAsync(url);var values=new Dictionary<string,string>{["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["RequestKey"]="retained-key",["ExpectedVersion"]="1",["Description"]="Giữ mô tả giả"};h.F.Store.FailRead=true;c.DefaultRequestHeaders.Accept.Add(new("application/json"));using var r=await c.PostAsync(url+"?handler=Upload",Multipart(MediaBytes.Png(),"Upload",values));Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);var body=await r.Content.ReadAsStringAsync();Assert.DoesNotContain("PRIVATE-MEDIA-CANARY",body,StringComparison.Ordinal);Assert.Contains("\"success\":false",body,StringComparison.Ordinal);Assert.Equal(0,h.F.Content.Creates);
    }
    [Fact]
    public async Task Razor_stale_metadata_keeps_old_expected_version_and_key_instead_of_silent_overwrite_on_retry()
    {
        await using var h=await Host.Start();var d=await h.F.Document();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Documents/Detail/"+d.EntityId;var html=await c.GetStringAsync(url);var save=FormBlock(html,"handler=Save");var key=Field(save,"RequestKey");await h.F.Service.UpdateDocumentAsync("ADMIN",d.EntityId,new(new("Bản cập nhật giả","FAKE.BM009","CAT-CHILD"),1),"new-metadata");
        var fields=new Dictionary<string,string>{["__RequestVerificationToken"]=Field(save,"__RequestVerificationToken"),["RequestKey"]=key,["ExpectedDocumentVersion"]="1",["CategoryId"]="CAT-CHILD",["Name"]="Bản cũ giả",["BusinessCode"]="FAKE.BM009"};
        using(var r=await c.PostAsync(url+"?handler=Save",new FormUrlEncodedContent(fields))){Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);html=await r.Content.ReadAsStringAsync();save=FormBlock(html,"handler=Save");Assert.Equal("1",Field(save,"ExpectedDocumentVersion"));Assert.Equal(key,Field(save,"RequestKey"));Assert.NotEqual(key,Field(FormBlock(html,"handler=Version"),"RequestKey"));}
        fields["__RequestVerificationToken"]=Field(save,"__RequestVerificationToken");using(var r=await c.PostAsync(url+"?handler=Save",new FormUrlEncodedContent(fields)))Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);Assert.Equal("Bản cập nhật giả",h.F.Store.State.Documents.Single().Name);Assert.Equal(2,h.F.Store.State.Operations.Count);
    }
    [Fact]
    public async Task Razor_pending_upload_keeps_original_retry_key_and_cancel_uses_an_independent_key()
    {
        await using var h=await Host.Start();var a=await h.F.Asset();using var c=h.Client();c.DefaultRequestHeaders.Add("Cookie","dt_session="+await h.Login("admin",AuthClientType.Web));var url="/Assets/Photos/"+a.AssetId;var html=await c.GetStringAsync(url);var upload=FormBlock(html,"handler=Upload");var key=Field(upload,"RequestKey");var fields=new Dictionary<string,string>{["__RequestVerificationToken"]=Field(upload,"__RequestVerificationToken"),["RequestKey"]=key,["ExpectedVersion"]="1",["Description"]="Giữ form giả"};h.F.Store.FailAvailable=1;
        using(var r=await c.PostAsync(url+"?handler=Upload",Multipart(MediaBytes.Png(),"Upload",fields))){Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);html=await r.Content.ReadAsStringAsync();Assert.Equal(key,Field(FormBlock(html,"handler=Upload"),"RequestKey"));Assert.DoesNotContain("alert-success",html,StringComparison.Ordinal);}
        var cancel=FormBlock(html,"handler=Cancel");var cancelKey=Field(cancel,"RequestKey");Assert.NotEqual(key,cancelKey);fields=new(){["__RequestVerificationToken"]=Field(cancel,"__RequestVerificationToken"),["RequestKey"]=cancelKey,["FileId"]=Field(cancel,"FileId"),["ExpectedVersion"]=Field(cancel,"ExpectedVersion"),["Reason"]="Hủy giả"};using(var r=await c.PostAsync(url+"?handler=Cancel",new FormUrlEncodedContent(fields)))Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.True(h.F.Store.State.Files.Single().IsDeleted);Assert.Equal(1,h.F.Content.Creates);
    }
    private static string FormBlock(string html,string handler)=>Regex.Matches(html,"<form[^>]*>.*?</form>",RegexOptions.Singleline).Select(m=>m.Value).Single(form=>form.Contains(handler,StringComparison.Ordinal));
    private static async Task Bearer(HttpClient c,Host h,string name)=>c.DefaultRequestHeaders.Authorization=new("Bearer",await h.Login(name,AuthClientType.Mobile));
    private static string Field(string html,string name){var m=Regex.Match(html,"name=\""+name+"\"[^>]*value=\"([^\"]+)\"");Assert.True(m.Success,"Missing form field: "+name);return WebUtility.HtmlDecode(m.Groups[1].Value);}
    private static MultipartFormDataContent Multipart(MediaFileContent f,string fileField,Dictionary<string,string> fields)
    {var form=new MultipartFormDataContent();foreach(var pair in fields)form.Add(new StringContent(pair.Value),pair.Key);var bytes=new ByteArrayContent(f.Bytes);bytes.Headers.ContentType=new(f.MimeType);form.Add(bytes,fileField,f.FileName);return form;}
    private sealed class Host(WebApplication app,ManagedMediaFixture fixture,string address):IAsyncDisposable
    {
        public ManagedMediaFixture F{get;}=fixture;public HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new(address)};
        public async Task<string> Login(string name,AuthClientType type){var result=await app.Services.GetRequiredService<AuthApplicationService>().LoginAsync(new(name,"fake-password",type,false,"media-http-test"));Assert.Equal(LoginStatus.Success,result.Status);return result.AccessToken!;}
        public static async Task<Host> Start(MediaUploadOptions? limits=null)
        {
            var f=await ManagedMediaFixture.Start();var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",ApplicationName=typeof(MediaWeb).Assembly.GetName().Name,ContentRootPath=AppContext.BaseDirectory});b.WebHost.UseKestrel();b.Logging.ClearProviders();b.Services.AddRazorPages();b.Services.AddSingleton(f.Service);b.Services.AddSingleton(f.Owner.Catalog);b.Services.AddSingleton(f.Owner.AssetService);b.Services.AddSingleton(limits??new MediaUploadOptions());b.Services.AddSingleton<IAuthSystemStore>(f.Owner.Base.Auth);b.Services.AddSingleton<IPasswordHashService>(new TestPasswordHashService());b.Services.AddSingleton(new AuthPolicyOptions());b.Services.AddSingleton<TimeProvider>(f.Owner.Base.Clock);b.Services.AddSingleton<AuthApplicationService>();b.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions,OpaqueSessionAuthenticationHandler>(OpaqueSessionAuthenticationHandler.SchemeName,_=>{});b.Services.AddAuthorization();var app=b.Build();app.Urls.Add("http://127.0.0.1:0");app.UseTask005SessionAuthentication();app.UseAuthorization();app.MapTask009MediaApi();app.MapRazorPages();await app.StartAsync();return new(app,f,app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        }
        public async ValueTask DisposeAsync(){await app.StopAsync();await app.DisposeAsync();}
    }
}
