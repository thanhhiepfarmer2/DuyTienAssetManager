using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Categories;
[Authorize]
public sealed class DetailModel(CategoryCatalogService service) : PageModel
{
    public CategoryView? Category { get; private set; }
    public IReadOnlyList<CategoryHistoryEntry> History { get; private set; } = [];
    public string? Error { get; private set; }
    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try { Category = await service.DetailAsync(CategoryWeb.Actor(User), id, ct); History = await service.HistoryAsync(CategoryWeb.Actor(User), id, ct); return Page(); }
        catch (CategoryNotFoundException) { return NotFound(); }
        catch (CategoryForbiddenException) { return Forbid(); }
        catch (Exception e) when (CategoryWeb.IsExpected(e)) { Error = CategoryWeb.Message(e); Response.StatusCode = CategoryWeb.Status(e); return Page(); }
    }
}
