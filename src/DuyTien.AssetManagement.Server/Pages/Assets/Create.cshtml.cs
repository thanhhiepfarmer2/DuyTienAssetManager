using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Server.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Assets;
[Authorize(Roles = "Admin,AssetEditor")]
public sealed class CreateModel(AssetCrudService service) : PageModel
{
    [BindProperty] public AssetForm Form { get; set; } = new();
    [BindProperty] public string RequestKey { get; set; } = "";
    [BindProperty] public string TreeVersionId { get; set; } = "";
    public string? Error { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; RequestKey = Guid.NewGuid().ToString("N");
        try { Form.Choices = await service.ChoicesAsync(AssetWeb.Actor(User), ct: ct); TreeVersionId = Form.Choices.TreeVersionId ?? ""; return Page(); }
        catch (AssetForbiddenException) { return Forbid(); }
        catch (Exception e) when (AssetWeb.IsExpected(e)) { Error = AssetWeb.Message(e); Response.StatusCode = AssetWeb.Status(e); return Page(); }
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            if (!ModelState.IsValid) { Response.StatusCode = 400; Form.Choices = await service.ChoicesAsync(AssetWeb.Actor(User), ct: ct); return Page(); }
            var result = await service.CreateAsync(AssetWeb.Actor(User), new(Form.Input(), 0, TreeVersionId), RequestKey, ct);
            return RedirectToPage("Detail", new { id = result.Asset.AssetId });
        }
        catch (AssetForbiddenException) { return Forbid(); }
        catch (AssetNotFoundException) { return NotFound(); }
        catch (Exception e) when (AssetWeb.IsExpected(e))
        {
            Error = AssetWeb.Message(e); Response.StatusCode = AssetWeb.Status(e);
            if (e is AssetValidationException v && v.Field is not null) ModelState.AddModelError("Form." + (v.Field == "Aliases" ? "AliasesText" : v.Field), v.Message);
            try { Form.Choices = await service.ChoicesAsync(AssetWeb.Actor(User), ct: ct); } catch (AssetForbiddenException) { return Forbid(); }
            catch (Exception load) when (AssetWeb.IsExpected(load)) { Error += " Danh mục chọn chưa tải được; giữ dữ liệu đã nhập và tải lại khi kết nối ổn định."; }
            return Page();
        }
    }
}
