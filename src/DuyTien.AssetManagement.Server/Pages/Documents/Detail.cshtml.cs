using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Server.Media;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Pages.Documents;
[Authorize,RequestSizeLimit(MediaWeb.RequestCap),RequestFormLimits(MultipartBodyLengthLimit=MediaWeb.RequestCap)]
public sealed class DetailModel(MediaLibraryService service,MediaUploadOptions options):MediaPageModel
{
    protected override string? RetryEntity=>string.Equals(Handler,"Upload",StringComparison.OrdinalIgnoreCase)?VersionId+":"+Kind:FileId??VersionId;
    public string UploadCapLabel=> (options.MaxDocumentBytes/1048576d).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"MiB";
    public DocumentView? Item{get;private set;}public bool CanManage=>User.IsInRole("Admin")||User.IsInRole("DocumentManager");
    [BindProperty]public string? Name{get;set;}[BindProperty]public string? BusinessCode{get;set;}[BindProperty]public string? CategoryId{get;set;}[BindProperty]public string? Description{get;set;}
    [BindProperty]public long ExpectedDocumentVersion{get;set;}[BindProperty]public string? Label{get;set;}[BindProperty]public string? VersionId{get;set;}[BindProperty]public long ExpectedVersion{get;set;}[BindProperty]public string? Kind{get;set;}[BindProperty]public IFormFile? Upload{get;set;}
    [BindProperty]public DateOnly? EffectiveOn{get;set;}[BindProperty]public string? Reason{get;set;}[BindProperty]public bool ConfirmDemo{get;set;}[BindProperty]public string? FileId{get;set;}
    public async Task<IActionResult> OnGetAsync(string id,CancellationToken ct){Response.Headers.CacheControl="no-store";NewKey();try{await Load(id,ct);Name=Item!.Name;BusinessCode=Item.BusinessCode;CategoryId=Item.CategoryId;Description=Item.Description;ExpectedDocumentVersion=Item.Version;return Page();}catch(Exception e)when(MediaWeb.Expected(e)){return Failure(e);}}
    public Task<IActionResult> OnPostSaveAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.UpdateDocumentAsync(CategoryWeb.Actor(User),id,new(new(Name??"",BusinessCode??"",CategoryId??"",Description??""),ExpectedDocumentVersion),Key,ct),ct);
    public Task<IActionResult> OnPostVersionAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.CreateVersionAsync(CategoryWeb.Actor(User),id,new(Label??"",ExpectedDocumentVersion),Key,ct),ct);
    public Task<IActionResult> OnPostPublishAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.PublishVersionAsync(CategoryWeb.Actor(User),VersionId??"",new(ExpectedVersion,ExpectedDocumentVersion,EffectiveOn??throw new MediaValidationException("Chọn ngày hiệu lực."),Reason??"",ConfirmDemo),Key,ct),ct);
    public Task<IActionResult> OnPostArchiveAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.ArchiveDocumentAsync(CategoryWeb.Actor(User),id,new(ExpectedDocumentVersion,Reason??""),Key,ct),ct);
    public Task<IActionResult> OnPostUploadAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.UploadDocumentFileAsync(CategoryWeb.Actor(User),VersionId??"",Kind??"",await MediaWeb.ReadFile(Upload,Kind??"",options,ct),ExpectedVersion,Key,ct),ct);
    public Task<IActionResult> OnPostReconcileAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.ReconcileAsync(CategoryWeb.Actor(User),FileId??"",ct),ct);
    public Task<IActionResult> OnPostCancelAsync(string id,CancellationToken ct)=>Run(id,async()=>await service.CancelUploadAsync(CategoryWeb.Actor(User),FileId??"",new(ExpectedVersion,Reason??""),Key,ct),ct);
    private async Task<IActionResult> Run(string id,Func<Task<object>> action,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";try{if(!ModelState.IsValid)throw new MediaValidationException("Form/version không hợp lệ.");if(!CanManage)throw new MediaForbiddenException();var parent=await service.DocumentAsync(CategoryWeb.Actor(User),id,ct);if(!string.IsNullOrEmpty(VersionId)&&!parent.Versions.Any(v=>v.VersionId==VersionId)||!string.IsNullOrEmpty(FileId)&&!parent.Versions.Any(v=>v.Docx?.FileId==FileId||v.Pdf?.FileId==FileId))throw new MediaNotFoundException();var result=await action();var json=JsonSuccess(result);if(json is not null)return json;Feedback="Thao tác đã được xác nhận; dữ liệu vẫn là DEMO.";NewKey();ModelState.Clear();await Load(id,ct);ExpectedDocumentVersion=Item!.Version;return Page();}
        catch(Exception e)when(MediaWeb.Expected(e)){if(!JsonRequest&&Feedback is null)try{await Load(id,ct);}catch(Exception read)when(MediaWeb.Expected(read)){Error="Chưa tải được phiên bản lưu; giữ form/key.";}return Failure(e);}
    }
    private async Task Load(string id,CancellationToken ct)=>Item=await service.DocumentAsync(CategoryWeb.Actor(User),id,ct);
}
