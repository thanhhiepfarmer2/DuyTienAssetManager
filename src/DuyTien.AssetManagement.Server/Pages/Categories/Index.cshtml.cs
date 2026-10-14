using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Categories;

[Authorize]
public sealed class IndexModel(CategoryCatalogService service) : PageModel
{
    public CategoryList Catalog { get; private set; } = new(0, null, 0, []);
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public string? Error { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try { if (PageNumber < 1 || PageNumber > 10000) throw new CategoryValidationException("Trang không hợp lệ."); Catalog = await service.ListAsync(CategoryWeb.Actor(User), Q, ct: ct); return Page(); }
        catch (CategoryForbiddenException) { return Forbid(); }
        catch (Exception e) when (CategoryWeb.IsExpected(e)) { Error = CategoryWeb.Message(e); Response.StatusCode = CategoryWeb.Status(e); return Page(); }
    }
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try { return File(await service.ExportAsync(CategoryWeb.Actor(User), ct), "text/plain; charset=utf-8", "danh-muc-DRAFT.txt"); }
        catch (CategoryForbiddenException) { return Forbid(); }
        catch (Exception e) when (CategoryWeb.IsExpected(e)) { Error = CategoryWeb.Message(e); Response.StatusCode = CategoryWeb.Status(e); return Page(); }
    }
}
