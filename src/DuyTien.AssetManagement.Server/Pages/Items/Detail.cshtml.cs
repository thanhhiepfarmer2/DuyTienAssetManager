using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Server.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Pages.Items;
[Authorize]
public sealed class DetailModel(InventoryService service):InventoryPageModel
{public ItemRecord? Item{get;private set;}public async Task<IActionResult> OnGetAsync(string id,CancellationToken ct){Response.Headers.CacheControl="no-store";try{Item=await service.DetailAsync(Actor,id,ct);return Page();}catch(Exception e)when(InventoryWeb.Expected(e)){return Failure(e);}}}
