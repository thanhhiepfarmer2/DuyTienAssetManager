using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Server.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Pages.Assets;
[Authorize]
public sealed class DetailModel(AssetCrudService service) : PageModel
{
    public AssetView? Item { get; private set; }
    [BindProperty] public AssetForm Form { get; set; } = new();
    [BindProperty] public string RequestKey { get; set; } = "";
    [BindProperty] public string TreeVersionId { get; set; } = "";
    [BindProperty] public long ExpectedVersion { get; set; }
    [BindProperty] public string? Reason { get; set; }
    public string? Error { get; private set; }
    public bool CanEdit => User.IsInRole("Admin") || User.IsInRole("AssetEditor");
    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var detail = await service.DetailWithChoicesAsync(AssetWeb.Actor(User), id, CanEdit, ct); Item = detail.Item; Form = AssetForm.From(Item);
            ExpectedVersion = Item.Asset.Version; RequestKey = Guid.NewGuid().ToString("N");
            if (CanEdit) { Form.Choices = detail.Choices!; TreeVersionId = Form.Choices.TreeVersionId ?? ""; }
            return Page();
        }
        catch (AssetNotFoundException) { return NotFound(); }
        catch (AssetForbiddenException) { return Forbid(); }
        catch (Exception e) when (AssetWeb.IsExpected(e)) { Error = AssetWeb.Message(e); Response.StatusCode = AssetWeb.Status(e); return Page(); }
    }
    public Task<IActionResult> OnPostSaveAsync(string id, CancellationToken ct) => Save(id, "UPDATE", ct);
    public Task<IActionResult> OnPostDeleteAsync(string id, CancellationToken ct) => Save(id, "DELETE", ct);
    public Task<IActionResult> OnPostRestoreAsync(string id, CancellationToken ct) => Save(id, "RESTORE", ct);
    private async Task<IActionResult> Save(string id, string action, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!CanEdit) return Forbid();
        try
        {
            if (ModelState.TryGetValue(nameof(ExpectedVersion), out var versionState) && versionState.Errors.Count > 0) throw new AssetValidationException("Version nhập không hợp lệ.");
            if (action == "UPDATE" && !ModelState.IsValid) throw new AssetValidationException("Có trường nhập không hợp lệ; kiểm tra form.");
            if (action == "UPDATE") await service.UpdateAsync(AssetWeb.Actor(User), id, new(Form.Input(), ExpectedVersion, TreeVersionId), RequestKey, ct);
            else if (action == "DELETE") await service.DeleteAsync(AssetWeb.Actor(User), id, new(ExpectedVersion, Reason ?? ""), RequestKey, ct);
            else await service.RestoreAsync(AssetWeb.Actor(User), id, new(ExpectedVersion, Reason ?? ""), RequestKey, ct);
            return RedirectToPage("Detail", new { id });
        }
        catch (AssetNotFoundException) { return NotFound(); }
        catch (AssetForbiddenException) { return Forbid(); }
        catch (Exception e) when (AssetWeb.IsExpected(e))
        {
            Error = AssetWeb.Message(e); Response.StatusCode = AssetWeb.Status(e);
            if (action != "UPDATE") ModelState.Clear();
            if (e is AssetValidationException v && v.Field is not null) ModelState.AddModelError(v.Field == "Reason" ? "Reason" : "Form." + (v.Field == "Aliases" ? "AliasesText" : v.Field), v.Message);
            try
            {
                var detail = await service.DetailWithChoicesAsync(AssetWeb.Actor(User), id, true, ct); Item = detail.Item;
                if (action != "UPDATE") Form = AssetForm.From(Item);
                Form.Choices = detail.Choices!;
            }
            catch (AssetNotFoundException) { return NotFound(); }
            catch (AssetForbiddenException) { return Forbid(); }
            catch (Exception load) when (AssetWeb.IsExpected(load)) { Error += " Chưa tải được bản lưu; giữ thông tin đã nhập và tải lại khi kết nối ổn định."; }
            return Page();
        }
    }
}
