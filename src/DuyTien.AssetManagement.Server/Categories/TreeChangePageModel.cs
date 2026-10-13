using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace DuyTien.AssetManagement.Server.Categories;
public sealed class TreeChangeForm
{
    public string Kind { get; set; } = "EDIT_CATEGORY";
    public string? Reason { get; set; }
    public string? CategoryId { get; set; }
    public string? DestinationCategoryId { get; set; }
    public string? Name { get; set; }
    public string? SegmentCode { get; set; }
    public string? Definition { get; set; }
    public string DeletePlan { get; set; } = "EMPTY_ONLY";
    public string[]? AssetIds { get; set; }
    public string? DestinationOrgUnitId { get; set; }
    public string? DestinationLocationId { get; set; }
    public TreeMutationRequest Request() => new(Kind, Reason ?? "", Empty(CategoryId), Empty(DestinationCategoryId), Name, SegmentCode, Definition, DeletePlan, AssetIds, Empty(DestinationOrgUnitId), Empty(DestinationLocationId));
    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
public abstract class TreeChangePageModel(TreeMutationService service, CategoryCatalogService catalog, AssetCrudService assets, bool assetMove) : PageModel
{
    public bool AssetMove { get; } = assetMove;
    [BindProperty] public TreeChangeForm Form { get; set; } = new();
    [BindProperty] public string? RequestKey { get; set; }
    [BindProperty] public string? PreviewHash { get; set; }
    [BindProperty] public long ExpectedTreeRevision { get; set; }
    [BindProperty] public long ExpectedAssetRevision { get; set; }
    [BindProperty] public bool ConfirmImpact { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public TreeMutationPreview? Preview { get; private set; }
    public TreeMutationResult? Result { get; private set; }
    public string? Error { get; private set; }
    public CategoryList Choices { get; private set; } = new(0, null, 0, []);
    public AssetList AssetChoices { get; private set; } = new([], 0, 1, 100, default);
    public AssetFormChoices ScopeChoices { get; private set; } = new(null, [], [], []);
    public async Task<IActionResult> OnGetAsync(string? id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; RequestKey = Guid.NewGuid().ToString("N");
        try
        {
            Form.Kind = AssetMove ? "MOVE_ASSETS" : "EDIT_CATEGORY";
            await Load(ct);
            if (id is not null && !AssetMove) { var n = await catalog.DetailAsync(CategoryWeb.Actor(User), id, ct); Form.CategoryId = id; Form.Name = n.Name; Form.SegmentCode = n.SegmentCode; Form.Definition = n.Definition; }
            if (id is not null && AssetMove) { _ = await assets.DetailAsync(CategoryWeb.Actor(User), id, ct); Form.AssetIds = [id]; }
            return Page();
        }
        catch (CategoryNotFoundException) { return NotFound(); }
        catch (CategoryForbiddenException) { return Forbid(); }
        catch (AssetForbiddenException) { return Forbid(); }
        catch (AssetNotFoundException) { return NotFound(); }
        catch (Exception e) when (Expected(e)) { Error = Message(e); Response.StatusCode = Status(e); return Page(); }
    }
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            ValidatePage(); if (!ModelState.IsValid) throw new CategoryValidationException("Dữ liệu form không hợp lệ.");
            Preview = await service.PreviewAsync(CategoryWeb.Actor(User), Form.Request(), ct);
            PreviewHash = Preview.PreviewHash; ExpectedTreeRevision = Preview.TreeRevision; ExpectedAssetRevision = Preview.AssetRevision;
            RequestKey = Guid.NewGuid().ToString("N"); ConfirmImpact = false;
            foreach (var key in new[] { nameof(RequestKey), nameof(PreviewHash), nameof(ExpectedTreeRevision), nameof(ExpectedAssetRevision), nameof(ConfirmImpact) }) ModelState.Remove(key);
            await Load(ct); return Page();
        }
        catch (CategoryNotFoundException) { return NotFound(); }
        catch (CategoryForbiddenException) { return Forbid(); }
        catch (Exception e) when (Expected(e)) { return await Failure(e, ct); }
    }
    public async Task<IActionResult> OnPostCommitAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            ValidatePage(); if (!ModelState.IsValid) throw new CategoryValidationException("Dữ liệu form/version không hợp lệ.");
            Result = await service.CommitAsync(CategoryWeb.Actor(User), Form.Request(), ExpectedTreeRevision, ExpectedAssetRevision, PreviewHash ?? "", RequestKey ?? "", ConfirmImpact, ct);
            await Load(ct); return Page();
        }
        catch (CategoryNotFoundException) { return NotFound(); }
        catch (CategoryForbiddenException) { return Forbid(); }
        catch (Exception e) when (Expected(e)) { return await Failure(e, ct); }
    }
    private void ValidatePage()
    { if (AssetMove != (Form.Kind == "MOVE_ASSETS")) throw new CategoryValidationException("Thao tác không thuộc trang này."); }
    private async Task Load(CancellationToken ct)
    {
        Choices = await catalog.ListAsync(CategoryWeb.Actor(User), ct: ct);
        if (AssetMove) { AssetChoices = await assets.ListAsync(CategoryWeb.Actor(User), new(Q: Q, PageSize: 100), ct); ScopeChoices = await assets.ChoicesAsync(CategoryWeb.Actor(User), true, ct); }
    }
    private async Task<IActionResult> Failure(Exception e, CancellationToken ct)
    {
        Error = Result is null ? Message(e) : "Commit đã được xác nhận nhưng chưa tải được màn hình mới. Tải lại để xem snapshot hiện tại."; Response.StatusCode = Result is null ? Status(e) : 200;
        try { await Load(ct); }
        catch (Exception read) when (Expected(read)) { Error += " Dữ liệu chọn chưa tải được; giữ form và tạo preview lại khi kết nối ổn định."; }
        return Page();
    }
    private static bool Expected(Exception e) => CategoryWeb.IsExpected(e) || e is AssetValidationException or AssetForbiddenException or AssetNotFoundException or AssetConflictException;
    private static int Status(Exception e) => e switch { AssetForbiddenException => 403, AssetNotFoundException => 404, AssetConflictException => 409, AssetValidationException => 400, _ => CategoryWeb.Status(e) };
    private static string Message(Exception e) => CategoryWeb.IsExpected(e) ? CategoryWeb.Message(e) : e.Message;
}
