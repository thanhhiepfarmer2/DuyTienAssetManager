using DuyTien.AssetManagement.Application.Authentication;
namespace DuyTien.AssetManagement.Application.Inventory;

public sealed partial class InventoryService
{
    public Task<ItemList> ListAsync(string actor, InventoryQuery query, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    { Page(query.Page, query.PageSize); if (query.Q?.Length > 200) throw new InventoryValidationException("Từ khóa<=200ký tự."); var c = await Load(actor, "ITEMS_READ", t); var rows = c.State.Items.Where(i => CanItem(c, actor, "ITEMS_READ", i)).Where(i => query.CategoryId is null || i.CategoryId == query.CategoryId).Where(i => query.Q is null || (i.SourceCode + " " + i.Name + " " + i.Cells[4]).Contains(query.Q, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.SourceCode, StringComparer.Ordinal).ToArray(); return new ItemList(rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(), rows.Length, query.Page, query.PageSize, clock.GetUtcNow()); }, ct);
    public Task<ItemRecord> DetailAsync(string actor, string id, CancellationToken ct = default) => writer.ExecuteAsync(async t => { var c = await Load(actor, "ITEMS_READ", t); return c.State.Items.SingleOrDefault(i => i.ItemId == id && CanItem(c, actor, "ITEMS_READ", i)) ?? throw new InventoryNotFoundException(); }, ct);
    private bool CanStock(Context c, string actor, string permission, StockBalance b)
    { var item = c.State.Items.SingleOrDefault(i => i.ItemId == b.ItemId); var loc = c.References.Locations.SingleOrDefault(l => l.Id == b.LocationId && l.IsActive); return item is not null && item.StockTracked == true && CanItem(c, actor, "ITEMS_READ", item) && loc is not null && c.References.OrgUnits.Any(o => o.Id == loc.OrgUnitId && o.IsActive) && authorization.IsAllowed(c.Auth, actor, permission, ResourceScope.Required(loc.OrgUnitId, loc.Id, item.CategoryId), c.Hierarchies, clock.GetUtcNow()); }
    public Task<StockList> StockAsync(string actor, InventoryQuery query, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    { Page(query.Page, query.PageSize); if (query.Q?.Length > 200) throw new InventoryValidationException("Từ khóa<=200ký tự."); var c = await Load(actor, "STOCK_READ", t); var rows = c.State.Balances.Where(b => CanStock(c, actor, "STOCK_READ", b)).Where(b => query.CategoryId is null || c.State.Items.Single(i => i.ItemId == b.ItemId).CategoryId == query.CategoryId).Where(b => query.Q is null || (b.ItemId + " " + b.LocationId + " " + b.Source).Contains(query.Q, StringComparison.OrdinalIgnoreCase)).OrderBy(b => b.ItemId, StringComparer.Ordinal).ThenBy(b => b.LocationId, StringComparer.Ordinal).ToArray(); return new StockList(rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(), rows.Length, query.Page, query.PageSize, clock.GetUtcNow()); }, ct);
    public Task<StockBalance> StockDetailAsync(string actor, string itemId, string locationId, string unit, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    {
        var c = await Load(actor, "STOCK_READ", t);
        return c.State.Balances.SingleOrDefault(b => b.ItemId == itemId && b.LocationId == locationId && b.Unit == unit && CanStock(c, actor, "STOCK_READ", b)) ?? throw new InventoryNotFoundException();
    }, ct);
    public Task<StockBalance> WriteStockAsync(string actor, StockWriteRequest r, string key, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    {
        Key(key); var c = await Load(actor, "STOCK_UPDATE", t); if (r is null || r.ExpectedVersion < 0 || string.IsNullOrWhiteSpace(r.Source) || r.Source.Length > 500 || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000 || r.Quantity < 0 || r.Minimum < 0 || r.AsOf.Offset != TimeSpan.Zero || r.AsOf == default || r.AsOf > clock.GetUtcNow()) throw new InventoryValidationException("Snapshot cần source/reason, asOf UTC không tương lai; quantity/minimum>=0 hoặc null.");
        var item = c.State.Items.SingleOrDefault(i => i.ItemId == r.ItemId && CanItem(c, actor, "ITEMS_READ", i)) ?? throw new InventoryNotFoundException();
        if (item.StockTracked != true || item.Cells[2] == "Dịch vụ" || r.Unit != item.PrimaryUnit || string.IsNullOrWhiteSpace(r.Unit)) throw new InventoryValidationException("Cần tracking xác nhận và đơn vị chính exact; không cộng/đổi đơn vị ngầm.");
        var old = c.State.Balances.SingleOrDefault(b => b.ItemId == r.ItemId && b.LocationId == r.LocationId && b.Unit == r.Unit);
        var b = new StockBalance(old?.BalanceId ?? "BAL-" + Guid.NewGuid().ToString("N"), r.ItemId, r.LocationId, r.Unit, r.Quantity, r.Minimum, r.AsOf, r.Source, actor, (old?.Version ?? 0) + 1);
        if (!CanStock(c, actor, "STOCK_UPDATE", b)) throw new InventoryNotFoundException(); var hash = Hash(r); var prior = c.State.Operations.SingleOrDefault(o => o.Actor == actor && o.Key == key);
        if (prior is not null) { if (prior.Action != "STOCK_UPDATE" || prior.RequestHash != hash) throw new InventoryConflictException("Key payload khác."); return prior.Balance!; }
        if ((old?.Version ?? 0) != r.ExpectedVersion || old is not null && r.AsOf < old.AsOf) throw new InventoryConflictException("Version/asOf stale; giữ snapshot đã lưu.");
        await Save(c, new("INV-" + Guid.NewGuid().ToString("N"), key, actor, hash, "STOCK_UPDATE", r.Reason, clock.GetUtcNow(), null, [], [], b), t); return b;
    }, ct);
    public Task<InventoryChoices> ChoicesAsync(string actor, bool stock = false, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    { var permission = stock ? "STOCK_UPDATE" : "ITEMS_IMPORT"; var c = await Load(actor, permission, t); bool Can(ResourceScope s) => authorization.IsAllowed(c.Auth, actor, permission, s, c.Hierarchies, clock.GetUtcNow()); return new InventoryChoices(c.Tree.ActiveNodes.Where(n => n.State == "DRAFT" && Can(CategoryScope(n.CategoryId))).Select(n => new InventoryChoice(n.CategoryId, n.Name + " [DRAFT]")).ToArray(), c.References.Locations.Where(l => l.IsActive && c.References.OrgUnits.Any(o => o.Id == l.OrgUnitId && o.IsActive) && Can(new(new(true, l.OrgUnitId), new(true, l.Id), new(false, null)))).Select(l => new InventoryChoice(l.Id, l.Name)).ToArray()); }, ct);
}
