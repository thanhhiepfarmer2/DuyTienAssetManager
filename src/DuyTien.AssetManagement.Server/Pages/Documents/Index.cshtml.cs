using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Media;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Pages.Documents;
[Authorize]
public sealed class IndexModel(MediaLibraryService service,CategoryCatalogService catalog):MediaPageModel
{
    [BindProperty(SupportsGet=true)]public string? Q{get;set;}
    [BindProperty]public string? Name{get;set;}[BindProperty]public string? BusinessCode{get;set;}[BindProperty]public string? CategoryId{get;set;}[BindProperty]public string? Description{get;set;}
    public IReadOnlyList<DocumentView> Items{get;private set;}=[];public CategoryList Choices{get;private set;}=new(0,null,0,[]);public bool CanManage=>User.IsInRole("Admin")||User.IsInRole("DocumentManager");
    public async Task<IActionResult> OnGetAsync(CancellationToken ct){Response.Headers.CacheControl="no-store";NewKey();try{await Load(ct);return Page();}catch(Exception e)when(MediaWeb.Expected(e)){return Failure(e);}}
    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {Response.Headers.CacheControl="no-store";try{if(!ModelState.IsValid)throw new MediaValidationException("Metadata form không hợp lệ.");var result=await service.CreateDocumentAsync(CategoryWeb.Actor(User),new(new(Name??"",BusinessCode??"",CategoryId??"",Description??""),0),Key,ct);return RedirectToPage("Detail",new{id=result.EntityId});}catch(Exception e)when(MediaWeb.Expected(e)){try{await Load(ct);}catch(Exception read)when(MediaWeb.Expected(read)||CategoryWeb.IsExpected(read)){Error="Chưa tải được danh sách.";}return Failure(e);}}
    private async Task Load(CancellationToken ct){Items=await service.DocumentsAsync(CategoryWeb.Actor(User),Q,ct);if(CanManage)Choices=await catalog.ListAsync(CategoryWeb.Actor(User),ct:ct);}
}
