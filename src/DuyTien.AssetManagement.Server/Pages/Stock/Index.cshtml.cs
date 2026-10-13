using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Server.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Pages.Stock;

[Authorize]
public sealed class IndexModel(InventoryService service) : InventoryPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? ItemId { get; set; }
    [BindProperty(SupportsGet = true)] public string? LocationId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Unit { get; set; }
    [BindProperty, ModelBinder(BinderType = typeof(InvariantNullableDecimalBinder))] public decimal? Quantity { get; set; }
    [BindProperty, ModelBinder(BinderType = typeof(InvariantNullableDecimalBinder))] public decimal? Minimum { get; set; }
    [BindProperty] public DateTimeOffset? AsOf { get; set; }
    [BindProperty] public string? Source { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty(SupportsGet = true)] public long ExpectedVersion { get; set; }
    [BindProperty] public string? RequestKey { get; set; } = Guid.NewGuid().ToString("N");
    public StockList Result { get; private set; } = new([], 0, 1, 20, default);
    public InventoryChoices Choices { get; private set; } = new([], []);
    public bool CanUpdate { get; private set; }
    public StockBalance? Saved { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            if (!ModelState.IsValid) throw new InventoryValidationException("Input không hợp lệ.");
            Result = await service.StockAsync(Actor, new(Q, Page: PageNumber), ct);
            await LoadChoices(ct);
            if (!string.IsNullOrWhiteSpace(ItemId) && !string.IsNullOrWhiteSpace(LocationId) && !string.IsNullOrWhiteSpace(Unit) && ExpectedVersion > 0)
            {
                var selected = await service.StockDetailAsync(Actor, ItemId, LocationId, Unit, ct);
                Quantity = selected.Quantity; Minimum = selected.Minimum; AsOf = selected.AsOf; Source = selected.Source; ExpectedVersion = selected.Version;
                ModelState.Remove(nameof(ExpectedVersion)); // GET reads current version; POST error must keep the submitted one.
            }
            return Page();
        }
        catch (Exception e) when (InventoryWeb.Expected(e)) { return Failure(e); }
    }
    private async Task LoadChoices(CancellationToken ct)
    { try { Choices = await service.ChoicesAsync(Actor, true, ct); CanUpdate = true; } catch (InventoryForbiddenException) { CanUpdate = false; } }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; CanUpdate = true;
        try
        {
            if (!ModelState.IsValid) throw new InventoryValidationException("Input số/thời điểm không hợp lệ; giữ input để sửa.");
            Saved = await service.WriteStockAsync(Actor, new(ItemId ?? "", LocationId ?? "", Unit ?? "", Quantity, Minimum, AsOf ?? default, Source ?? "", ExpectedVersion, Reason ?? ""), RequestKey ?? "", ct);
            CanUpdate = false; return Page();
        }
        catch (Exception e) when (InventoryWeb.Expected(e))
        {
            try { await LoadChoices(ct); } catch (Exception load) when (InventoryWeb.Expected(load)) {/* Keep posted fields/key/version; option fallback carries selected values. */}
            return Failure(e);
        }
    }
}
