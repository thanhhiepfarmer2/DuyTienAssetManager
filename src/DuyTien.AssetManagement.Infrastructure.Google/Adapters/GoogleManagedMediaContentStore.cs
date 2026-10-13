using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleManagedMediaContentStore(GoogleWorkspaceOptions options,GoogleApiRequestExecutor api):IManagedMediaContentStore
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private const string Fields="id,name,mimeType,size,parents,appProperties,trashed,ownedByMe,capabilities(canDownload,canAddChildren)";
    public async Task<string> AllocateIdAsync(CancellationToken ct=default)
    {
        await VerifyFolder(ct);using var response=await api.SendAsync(()=>new HttpRequestMessage(HttpMethod.Get,UriFor("drive/v3/files/generateIds?count=1&space=drive&type=files")),GoogleRequestSemantics.SafeRead,ct);var ids=await ReadJson<IdResponse>(response,ct);if(ids.Ids?.Length!=1||!ValidId(ids.Ids[0]))throw new ExternalStorageException("Drive trả ID cấp trước không hợp lệ.");return ids.Ids[0];
    }
    public async Task EnsureUploadedAsync(ManagedFile f,ReadOnlyMemory<byte> bytes,CancellationToken ct=default)
    {
        CheckFile(f);if(bytes.Length!=f.Size||Convert.ToHexString(SHA256.HashData(bytes.Span))!=f.Sha256)throw new ExternalStorageException("Upload bytes không khớp operation đã đăng ký.");await VerifyFolder(ct);
        var existing=await MetadataOrMissing(f.DriveFileId,ct);if(existing is not null){_=await DownloadVerifiedAsync(f,ct);return;}
        var metadata=new{id=f.DriveFileId,name=f.FileName,mimeType=f.MimeType,parents=new[]{options.DriveFolderId!},appProperties=new Dictionary<string,string>{{"task","TASK-009"},{"operationId",f.UploadOperationId},{"sha256",f.Sha256}}};
        try
        {
            if(bytes.Length>5*1024*1024) await Resumable(f,metadata,bytes,ct);
            else { using var response=await api.SendAsync(()=>{var parts=new MultipartContent("related","duytien_media_"+Guid.NewGuid().ToString("N"));parts.Add(JsonContent.Create(metadata,options:Json));var binary=new ByteArrayContent(bytes.ToArray());binary.Headers.ContentType=new(f.MimeType);parts.Add(binary);return new HttpRequestMessage(HttpMethod.Post,UriFor("upload/drive/v3/files?uploadType=multipart&fields=id")){Content=parts};},GoogleRequestSemantics.NonIdempotentWrite,ct); }
        }
        catch(ExternalStorageException){_=await DownloadVerifiedAsync(f,ct);return;}
        _=await DownloadVerifiedAsync(f,ct);
    }
    public async Task<byte[]> DownloadVerifiedAsync(ManagedFile f,CancellationToken ct=default)
    {
        CheckFile(f);var meta=await MetadataOrMissing(f.DriveFileId,ct)??throw new ExternalStorageException("Drive file chưa có hoặc chưa cấp quyền; cần đối soát.");
        if(meta.Id!=f.DriveFileId||meta.Name!=f.FileName||meta.MimeType!=f.MimeType||meta.Trashed||!meta.OwnedByMe||meta.Capabilities?.CanDownload!=true||meta.Parents?.Contains(options.DriveFolderId!,StringComparer.Ordinal)!=true||meta.AppProperties?.GetValueOrDefault("task")!="TASK-009"||meta.AppProperties.GetValueOrDefault("operationId")!=f.UploadOperationId||meta.AppProperties.GetValueOrDefault("sha256")!=f.Sha256||!long.TryParse(meta.Size,NumberStyles.None,CultureInfo.InvariantCulture,out var size)||size!=f.Size)throw new ExternalStorageException("Metadata/owner/parent/checksum operation Drive không khớp; không phục vụ file.");
        using var response=await api.SendAsync(()=>new HttpRequestMessage(HttpMethod.Get,UriFor("drive/v3/files/"+Uri.EscapeDataString(f.DriveFileId)+"?alt=media")),GoogleRequestSemantics.SafeRead,ct);
        if(response.Content.Headers.ContentLength is {} length&&length!=f.Size)throw new ExternalStorageException("Content length Drive không khớp registry.");
        try
        {
            await using var input=await response.Content.ReadAsStreamAsync(ct);using var result=new MemoryStream();var buffer=new byte[81920];int read;
            while((read=await input.ReadAsync(buffer,ct))>0){if(result.Length+read>f.Size)throw new ExternalStorageException("Drive content vượt giới hạn registry.");result.Write(buffer,0,read);}
            var data=result.ToArray();if(data.LongLength!=f.Size||Convert.ToHexString(SHA256.HashData(data))!=f.Sha256)throw new ExternalStorageException("Nội dung Drive không khớp checksum đã đăng ký.");return data;
        }
        catch(IOException e){throw new ExternalStorageTransientException("Không đọc được file Drive.",false,e);}
        catch(HttpRequestException e){throw new ExternalStorageTransientException("Không đọc được file Drive.",false,e);}
        catch(OperationCanceledException e)when(!ct.IsCancellationRequested){throw new ExternalStorageTransientException("Đọc file Drive timeout.",false,e);}
    }
    private async Task Resumable(ManagedFile file,object metadata,ReadOnlyMemory<byte> bytes,CancellationToken ct)
    {
        using var initial=await api.SendAsync(()=>{var r=new HttpRequestMessage(HttpMethod.Post,UriFor("upload/drive/v3/files?uploadType=resumable&fields=id")){Content=JsonContent.Create(metadata,options:Json)};r.Headers.Add("X-Upload-Content-Type",file.MimeType);r.Headers.Add("X-Upload-Content-Length",file.Size.ToString(CultureInfo.InvariantCulture));return r;},GoogleRequestSemantics.NonIdempotentWrite,ct);
        var session=initial.Headers.Location;var allowed=new Uri(options.DriveApiBaseUrl);
        if(session is null||!session.IsAbsoluteUri||session.Scheme!=allowed.Scheme||session.Authority!=allowed.Authority||session.UserInfo.Length!=0||session.AbsolutePath!="/upload/drive/v3/files")throw new ExternalStorageException("Drive session URI không thuộc endpoint được cấu hình; không gửi credential/content.");
        var offset=0;for(var attempt=0;attempt<Math.Max(1,options.MaxRetryAttempts);attempt++)
        {
            try
            {
                using var result=await api.SendAsync(()=>{var r=new HttpRequestMessage(HttpMethod.Put,session){Content=new ByteArrayContent(bytes[offset..].ToArray())};r.Content.Headers.ContentType=new(file.MimeType);r.Content.Headers.ContentRange=new System.Net.Http.Headers.ContentRangeHeaderValue(offset,bytes.Length-1,bytes.Length);return r;},GoogleRequestSemantics.NonIdempotentWrite,ct,acceptResumeIncomplete:true);
                if(result.IsSuccessStatusCode)return;offset=ResumeOffset(result,bytes.Length);
            }
            catch(ExternalStorageException)
            {
                using var status=await api.SendAsync(()=>{var r=new HttpRequestMessage(HttpMethod.Put,session){Content=new ByteArrayContent([])};r.Content.Headers.ContentRange=new System.Net.Http.Headers.ContentRangeHeaderValue(bytes.Length);return r;},GoogleRequestSemantics.IdempotentWrite,ct,acceptResumeIncomplete:true);
                if(status.IsSuccessStatusCode)return;offset=ResumeOffset(status,bytes.Length);
            }
        }
        throw new ExternalStorageTransientException("Resumable upload chưa hoàn tất; giữ operation và Drive ID để retry/đối soát.",true);
    }
    private static int ResumeOffset(HttpResponseMessage response,int total)
    {
        if(!response.Headers.TryGetValues("Range",out var values))return 0;var all=values.Take(2).ToArray();if(all.Length!=1)throw new ExternalStorageException("Resumable Range header không hợp lệ.");var raw=all[0];
        if(!System.Net.Http.Headers.RangeHeaderValue.TryParse(raw,out var range)||range.Unit!="bytes"||range.Ranges.Count!=1||range.Ranges.Single().From!=0||range.Ranges.Single().To is not {} end||end<0||end>=total-1)throw new ExternalStorageException("Resumable byte range không hợp lệ; chưa xác nhận upload.");return checked((int)end+1);
    }
    private async Task VerifyFolder(CancellationToken ct)
    {Configured();var meta=await MetadataOrMissing(options.DriveFolderId!,ct);if(meta is null||meta.Id!=options.DriveFolderId||meta.MimeType!="application/vnd.google-apps.folder"||meta.Trashed||!meta.OwnedByMe||meta.Capabilities?.CanAddChildren!=true)throw new ExternalStorageException("Drive target phải là My Drive folder của operator/app, có quyền thêm file; chưa ghi.");}
    private async Task<Metadata?> MetadataOrMissing(string id,CancellationToken ct)
    {
        Configured();if(!ValidId(id))throw new ExternalStorageException("Drive ID không hợp lệ.");
        try{using var response=await api.SendAsync(()=>new HttpRequestMessage(HttpMethod.Get,UriFor("drive/v3/files/"+Uri.EscapeDataString(id)+"?fields="+Uri.EscapeDataString(Fields))),GoogleRequestSemantics.SafeRead,ct);return await ReadJson<Metadata>(response,ct);}
        catch(ExternalStorageAccessDeniedException e)when(e.StatusCode==404){return null;}
    }
    private void Configured(){if(options.GetMissingDriveSettings().Count>0)throw new ExternalStorageException("Drive configuration chưa đủ; không fallback scope/resource.");}
    private static bool ValidId(string? id)=>!string.IsNullOrWhiteSpace(id)&&id.Length<=200&&id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    private void CheckFile(ManagedFile f){Configured();if(!ValidId(f.DriveFileId)||f.Size is <1 or >25*1024*1024)throw new ExternalStorageException("Registry file/size không hợp lệ.");}
    private Uri UriFor(string path)=>new(new Uri(options.DriveApiBaseUrl),path);
    private static async Task<T> ReadJson<T>(HttpResponseMessage response,CancellationToken ct)
    {try{return await response.Content.ReadFromJsonAsync<T>(Json,ct)??throw new ExternalStorageException("Drive response thiếu dữ liệu.");}catch(JsonException){throw new ExternalStorageException("Drive JSON không hợp lệ.");}catch(IOException e){throw new ExternalStorageTransientException("Drive response lỗi đọc.",false,e);}catch(HttpRequestException e){throw new ExternalStorageTransientException("Drive response lỗi đọc.",false,e);}catch(OperationCanceledException e)when(!ct.IsCancellationRequested){throw new ExternalStorageTransientException("Drive response timeout.",false,e);}}
    private sealed record IdResponse(string[] Ids);
    private sealed record Metadata(string Id,string Name,string MimeType,string? Size,string[]? Parents,Dictionary<string,string>? AppProperties,bool Trashed,bool OwnedByMe,Capabilities? Capabilities);
    private sealed record Capabilities(bool CanDownload,bool CanAddChildren);
}
