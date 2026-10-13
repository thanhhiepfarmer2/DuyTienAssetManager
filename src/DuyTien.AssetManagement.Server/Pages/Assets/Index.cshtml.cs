using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Server.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Assets;
[Authorize]
public sealed class IndexModel(AssetCrudService service) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public bool IncludeDescendants { get; set; } = true;
    [BindProperty(SupportsGet = true)] public string? Condition { get; set; }
    [BindProperty(SupportsGet = true)] public string? OrgUnitId { get; set; }
    [BindProperty(SupportsGet = true)] public string? LocationId { get; set; }
    [BindProperty(SupportsGet = true)] public string? CustodianRef { get; set; }
    [BindProperty(SupportsGet = true)] public bool IncludeDeleted { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    public AssetList Result { get; private set; } = new([], 0, 1, 20, default);
    public AssetFormChoices Filters { get; private set; } = new(null, [], [], []);
    public string? Error { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!ModelState.IsValid) { Error = "Bộ lọc không hợp lệ."; Response.StatusCode = 400; return Page(); }
        try { (Result, Filters) = await service.BrowseAsync(AssetWeb.Actor(User), new(Q, CategoryId, IncludeDescendants, Condition, OrgUnitId, LocationId, CustodianRef, IncludeDeleted, PageNumber, PageSize), ct); return Page(); }
        catch (AssetForbiddenException) { return Forbid(); }
        catch (AssetNotFoundException) { return NotFound(); }
        catch (Exception e) when (AssetWeb.IsExpected(e)) { Error = AssetWeb.Message(e); Response.StatusCode = AssetWeb.Status(e); return Page(); }
    }
}
