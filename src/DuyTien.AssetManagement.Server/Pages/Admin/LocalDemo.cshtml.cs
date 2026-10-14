using DuyTien.AssetManagement.Server.LocalOffline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
namespace DuyTien.AssetManagement.Server.Pages.Admin;

[Authorize(Roles = "Admin")]
public sealed class LocalDemoModel(IServiceProvider services) : PageModel
{
    public string? Message { get; private set; }
    public IActionResult OnGet() => services.GetService<LocalDemoInitializer>() is null ? NotFound() : Page();
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (services.GetService<LocalDemoInitializer>() is not { } initializer) return NotFound();
        try { await initializer.InitializeAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, ct); Message = "Đã lưu demo GIẢ trên máy. Có thể tạo máy, import Items riêng và upload ảnh/tài liệu; không phê duyệt dữ liệu công ty."; }
        catch (CategoryForbiddenException) { return Forbid(); }
        catch (Exception e) when (e is CategoryConflictException or CategoryValidationException or ExternalStorageException) { Response.StatusCode = e is CategoryConflictException ? 409 : 503; Message = "Chưa xác nhận khởi tạo demo. Giữ workspace, không reset dữ liệu; đối soát trước khi thử lại."; }
        return Page();
    }
}
