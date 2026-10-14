using System.Text.Json;
namespace DuyTien.AssetManagement.Application.Inventory;

public static class InventoryIntegrity
{
    public static void Apply(InventoryState s, InventoryOperation op)
    {
        if (s.Operations.Any(o => o.OperationId == op.OperationId || o.Actor == op.Actor && o.Key == op.Key)) throw new InventoryConflictException("Operation/key đã có.");
        if (op.Job is not null) { var i = s.Jobs.FindIndex(j => j.JobId == op.Job.JobId); if (i < 0) s.Jobs.Add(op.Job); else s.Jobs[i] = op.Job; }
        s.Plan.AddRange(op.Plan);
        var positions = s.Items.Select((r, i) => (r.ItemId, i)).ToDictionary(p => p.ItemId, p => p.i, StringComparer.Ordinal);
        foreach (var item in op.Items) { if (positions.TryGetValue(item.ItemId, out var i)) s.Items[i] = item; else { positions[item.ItemId] = s.Items.Count; s.Items.Add(item); } }
        if (op.Balance is not null) { var i = s.Balances.FindIndex(b => b.BalanceId == op.Balance.BalanceId); if (i < 0) s.Balances.Add(op.Balance); else s.Balances[i] = op.Balance; }
        s.Operations.Add(op);
    }
    public static void Validate(InventoryState s)
    {
        using var metric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure("journal-replay");
        if (s.Revision != s.Operations.Count || s.Revision < 0) throw new InventoryValidationException("Inventory revision/journal sai.");
        var items = new Dictionary<string, ItemRecord>(StringComparer.Ordinal); var jobs = new Dictionary<string, ItemImportJob>(StringComparer.Ordinal); var plans = new Dictionary<string, List<ItemImportPlanRow>>(StringComparer.Ordinal); var balances = new Dictionary<string, StockBalance>(StringComparer.Ordinal); var planIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); var planRows = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal); var keys = new HashSet<string>(StringComparer.Ordinal); var ops = new HashSet<string>(StringComparer.Ordinal);
        foreach (var op in s.Operations)
        {
            if (op is null || string.IsNullOrWhiteSpace(op.OperationId) || string.IsNullOrWhiteSpace(op.Actor) || string.IsNullOrWhiteSpace(op.Key) || op.RequestHash?.Length != 64 || string.IsNullOrWhiteSpace(op.Reason) || op.Plan is null || op.Items is null || !keys.Add(op.Actor + "\0" + op.Key) || !ops.Add(op.OperationId)) throw new InventoryValidationException("Inventory operation không hợp lệ.");
            var j = op.Job; ItemImportJob? before = null; if (j is not null) jobs.TryGetValue(j.JobId, out before);
            switch (op.Action)
            {
                case "IMPORT_START":
                    if (j is null || before is not null || j.Actor != op.Actor || j.RequestHash != op.RequestHash || j.Processed != 0 || j.State != "STAGING" || j.PlannedCount is < 1 or > 30000 || j.TotalSourceRows < j.PlannedCount || string.IsNullOrWhiteSpace(j.TreeVersionId) || j.DecisionsJson is null || j.DecisionsJson.Length > 40000 || op.Plan.Length != 0 || op.Items.Length != 0 || op.Balance is not null) throw new InventoryValidationException("Import start sai.");
                    plans[j.JobId] = []; planIds[j.JobId] = new(StringComparer.Ordinal); planRows[j.JobId] = []; break;
                case "IMPORT_STAGE":
                    if (j is null || before is null || before.State != "STAGING" || op.Plan.Length is < 1 or > InventoryService.ChunkSize || op.Items.Length != 0 || op.Balance is not null) throw new InventoryValidationException("Import staging sai.");
                    foreach (var p in op.Plan) { if (p is null || p.Row is null) throw new InventoryValidationException("Null plan row."); ValidItem(p.Row.Item, false); if (p.JobId != j.JobId || p.Row.SourceRow < 3 || p.Row.ExpectedVersion < 0 || p.Row.Action is not ("ADD" or "UPDATE" or "SKIP") || p.Row.Item.Version != p.Row.ExpectedVersion || !planIds[j.JobId].Add(p.Row.Item.ItemId) || !planRows[j.JobId].Add(p.Row.SourceRow)) throw new InventoryValidationException("Plan row sai/trùng."); plans[j.JobId].Add(p); }
                    if (j.Processed != 0 || plans[j.JobId].Count > j.PlannedCount || j.State != (plans[j.JobId].Count == j.PlannedCount ? "VALIDATED" : "STAGING")) throw new InventoryValidationException("Stage count/state sai."); break;
                case "IMPORT_CHUNK":
                    if (j is null || before is null || before.State is not ("VALIDATED" or "PARTIAL") || op.Plan.Length != 0 || op.Balance is not null) throw new InventoryValidationException("Import chunk sai.");
                    var remaining = plans[j.JobId].Skip(before.Processed); var selected = remaining.Take(InventoryService.NextChunkCount(remaining.Select(p => p.Row))).ToArray(); var wanted = new List<ItemRecord>();
                    foreach (var p in selected) { items.TryGetValue(p.Row.Item.ItemId, out var old); if ((old?.Version ?? 0) != p.Row.ExpectedVersion) throw new InventoryValidationException("Chunk stale trong journal."); if (p.Row.Action != "SKIP") wanted.Add(p.Row.Item with { Version = p.Row.ExpectedVersion + 1 }); }
                    if (selected.Length == 0 || j.Processed != before.Processed + selected.Length || j.State != (j.Processed == j.PlannedCount ? "COMPLETE" : "PARTIAL") || !Same(op.Items, wanted)) throw new InventoryValidationException("Chunk progress/materialized sai."); foreach (var item in op.Items) { ValidItem(item, true); items[item.ItemId] = item; }
                    break;
                case "ITEM_RECLASSIFY":
                    if (j is not null || op.Plan.Length != 0 || op.Balance is not null || op.Items.Length == 0 || op.Items.Select(i => i.ItemId).Distinct(StringComparer.Ordinal).Count() != op.Items.Length) throw new InventoryValidationException("Reclassification journal sai.");
                    foreach (var moved in op.Items)
                    {
                        ValidItem(moved, true);
                        if (!items.TryGetValue(moved.ItemId, out var prior) || prior.State != "DRAFT_DEMO" || moved.CategoryId == prior.CategoryId || moved.Version != prior.Version + 1 ||
                            !Same(prior with { CategoryId = moved.CategoryId, Version = moved.Version }, moved)) throw new InventoryValidationException("Không đổi source/identity/cells/tracking/state qua reclassification.");
                        items[moved.ItemId] = moved;
                    }
                    break;
                case "STOCK_UPDATE":
                    var b = op.Balance; if (j is not null || op.Items.Length != 0 || op.Plan.Length != 0 || b is null || !items.TryGetValue(b.ItemId, out var owner) || owner.StockTracked != true || owner.State != "DRAFT_DEMO" || b.Unit != owner.PrimaryUnit || string.IsNullOrWhiteSpace(b.LocationId) || b.Quantity < 0 || b.Minimum < 0 || b.AsOf.Offset != TimeSpan.Zero || b.AsOf == default || b.AsOf > op.At || string.IsNullOrWhiteSpace(b.Source) || b.UpdatedBy != op.Actor || b.State != "DRAFT_DEMO") throw new InventoryValidationException("Stock journal sai."); balances.TryGetValue(b.BalanceId, out var oldBalance); if (b.Version != (oldBalance?.Version ?? 0) + 1 || oldBalance is not null && (b.ItemId != oldBalance.ItemId || b.LocationId != oldBalance.LocationId || b.Unit != oldBalance.Unit || b.AsOf < oldBalance.AsOf) || balances.Values.Any(x => x.BalanceId != b.BalanceId && x.ItemId == b.ItemId && x.LocationId == b.LocationId && x.Unit == b.Unit)) throw new InventoryValidationException("Stock identity/version sai."); balances[b.BalanceId] = b; break;
                default: throw new InventoryValidationException("Inventory action chưa hỗ trợ.");
            }
            if (j is not null)
            { if (before is not null && (j.Actor != before.Actor || j.RequestHash != before.RequestHash || j.TreeVersionId != before.TreeVersionId || j.Reason != before.Reason || j.DecisionsJson != before.DecisionsJson || j.PlannedCount != before.PlannedCount || j.TotalSourceRows != before.TotalSourceRows || j.CreatedAt != before.CreatedAt)) throw new InventoryValidationException("Job metadata bất biến bị đổi."); jobs[j.JobId] = j; }
        }
        if (items.Values.GroupBy(i => i.Namespace + "\0" + i.SourceIdentity, StringComparer.Ordinal).Any(g => g.Count() > 1) || !Same(items.Values.OrderBy(i => i.ItemId, StringComparer.Ordinal), s.Items.OrderBy(i => i.ItemId, StringComparer.Ordinal)) || !Same(jobs.Values.OrderBy(j => j.JobId, StringComparer.Ordinal), s.Jobs.OrderBy(j => j.JobId, StringComparer.Ordinal)) || !Same(balances.Values.OrderBy(b => b.BalanceId, StringComparer.Ordinal), s.Balances.OrderBy(b => b.BalanceId, StringComparer.Ordinal)) || !Same(plans.Values.SelectMany(p => p).OrderBy(p => p.JobId, StringComparer.Ordinal).ThenBy(p => p.Row.SourceRow), s.Plan.OrderBy(p => p.JobId, StringComparer.Ordinal).ThenBy(p => p.Row.SourceRow))) throw new InventoryValidationException("Inventory materialized/journal mismatch.");
    }
    private static bool Same(object? a, object? b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
    private static void ValidItem(ItemRecord i, bool committed)
    { if (i is null || string.IsNullOrWhiteSpace(i.ItemId) || i.Namespace != "MISA_VTHH" || string.IsNullOrWhiteSpace(i.SourceIdentity) || i.CategoryId is null || i.Cells is null || i.Cells.Length != 20 || i.Cells.Any(c => c is null) || string.IsNullOrWhiteSpace(i.SourceCode) || i.Version < (committed ? 1 : 0) || i.State != (i.CategoryId.Length == 0 ? "QUARANTINED" : "DRAFT_DEMO")) throw new InventoryValidationException("Item không hợp lệ."); }
}
