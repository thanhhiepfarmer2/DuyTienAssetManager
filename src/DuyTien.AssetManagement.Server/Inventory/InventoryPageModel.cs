using DuyTien.AssetManagement.Application.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Inventory;
public abstract class InventoryPageModel:PageModel
{
    public string? Error{get;protected set;}
    protected string Actor=>InventoryWeb.Actor(User);
    protected IActionResult Failure(Exception e)
    {if(e is InventoryForbiddenException)return Forbid();if(e is InventoryNotFoundException)return NotFound();Response.StatusCode=InventoryWeb.Status(e);Error=InventoryWeb.Message(e);return Page();}
}
