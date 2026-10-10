using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Categories;
[Authorize(Roles = "Admin,CatalogManager")]
[RequestSizeLimit(12 * 1024 * 1024)]
public sealed class ImportModel(CategoryCatalogService service, CategoryPreviewCache cache) : PageModel
{
    [BindProperty] public IFormFile? Upload { get; set; }
    [BindProperty] public string? PreviewId { get; set; }
    [BindProperty] public bool ConfirmDuplicateNames { get; set; }
    public CategoryPreview? Preview { get; private set; }
    public CategoryImportResult? Result { get; private set; }
    public void OnGet() => Response.Headers.CacheControl = "no-store";
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try {
            var actor = CategoryWeb.Actor(User); var document = await CategoryWeb.ReadUpload(Upload, ct);
            Preview = await service.PreviewAsync(actor, document, ct);
            PreviewId = Preview.CanCommit ? cache.Add(actor, document, Preview) : null;
            ModelState.Clear(); return Page();
        } catch (CategoryForbiddenException) { return Forbid(); }
        catch (CategoryNotFoundException) { return NotFound(); }
        catch (Exception e) when (CategoryWeb.IsExpected(e)) { ModelState.AddModelError("", CategoryWeb.Message(e)); Response.StatusCode = CategoryWeb.Status(e); return Page(); }
    }
    public async Task<IActionResult> OnPostCommitAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try {
            var actor = CategoryWeb.Actor(User); var entry = cache.Get(PreviewId ?? "", actor); Preview = entry.Preview;
            Result = await service.ImportAsync(actor, entry.Document, Preview.Revision, Preview.PreviewHash, entry.IdempotencyKey, ConfirmDuplicateNames, ct);
            cache.Remove(PreviewId!); PreviewId = null; return Page();
        } catch (CategoryForbiddenException) { return Forbid(); }
        catch (CategoryNotFoundException) { return NotFound(); }
        catch (Exception e) when (CategoryWeb.IsExpected(e)) { ModelState.AddModelError("", CategoryWeb.Message(e)); Response.StatusCode = CategoryWeb.Status(e); return Page(); }
    }
}
