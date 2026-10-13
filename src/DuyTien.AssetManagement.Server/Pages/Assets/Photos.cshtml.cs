using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Server.Media;
using DuyTien.AssetManagement.Server.Assets;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Pages.Assets;
[Authorize,RequestSizeLimit(MediaWeb.RequestCap),RequestFormLimits(MultipartBodyLengthLimit=MediaWeb.RequestCap)]
public sealed class PhotosModel(MediaLibraryService service,AssetCrudService assets,MediaUploadOptions options):MediaPageModel
{
    protected override string? RetryEntity=>FileId;
    public string UploadCapLabel=> (options.MaxPhotoBytes/1048576d).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"MiB";
    public long UploadExpectedVersion=>Retrying("Upload")?ExpectedVersion:Item?.Asset.Version??ExpectedVersion;
    public string FileActionKey(MediaFileView file)=>FormKey(Retrying("Delete",file.FileId)?"Delete":file.State=="AVAILABLE"?"Update":"Cancel",file.FileId);
    public bool RetryingFile(MediaFileView file)=>Retrying("Update",file.FileId)||Retrying("Delete",file.FileId)||Retrying("Cancel",file.FileId);
    public AssetView? Item{get;private set;}public IReadOnlyList<MediaFileView> Files{get;private set;}=[];public bool CanManage=>User.IsInRole("Admin")||User.IsInRole("AssetEditor");
    [BindProperty]public IFormFile? Upload{get;set;}[BindProperty]public long ExpectedVersion{get;set;}[BindProperty]public string? Description{get;set;}[BindProperty]public bool IsPrimary{get;set;}[BindProperty]public string? FileId{get;set;}[BindProperty]public string? Reason{get;set;}
    public async Task<IActionResult> OnGetAsync(string id,CancellationToken ct){Response.Headers.CacheControl="no-store";NewKey();try{await Load(id,ct);ExpectedVersion=Item!.Asset.Version;return Page();}catch(Exception e)when(MediaWeb.Expected(e)){return Failure(e);}catch(Exception e)when(AssetWeb.IsExpected(e)){return AssetFailure(e);}}
    public Task<IActionResult> OnPostUploadAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.UploadPhotoAsync(CategoryWeb.Actor(User),id,await MediaWeb.ReadFile(Upload,"PHOTO",options,ct),ExpectedVersion,Description??"",IsPrimary,Key,ct),ct);
    public Task<IActionResult> OnPostUpdateAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.UpdatePhotoAsync(CategoryWeb.Actor(User),FileId??"",new(ExpectedVersion,Description??"",IsPrimary,Reason??""),Key,ct),ct);
    public Task<IActionResult> OnPostDeleteAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.DeletePhotoAsync(CategoryWeb.Actor(User),FileId??"",new(ExpectedVersion,Reason??""),Key,ct),ct);
    public Task<IActionResult> OnPostReconcileAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.ReconcileAsync(CategoryWeb.Actor(User),FileId??"",ct),ct);
    public Task<IActionResult> OnPostCancelAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.CancelUploadAsync(CategoryWeb.Actor(User),FileId??"",new(ExpectedVersion,Reason??""),Key,ct),ct);
    private async Task<IActionResult> Run(string id,Func<Task<object>> action,CancellationToken ct)
    {Response.Headers.CacheControl="no-store";try{if(!ModelState.IsValid)throw new MediaValidationException("Form/version không hợp lệ.");if(!CanManage)throw new MediaForbiddenException();var parentFiles=await service.PhotosAsync(CategoryWeb.Actor(User),id,ct);if(!string.IsNullOrEmpty(FileId)&&!parentFiles.Any(f=>f.FileId==FileId))throw new MediaNotFoundException();var result=await action();var json=JsonSuccess(result);if(json is not null)return json;Feedback="Thao tác đã được xác nhận, file cũ được giữ.";NewKey();ModelState.Clear();await Load(id,ct);ExpectedVersion=Item!.Asset.Version;return Page();}catch(Exception e)when(MediaWeb.Expected(e)){if(!JsonRequest&&Feedback is null)try{await Load(id,ct);}catch(Exception read)when(MediaWeb.Expected(read)||AssetWeb.IsExpected(read)){Error="Chưa tải được ảnh lưu; giữ form/key.";}return Failure(e);}catch(Exception e)when(AssetWeb.IsExpected(e)){return AssetFailure(e);}}
    private async Task Load(string id,CancellationToken ct){Item=await assets.DetailAsync(CategoryWeb.Actor(User),id,ct);Files=await service.PhotosAsync(CategoryWeb.Actor(User),id,ct);}
    private IActionResult AssetFailure(Exception e){if(e is AssetForbiddenException)return Forbid();if(e is AssetNotFoundException)return NotFound();Error=AssetWeb.Message(e);Response.StatusCode=Feedback is null?AssetWeb.Status(e):200;return Page();}
}
