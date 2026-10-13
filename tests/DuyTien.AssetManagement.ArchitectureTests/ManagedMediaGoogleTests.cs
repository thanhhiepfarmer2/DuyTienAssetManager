using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;
public sealed class ManagedMediaGoogleTests
{
    [Fact]
    public async Task Real_adapters_fake_transport_persist_pending_before_drive_and_reconcile_lost_responses_without_duplicate()
    {
        var f=await Rig.Start();var a=await f.Owner.Create();f.Drive.LoseUploadResponse=true;f.Drive.AfterCreate=()=>f.Owner.Handler.LoseResponseOnce=true;
        var uploaded=await f.Service().UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"Synthetic",true,"upload");Assert.Equal("AVAILABLE",uploaded.File.State);Assert.Equal(1,f.Drive.Creates);Assert.True(f.Drive.PendingWasDurable);Assert.Equal(2,(await f.Store().ReadAsync()).Operations.Count);
        var replay=await f.Service().UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"Synthetic",true,"upload");Assert.True(replay.Replayed);Assert.Equal(1,f.Drive.Creates);Assert.Equal(MediaBytes.Png().Bytes,(await f.Service().DownloadAsync("ADMIN",uploaded.File.FileId)).Bytes);
        Assert.DoesNotContain("formulaValue",f.Owner.Handler.LastBody,StringComparison.Ordinal);Assert.All(f.Owner.Handler.Uris,u=>Assert.Contains("business-fake",u,StringComparison.Ordinal));
    }
    [Fact]
    public async Task Drive_success_final_sheet_failure_is_pending_and_recovered_via_same_id_after_new_service()
    {
        var f=await Rig.Start();var a=await f.Owner.Create();f.Drive.AfterCreate=()=>f.Owner.Handler.FailBeforeCommitOnce=true;var e=await Assert.ThrowsAsync<MediaPendingException>(()=>f.Service().UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Jpeg(),1,"",false,"same"));Assert.Equal("PENDING",(await f.Store().ReadAsync()).Files.Single().State);Assert.Equal(1,f.Drive.Creates);
        f.Drive.AfterCreate=null;var result=await f.Service().ReconcileAsync("ADMIN",e.FileId);Assert.Equal("AVAILABLE",result.File.State);Assert.Equal(1,f.Drive.Creates);MediaCatalogIntegrity.Validate(await f.Store().ReadAsync());
    }
    [Theory][InlineData("partial")][InlineData("header")][InlineData("record")][InlineData("audit")][InlineData("range")]
    public async Task Unknown_or_modified_registry_fails_closed_before_write(string fault)
    {
        var f=await Rig.Start();await f.Service().CreateDocumentAsync("ADMIN",new(new("Synthetic doc","FAKE009",f.Owner.Child),0),"doc");var posts=f.Owner.Handler.Posts;
        if(fault=="partial")f.Owner.Handler.Tables.Remove("Files");else if(fault=="header")f.Owner.Handler.Tables["Files"].Rows[0][0]="Unknown";else if(fault=="record")f.Owner.Handler.Tables["Documents"].Rows[1][1]="null";else if(fault=="audit")f.Owner.Handler.Tables["MediaAuditEvents"].Rows[1][3]="unmanaged";else f.Owner.Handler.WrongRange=true;
        await Assert.ThrowsAsync<ExternalStorageException>(()=>f.Store().ReadAsync());Assert.Equal(posts,f.Owner.Handler.Posts);
    }
    [Theory][InlineData("parent")][InlineData("operation")][InlineData("mime")][InlineData("content")][InlineData("download-denied")]
    public async Task Drive_metadata_or_content_changes_block_gateway_without_overwrite(string fault)
    {
        var f=await Rig.Start();var a=await f.Owner.Create();var uploaded=await f.Service().UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"photo");var record=f.Drive.Records.Values.Single();
        if(fault=="parent")record.Parent="outside-folder";else if(fault=="operation")record.Properties["operationId"]="other";else if(fault=="mime")record.Mime="text/html";else if(fault=="content")record.Bytes[20]^=1;else record.CanDownload=false;
        await Assert.ThrowsAsync<ExternalStorageException>(()=>f.Service().DownloadAsync("ADMIN",uploaded.File.FileId));Assert.Equal(1,f.Drive.Creates);
    }
    [Fact]
    public async Task Folder_owner_or_acl_and_unsupported_tree_metadata_guard_are_preserved()
    {
        var f=await Rig.Start();var a=await f.Owner.Create();f.Drive.FolderOwned=false;await Assert.ThrowsAsync<ExternalStorageException>(()=>f.Service().UploadPhotoAsync("ADMIN",a.AssetId,MediaBytes.Png(),1,"",false,"bad"));Assert.Empty((await f.Store().ReadAsync()).Operations);Assert.Equal(0,f.Drive.Creates);f.Drive.FolderOwned=true;
        await f.Service().CreateDocumentAsync("ADMIN",new(new("Synthetic","FAKE",f.Owner.Child),0),"doc");await Assert.ThrowsAsync<ExternalStorageException>(()=>f.Owner.Store().ReadAsync());Assert.True(f.Owner.Handler.Tables.ContainsKey("Documents"));
    }
    [Theory][InlineData("normal")][InlineData("lost-put")][InlineData("partial")]
    public async Task Large_pdf_uses_resumable_and_server_byte_range_status_with_same_durable_drive_id(string mode)
    {
        var f=await Rig.Start();f.Drive.LosePutResponse=mode=="lost-put";f.Drive.PartialPut=mode=="partial";var d=await f.Service().CreateDocumentAsync("ADMIN",new(new("Synthetic large","FAKE.LARGE",f.Owner.Child),0),"large-doc");var v=await f.Service().CreateVersionAsync("ADMIN",d.EntityId,new("large-v1",1),"large-v");var input=LargePdf();
        var result=await f.Service().UploadDocumentFileAsync("ADMIN",v.EntityId,"PDF",input,1,"large-file");Assert.Equal("AVAILABLE",result.File.State);Assert.True(f.Drive.PutCalls>=1);Assert.Equal(1,f.Drive.Creates);Assert.True(f.Drive.PendingWasDurable);Assert.Equal(input.Bytes,(await f.Service().DownloadAsync("ADMIN",result.File.FileId)).Bytes);
        Assert.True((await f.Service().UploadDocumentFileAsync("ADMIN",v.EntityId,"PDF",input,1,"large-file")).Replayed);Assert.Equal(1,f.Drive.Creates);
    }
    [Fact]
    public async Task Resumable_foreign_location_is_rejected_before_bearer_or_bytes_are_forwarded()
    {
        var f=await Rig.Start();f.Drive.UnsafeSessionLocation=true;var d=await f.Service().CreateDocumentAsync("ADMIN",new(new("Fake","FAKE",f.Owner.Child),0),"doc");var v=await f.Service().CreateVersionAsync("ADMIN",d.EntityId,new("v1",1),"v");await Assert.ThrowsAsync<MediaPendingException>(()=>f.Service().UploadDocumentFileAsync("ADMIN",v.EntityId,"PDF",LargePdf(),1,"upload"));Assert.Equal(0,f.Drive.PutCalls);Assert.Equal(0,f.Drive.Creates);Assert.Equal("FAILED",(await f.Store().ReadAsync()).Files.Single().State);
    }
    private static MediaFileContent LargePdf(){var original=MediaBytes.Pdf();var text=Encoding.ASCII.GetString(original.Bytes);return original with{Bytes=Encoding.ASCII.GetBytes(text.Replace("%%EOF","%"+new string('F',6*1024*1024)+"\n%%EOF",StringComparison.Ordinal))};}
    [Theory][InlineData("GET",true,false)][InlineData("POST",true,false)][InlineData("PUT",false,false)][InlineData("PUT",true,true)]
    public async Task Resume_incomplete_response_is_accepted_only_for_explicit_put_without_changing_default_executor(string method,bool accept,bool succeeds)
    {
        using var http=new HttpClient(new ResumeOnlyHandler());var executor=new GoogleApiRequestExecutor(new(){MaxRetryAttempts=1},new ResumeToken(),http);var call=executor.SendAsync(()=>new HttpRequestMessage(new HttpMethod(method),"https://www.googleapis.com/fake"),GoogleRequestSemantics.NonIdempotentWrite,acceptResumeIncomplete:accept);
        if(succeeds){using var response=await call;Assert.Equal(308,(int)response.StatusCode);}else await Assert.ThrowsAsync<ExternalStorageException>(()=>call);
    }
    private sealed class ResumeOnlyHandler:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)308));}
    private sealed class ResumeToken:IGoogleAccessTokenProvider{public Task<string> GetAccessTokenAsync(CancellationToken ct=default)=>Task.FromResult("synthetic-resume-token");public void Invalidate(){}}
    internal sealed class Rig
    {
        public GoogleTreeMutationTests.Fixture Owner{get;}public DriveHandler Drive{get;}public GoogleWorkspaceOptions Options{get;}=new(){SpreadsheetId="business-fake",SystemSpreadsheetId="system-fake",DriveFolderId="folder-fake",OAuthClientId="fake-client",OAuthClientSecret="fake-config",OAuthRefreshToken="fake-config",MaxRetryAttempts=3,InitialRetryDelayMilliseconds=1};
        private readonly Router router;private Rig(GoogleTreeMutationTests.Fixture owner){Owner=owner;Drive=new(owner.Handler);router=new(owner.Handler,Drive);}
        public static async Task<Rig> Start()=>new(await GoogleTreeMutationTests.Fixture.Start());
        private GoogleApiRequestExecutor Api()=>new(Options,new Token(),new HttpClient(router));
        public GoogleMediaCatalogStore Store()=>new(Options,Api());
        public MediaLibraryService Service()=>new(Store(),new GoogleManagedMediaContentStore(Options,Api()),new GoogleAssetCatalogStore(Options,Api()),new GoogleCategoryCatalogStore(Options,Api()),Owner.AuthFixture.Auth,Owner.Writer,Owner.AuthFixture.Clock,new());
        private sealed class Token:IGoogleAccessTokenProvider{public Task<string> GetAccessTokenAsync(CancellationToken ct=default)=>Task.FromResult("synthetic-media-token");public void Invalidate(){}}
    }
    private sealed class Router(HttpMessageHandler sheets,HttpMessageHandler drive):HttpMessageHandler
    {
        private readonly HttpMessageInvoker sheetInvoker=new(sheets,false),driveInvoker=new(drive,false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>r.RequestUri!.AbsolutePath.Contains("spreadsheets/",StringComparison.Ordinal)?sheetInvoker.SendAsync(r,ct):driveInvoker.SendAsync(r,ct);
    }
    internal sealed class DriveRecord(string id,string name,string mime,string parent,Dictionary<string,string> properties,byte[] bytes)
    {public string Id{get;}=id;public string Name{get;}=name;public string Mime{get;set;}=mime;public string Parent{get;set;}=parent;public Dictionary<string,string> Properties{get;}=properties;public byte[] Bytes{get;}=bytes;public bool CanDownload{get;set;}=true;}
    internal sealed class DriveHandler(GoogleCategoryCatalogStoreTests.CategorySheetsHandler sheets):HttpMessageHandler
    {
        private int reserved;public int PutCalls{get;private set;}public bool LosePutResponse{get;set;}public bool PartialPut{get;set;}public bool UnsafeSessionLocation{get;set;}private readonly Dictionary<string,(JsonElement Meta,byte[] Bytes)> sessions=[];public int Creates{get;private set;}public bool PendingWasDurable{get;private set;}public bool LoseUploadResponse{get;set;}public bool FolderOwned{get;set;}=true;public Action? AfterCreate{get;set;}public Dictionary<string,DriveRecord> Records{get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);var path=request.RequestUri!.AbsolutePath;
            if(path.EndsWith("generateIds",StringComparison.Ordinal))return Response(new{ids=new[]{"Drive_Fake_"+ ++reserved},space="drive",kind="drive#generatedIds"});
            if(request.Method==HttpMethod.Put)
            {
                PutCalls++;var key=request.RequestUri.Query.Split('&').Single(x=>x.StartsWith("upload_id=",StringComparison.Ordinal))[10..];var session=sessions[key];var id=session.Meta.GetProperty("id").GetString()!;var data=await request.Content!.ReadAsByteArrayAsync(ct);
                if(Records.ContainsKey(id))return Response(new{id});if(data.Length==0)return ResumeResponse(session.Bytes.Length);
                Assert.Equal(session.Bytes.Length,request.Content.Headers.ContentRange!.From);Assert.Equal(session.Meta.GetProperty("mimeType").GetString(),request.Content.Headers.ContentType!.MediaType);
                if(PartialPut){PartialPut=false;sessions[key]=(session.Meta,data[..262144]);return ResumeResponse(262144);}var all=session.Bytes.Concat(data).ToArray();Create(session.Meta,all);if(LosePutResponse){LosePutResponse=false;throw new HttpRequestException("Synthetic lost resumable PUT");}return Response(new{id});
            }
            if(request.Method==HttpMethod.Post&&request.RequestUri.Query.Contains("uploadType=resumable",StringComparison.Ordinal))
            {using var parsed=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var key="Session_Fake_"+sessions.Count;sessions.Add(key,(parsed.RootElement.Clone(),[]));var result=new HttpResponseMessage(HttpStatusCode.OK);result.Headers.Location=new Uri(UnsafeSessionLocation?"https://outside.invalid/upload/drive/v3/files?upload_id=fake":"https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&upload_id="+key);return result;}
            if(request.Method==HttpMethod.Post)
            {
                var multipart=Assert.IsType<MultipartContent>(request.Content);var parts=multipart.ToArray();using var json=JsonDocument.Parse(await parts[0].ReadAsStringAsync(ct));var p=json.RootElement;var id=p.GetProperty("id").GetString()!;if(Records.ContainsKey(id))return new(HttpStatusCode.Conflict);
                var op=p.GetProperty("appProperties").GetProperty("operationId").GetString();PendingWasDurable=sheets.Tables.TryGetValue("Files",out var table)&&table.Rows.Skip(1).Any(row=>row[1].Contains("PENDING",StringComparison.Ordinal)&&row[1].Contains(id,StringComparison.Ordinal)&&row[1].Contains(op!,StringComparison.Ordinal));Assert.True(PendingWasDurable);
                Create(p,await parts[1].ReadAsByteArrayAsync(ct));if(LoseUploadResponse){LoseUploadResponse=false;throw new HttpRequestException("Synthetic lost Drive response");}return Response(new{id});
            }
            var fileId=path.Split('/').Last();if(fileId=="folder-fake")return Response(new{id=fileId,name="Synthetic folder",mimeType="application/vnd.google-apps.folder",ownedByMe=FolderOwned,trashed=false,capabilities=new{canAddChildren=true,canDownload=false}});
            if(!Records.TryGetValue(fileId,out var f))return new(HttpStatusCode.NotFound);
            if(request.RequestUri.Query.Contains("alt=media",StringComparison.Ordinal))return new(HttpStatusCode.OK){Content=new ByteArrayContent(f.Bytes)};
            return Response(new{id=f.Id,name=f.Name,mimeType=f.Mime,size=f.Bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),parents=new[]{f.Parent},appProperties=f.Properties,trashed=false,ownedByMe=true,capabilities=new{canDownload=f.CanDownload,canAddChildren=false}});
        }
        private void Create(JsonElement p,byte[] bytes)
        {var id=p.GetProperty("id").GetString()!;var op=p.GetProperty("appProperties").GetProperty("operationId").GetString();PendingWasDurable=sheets.Tables["Files"].Rows.Skip(1).Any(row=>row[1].Contains("PENDING",StringComparison.Ordinal)&&row[1].Contains(id,StringComparison.Ordinal)&&row[1].Contains(op!,StringComparison.Ordinal));Assert.True(PendingWasDurable);Records.Add(id,new(id,p.GetProperty("name").GetString()!,p.GetProperty("mimeType").GetString()!,p.GetProperty("parents")[0].GetString()!,p.GetProperty("appProperties").Deserialize<Dictionary<string,string>>()!,bytes));Creates++;AfterCreate?.Invoke();}
        private static HttpResponseMessage ResumeResponse(int received){var r=new HttpResponseMessage((HttpStatusCode)308);if(received>0)r.Headers.TryAddWithoutValidation("Range","bytes=0-"+(received-1));return r;}
        private static HttpResponseMessage Response(object body)=>new(HttpStatusCode.OK){Content=JsonContent.Create(body)};
    }
}
