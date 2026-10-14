using System.Globalization;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.ExternalStorage;
namespace DuyTien.AssetManagement.Application.Assets;

public static class AssetCatalogIntegrity
{
    private static bool Equal(object? a, object? b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void ValidateReferences(AssetCatalogState s)
    {
        _ = ScopeHierarchy.Create(AuthScopeType.OrgUnit, s.OrgUnits.Select(n => new ScopeNode(n.Id, n.ParentId)));
        _ = ScopeHierarchy.Create(AuthScopeType.Location, s.Locations.Select(n => new ScopeNode(n.Id, n.ParentId)));
        if (s.Locations.Any(l => !s.OrgUnits.Any(o => o.Id == l.OrgUnitId)) || s.Rules.Select(r => r.RuleId).Distinct(StringComparer.Ordinal).Count() != s.Rules.Count || s.Rules.Any(r => string.IsNullOrWhiteSpace(r.RuleId) || r.Status is not ("DEMO" or "PROPOSED" or "RETIRED"))) throw new ExternalStorageException("Reference/rule tài sản không hợp lệ.");
    }
    public static void Validate(AssetCatalogState s)
    {
        using var metric = DuyTien.AssetManagement.Application.Diagnostics.PerformanceTrace.Measure("journal-replay");
        if (s.Revision != s.Operations.Count || s.Operations.Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count() != s.Operations.Count || s.Operations.Select(o => (o.Actor, o.IdempotencyKey)).Distinct().Count() != s.Operations.Count) throw new ExternalStorageException("Ledger tài sản không hợp lệ.");
        var assets = new Dictionary<string, AssetRecord>(StringComparer.Ordinal); var aliases = new Dictionary<string, AssetAlias>(StringComparer.Ordinal); var counters = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var op in s.Operations)
        {
            if (op.AliasesAfter.Any(a => a is null)) throw new ExternalStorageException("Alias snapshot không hợp lệ.");
            var a = op.Result; var before = assets.GetValueOrDefault(a.AssetId);
            _ = AssetCrudService.ValidateInput(new(a.Name, a.CategoryId, a.OrgUnitId, a.LocationId, a.Type, a.CustodianRef, a.Serial, a.Model, a.Manufacturer, a.RecordedOn, a.Condition, a.Notes, op.AliasesAfter.Where(x => x.IsActive).Select(x => x.OldCode).ToArray()));
            if (op.Before != before || a.DataState != "DRAFT_DEMO" || a.Type != "MACHINE" || a.Version != (before?.Version ?? 0) + 1 || a.UpdatedBy != op.Actor || a.UpdatedAt != op.CompletedAt || string.IsNullOrWhiteSpace(op.IdempotencyKey) || string.IsNullOrWhiteSpace(op.RequestHash)) throw new ExternalStorageException("Version/history tài sản không hợp lệ.");
            if (before is null)
            {
                if (op.Action != "CREATE" || a.IsDeleted || a.CreatedBy != op.Actor || a.CreatedAt != op.CompletedAt || !op.CounterKey.EndsWith("-" + a.IssuedCcc, StringComparison.Ordinal) || op.CounterValue <= counters.GetValueOrDefault(op.CounterKey) || op.CounterValue > 999999 || a.ObjectCode != op.CounterKey + "-" + op.CounterValue.ToString("D6", CultureInfo.InvariantCulture)) throw new ExternalStorageException("Counter cấp mã không hợp lệ.");
                counters[op.CounterKey] = op.CounterValue;
            }
            else
            {
                if (a.ObjectCode != before.ObjectCode || a.IssuedCcc != before.IssuedCcc || a.CodeRuleId != before.CodeRuleId || a.CreatedAt != before.CreatedAt || a.CreatedBy != before.CreatedBy || op.CounterKey != "" || op.CounterValue != 0 ||
                    (op.Action == "MOVE" ? a.IsDeleted != before.IsDeleted || a != (before with { CategoryId = a.CategoryId, OrgUnitId = a.OrgUnitId, LocationId = a.LocationId, UpdatedBy = a.UpdatedBy, UpdatedAt = a.UpdatedAt, Version = a.Version }) : op.Action == "UPDATE" ? before.IsDeleted || a.IsDeleted : op.Action == "DELETE" ? before.IsDeleted || !a.IsDeleted : op.Action == "RESTORE" ? !before.IsDeleted || a.IsDeleted : true)) throw new ExternalStorageException("ID/code/lifecycle không bất biến.");
            }
            foreach (var prior in aliases.Values.Where(x => x.AssetId == a.AssetId).ToArray())
                if (!op.AliasesAfter.Any(x => x.AliasId == prior.AliasId && x.OldCode == prior.OldCode && x.AssetId == prior.AssetId)) throw new ExternalStorageException("Không được mất mapping alias lịch sử.");
            foreach (var alias in op.AliasesAfter)
            {
                if (alias.AssetId != a.AssetId || alias.Namespace != "DEMO" || string.IsNullOrWhiteSpace(alias.OldCode) || aliases.Values.Any(x => string.Equals(x.OldCode, alias.OldCode, StringComparison.OrdinalIgnoreCase) && x.AssetId != alias.AssetId)) throw new ExternalStorageException("Alias mapping không unique.");
                aliases[alias.AliasId] = alias;
            }
            assets[a.AssetId] = a;
        }
        if (aliases.Values.Select(a => a.OldCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != aliases.Count || aliases.Values.Any(x => assets.Values.Any(a => a.AssetId != x.AssetId && string.Equals(a.ObjectCode, x.OldCode, StringComparison.OrdinalIgnoreCase)))) throw new ExternalStorageException("Alias/mã tài sản không unique.");
        if (assets.Values.Select(a => a.ObjectCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != assets.Count || !Equal(assets.Values.OrderBy(a => a.AssetId, StringComparer.Ordinal), s.Assets.OrderBy(a => a.AssetId, StringComparer.Ordinal)) ||
            !Equal(aliases.Values.OrderBy(a => a.AliasId, StringComparer.Ordinal), s.Aliases.OrderBy(a => a.AliasId, StringComparer.Ordinal)) ||
            !Equal(counters.Select(p => new AssetCounter(p.Key, p.Value)).OrderBy(c => c.Key, StringComparer.Ordinal), s.Counters.OrderBy(c => c.Key, StringComparer.Ordinal))) throw new ExternalStorageException("Trạng thái tài sản khác journal.");
    }
}
