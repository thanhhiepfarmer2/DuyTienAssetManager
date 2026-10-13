using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Server.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Pages.Items;
[Authorize]
public sealed class IndexModel(InventoryService service):InventoryPageModel
{
    [BindProperty(SupportsGet=true)]public string? Q{get;set;}
    [BindProperty(SupportsGet=true)]public string? CategoryId{get;set;}
    [BindProperty(SupportsGet=true)]public int PageNumber{get;set;}=1;
    public ItemList Result{get;private set;}=new([],0,1,20,default);
    public async Task<IActionResult> OnGetAsync(CancellationToken ct){Response.Headers.CacheControl="no-store";if(!ModelState.IsValid){Error="Bộ lọc không hợp lệ.";Response.StatusCode=400;return Page();}try{Result=await service.ListAsync(Actor,new(Q,CategoryId,PageNumber),ct);return Page();}catch(Exception e)when(InventoryWeb.Expected(e)){return Failure(e);}}
}
