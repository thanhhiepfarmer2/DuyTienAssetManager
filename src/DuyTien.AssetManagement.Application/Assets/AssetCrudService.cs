using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
namespace DuyTien.AssetManagement.Application.Assets;

public sealed class AssetCrudService(IAssetCatalogStore store, ICategoryCatalogStore categories,
    IAuthSystemStore authStore, ISingleWriterCoordinator writer, TimeProvider clock)
{
    private readonly ScopeAuthorizationService authorization = new();
    public async Task<AssetList> ListAsync(string actor, AssetQuery query, CancellationToken ct = default) => (await BrowseAsync(actor, query, ct)).List;
    public Task<(AssetList List, AssetFormChoices Filters)> BrowseAsync(string actor, AssetQuery query, CancellationToken ct = default) => writer.ReadAsync(t => BrowseCore(actor, query, t), ct);
    private async Task<(AssetList List, AssetFormChoices Filters)> BrowseCore(string actor, AssetQuery query, CancellationToken ct)
    {
        ValidateQuery(query);
        var c = await Load(actor, "ASSETS_READ", ct, combinedRead: true);
        var visible = c.State.Assets.Where(a => Allowed(c, actor, "ASSETS_READ", a));
        if (!query.IncludeDeleted) visible = visible.Where(a => !a.IsDeleted);
        if (!string.IsNullOrEmpty(query.CategoryId))
        {
            var categoryScope = new ResourceScope(new(false, null), new(false, null), new(true, query.CategoryId));
            if (!c.Nodes.ContainsKey(query.CategoryId) || !authorization.IsAllowed(c.Auth, actor, "ASSETS_READ", categoryScope, c.Hierarchies, clock.GetUtcNow())) throw new AssetNotFoundException();
            visible = visible.Where(a => c.Hierarchies.Categories.IsWithin(query.CategoryId, a.CategoryId, query.IncludeDescendants));
        }
        if (!string.IsNullOrEmpty(query.OrgUnitId)) visible = visible.Where(a => a.OrgUnitId == query.OrgUnitId);
        if (!string.IsNullOrEmpty(query.LocationId)) visible = visible.Where(a => a.LocationId == query.LocationId);
        if (!string.IsNullOrEmpty(query.Condition)) visible = visible.Where(a => a.Condition == query.Condition);
        if (!string.IsNullOrEmpty(query.CustodianRef)) visible = visible.Where(a => a.CustodianRef == query.CustodianRef);
        var q = Normalize(query.Q ?? "");
        if (q.Length > 0) visible = visible.Where(a => Normalize(a.ObjectCode + " " + a.Name + " " + a.Serial + " " + string.Join(" ", c.State.Aliases.Where(x => x.AssetId == a.AssetId).Select(x => x.OldCode))).Contains(q, StringComparison.Ordinal));
        var rows = visible.OrderBy(a => a.ObjectCode, StringComparer.Ordinal).ToArray();
        return (new(rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(a => View(c, a)).ToArray(), rows.Length, query.Page, query.PageSize, clock.GetUtcNow()), Choices(c, actor, "ASSETS_READ"));
    }
    public Task<AssetView> DetailAsync(string actor, string id, CancellationToken ct = default) => writer.ReadAsync(t => DetailCore(actor, id, t), ct);
    private async Task<AssetView> DetailCore(string actor, string id, CancellationToken ct)
    {
        var c = await Load(actor, "ASSETS_READ", ct, combinedRead: true);
        return View(c, Find(c, actor, "ASSETS_READ", id));
    }
    public Task<(AssetView Item, AssetFormChoices? Choices)> DetailWithChoicesAsync(string actor, string id, bool editing, CancellationToken ct = default) => writer.ReadAsync(async token =>
    {
        var c = await Load(actor, "ASSETS_READ", token, combinedRead: true);
        var item = View(c, Find(c, actor, "ASSETS_READ", id));
        if (!editing) return (item, (AssetFormChoices?)null);
        RequirePermission(c.Auth, actor, "ASSETS_UPDATE");
        return (item, Choices(c, actor, "ASSETS_UPDATE"));
    }, ct);
    public Task<AssetFormChoices> ChoicesAsync(string actor, bool editing = false, CancellationToken ct = default) => writer.ReadAsync(t => ChoicesCore(actor, editing, t), ct);
    private async Task<AssetFormChoices> ChoicesCore(string actor, bool editing, CancellationToken ct)
    {
        var permission = editing ? "ASSETS_UPDATE" : "ASSETS_CREATE";
        var c = await Load(actor, permission, ct, combinedRead: true);
        return Choices(c, actor, permission);
    }
    private AssetFormChoices Choices(Context c, string actor, string permission)
    {
        bool Can(ResourceScope scope) => authorization.IsAllowed(c.Auth, actor, permission, scope, c.Hierarchies, clock.GetUtcNow());
        return new(c.Tree.ActiveTreeVersionId,
            c.Nodes.Values.Where(n => n.State == "DRAFT" && Can(new(new(false, null), new(false, null), new(true, n.CategoryId))))
                .OrderBy(n => n.Name, StringComparer.Ordinal).Select(n => new AssetChoice(n.CategoryId, n.Name + " [" + n.SegmentCode + " / DRAFT]")).ToArray(),
            c.State.OrgUnits.Where(n => n.IsActive && Can(new(new(true, n.Id), new(false, null), new(false, null))))
                .Select(n => new AssetChoice(n.Id, n.Name)).ToArray(),
            c.State.Locations.Where(n => n.IsActive && c.State.OrgUnits.Any(o => o.Id == n.OrgUnitId && o.IsActive) && Can(new(new(true, n.OrgUnitId), new(true, n.Id), new(false, null))))
                .Select(n => new AssetChoice(n.Id, n.Name + " — " + c.State.OrgUnits.Single(o => o.Id == n.OrgUnitId).Name)).ToArray());
    }
    public Task<AssetWriteResult> CreateAsync(string actor, AssetWriteRequest request, string key, CancellationToken ct = default) =>
        Write(actor, null, request, null, "CREATE", key, ct);
    public Task<AssetWriteResult> UpdateAsync(string actor, string id, AssetWriteRequest request, string key, CancellationToken ct = default) =>
        Write(actor, id, request, null, "UPDATE", key, ct);
    public Task<AssetWriteResult> DeleteAsync(string actor, string id, AssetLifecycleRequest request, string key, CancellationToken ct = default) =>
        Write(actor, id, null, request, "DELETE", key, ct);
    public Task<AssetWriteResult> RestoreAsync(string actor, string id, AssetLifecycleRequest request, string key, CancellationToken ct = default) =>
        Write(actor, id, null, request, "RESTORE", key, ct);
    private Task<AssetWriteResult> Write(string actor, string? id, AssetWriteRequest? request,
        AssetLifecycleRequest? lifecycle, string action, string key, CancellationToken ct) => writer.ExecuteAsync(async token =>
    {
        var permission = "ASSETS_" + action;
        var c = await Load(actor, permission, token);
        Text(key, "IdempotencyKey", true, 100);
        var input = request is null ? null : ValidateInput(request.Asset);
        if (lifecycle is not null) Text(lifecycle.Reason, "Reason", true, 500);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { action, id, input, ExpectedVersion = request?.ExpectedVersion ?? lifecycle!.ExpectedVersion, request?.TreeVersionId, lifecycle?.Reason })));
        var replay = c.State.Operations.SingleOrDefault(o => o.Actor == actor && o.IdempotencyKey == key);
        if (replay is not null)
        {
            if (replay.RequestHash != hash) throw new AssetConflictException("Idempotency key đã dùng cho dữ liệu khác.");
            Find(c, actor, permission, replay.Result.AssetId);
            if (!Allowed(c, actor, permission, replay.Result)) throw new AssetNotFoundException();
            return new AssetWriteResult(replay.Result, true);
        }
        var before = id is null ? null : Find(c, actor, permission, id);
        var expected = request?.ExpectedVersion ?? lifecycle!.ExpectedVersion;
        if (expected != (before?.Version ?? 0)) throw new AssetConflictException("Bản tài sản đã thay đổi. Tải lại dữ liệu trước khi lưu; không tự ghi đè.");
        if (action == "UPDATE" && before!.IsDeleted) throw new AssetConflictException("Tài sản đã xóa mềm; cần khôi phục trước khi sửa.");
        if (action == "DELETE" && before!.IsDeleted || action == "RESTORE" && !before!.IsDeleted)
            throw new AssetConflictException("Trạng thái tài sản đã thay đổi; tải lại trang.");
        if (request is not null)
        {
            if (request.TreeVersionId != c.Tree.ActiveTreeVersionId) throw new AssetConflictException("Cây DRAFT đã thay đổi; tải lại danh mục trước khi lưu.");
            ValidateDestination(c, input!);
            var destination = ResourceScope.Required(input!.OrgUnitId, input.LocationId, input.CategoryId);
            if (!authorization.IsAllowed(c.Auth, actor, permission, destination, c.Hierarchies, clock.GetUtcNow())) throw new AssetNotFoundException();
            if (before is not null && (before.OrgUnitId != input.OrgUnitId || before.LocationId != input.LocationId || before.CategoryId != input.CategoryId) &&
                !authorization.IsMoveAllowed(c.Auth, actor, "ASSETS_MOVE", Scope(before), destination, c.Hierarchies, clock.GetUtcNow())) throw new AssetNotFoundException();
        }
        var candidate = c.State.Clone(); var now = clock.GetUtcNow(); string counterKey = ""; long counterValue = 0;
        AssetRecord after;
        if (before is null)
        {
            var root = c.Nodes[input!.CategoryId];
            while (root.ParentId is not null) root = c.Nodes[root.ParentId];
            var rules = c.State.Rules.Where(r => r.CategoryRootId == root.CategoryId && r.ApprovedForDemo && r.Status == "DEMO").ToArray();
            if (rules.Length != 1) throw new AssetValidationException("Chưa có đúng một rule cấp mã demo được xác lập cho nhóm này. Không tự cấp mã từ DRAFT.", "CategoryId");
            var rule = rules[0];
            if (!Regex.IsMatch(rule.Prefix, "^[A-Z][A-Z0-9]{0,11}$", RegexOptions.CultureInvariant) || !Regex.IsMatch(rule.Ccc, "^[A-Z][A-Z0-9]{2}$", RegexOptions.CultureInvariant) || rule.Ccc != root.SegmentCode || rule.Version < 1)
                throw new AssetValidationException("Rule demo không hợp lệ; chưa cấp mã.", "CategoryId");
            counterKey = rule.Prefix + "-" + rule.Ccc;
            counterValue = c.State.Counters.SingleOrDefault(x => x.Key == counterKey)?.LastAllocated ?? 0;
            string code;
            do
            {
                if (counterValue >= 999999) throw new AssetConflictException("Bộ đếm sáu chữ số đã hết; không tái sử dụng mã.");
                counterValue++; code = counterKey + "-" + counterValue.ToString("D6", CultureInfo.InvariantCulture);
            } while (c.State.Assets.Any(a => string.Equals(a.ObjectCode, code, StringComparison.OrdinalIgnoreCase)) || c.State.Aliases.Any(a => string.Equals(a.OldCode, code, StringComparison.OrdinalIgnoreCase)));
            candidate.Counters.RemoveAll(x => x.Key == counterKey); candidate.Counters.Add(new(counterKey, counterValue));
            after = new("AST-" + Guid.NewGuid().ToString("N"), code, rule.Ccc, rule.RuleId, input.CategoryId,
                input.Name, input.Type, input.OrgUnitId, input.LocationId, input.CustodianRef, input.Serial, input.Model,
                input.Manufacturer, input.RecordedOn, input.Condition, input.Notes, "DRAFT_DEMO", false, now, actor, now, actor, 1);
        }
        else if (input is not null)
            after = before with
            {
                CategoryId = input.CategoryId,
                Name = input.Name,
                OrgUnitId = input.OrgUnitId,
                LocationId = input.LocationId,
                CustodianRef = input.CustodianRef,
                Serial = input.Serial,
                Model = input.Model,
                Manufacturer = input.Manufacturer,
                RecordedOn = input.RecordedOn,
                Condition = input.Condition,
                Notes = input.Notes,
                UpdatedAt = now,
                UpdatedBy = actor,
                Version = before.Version + 1
            };
        else after = before with { IsDeleted = action == "DELETE", UpdatedAt = now, UpdatedBy = actor, Version = before.Version + 1 };
        candidate.Assets.RemoveAll(a => a.AssetId == after.AssetId); candidate.Assets.Add(after);
        if (input is not null) SetAliases(candidate, after.AssetId, input.Aliases!);
        var aliases = candidate.Aliases.Where(a => a.AssetId == after.AssetId).OrderBy(a => a.AliasId, StringComparer.Ordinal).ToArray();
        candidate.Operations.Add(new("ASSETOP-" + Guid.NewGuid().ToString("N"), key, actor, hash, action, before, after, aliases,
            counterKey, counterValue, lifecycle?.Reason.Trim() ?? "CRUD demo DRAFT", now));
        candidate.Revision++;
        await store.CommitAsync(candidate, c.State.Revision, token);
        return new AssetWriteResult(after, false);
    }, ct);
    private static void SetAliases(AssetCatalogState state, string id, string[] requested)
    {
        foreach (var code in requested)
            if (state.Aliases.Any(a => a.AssetId != id && string.Equals(a.OldCode, code, StringComparison.OrdinalIgnoreCase)) || state.Assets.Any(a => a.AssetId != id && string.Equals(a.ObjectCode, code, StringComparison.OrdinalIgnoreCase)))
                throw new AssetConflictException("Alias đã thuộc tài sản khác; không tự ghi đè mapping.");
        for (var i = 0; i < state.Aliases.Count; i++)
        {
            var a = state.Aliases[i];
            if (a.AssetId == id) state.Aliases[i] = a with { IsActive = requested.Contains(a.OldCode, StringComparer.OrdinalIgnoreCase) };
        }
        foreach (var code in requested)
            if (!state.Aliases.Any(a => a.AssetId == id && string.Equals(a.OldCode, code, StringComparison.OrdinalIgnoreCase)))
                state.Aliases.Add(new("ALIAS-" + Guid.NewGuid().ToString("N"), "DEMO", code, id, true));
    }
    private async Task<Context> Load(string actor, string permission, CancellationToken ct, bool combinedRead = false)
    {
        var auth = await authStore.ReadAsync(s => s.Clone(), ct);
        RequirePermission(auth, actor, permission);
        AssetCatalogState state; CategoryCatalogState tree;
        if (combinedRead && store is IAssetCategorySnapshotReader combined && combined.CanReadTogether(categories)) (state, tree) = await combined.ReadAssetCategoriesAsync(ct);
        else { state = await store.ReadAsync(ct); tree = await categories.ReadAsync(ct); }
        try
        {
            var h = new ScopeHierarchySet(ScopeHierarchy.Create(AuthScopeType.OrgUnit, state.OrgUnits.Select(n => new ScopeNode(n.Id, n.ParentId))),
                ScopeHierarchy.Create(AuthScopeType.Location, state.Locations.Select(n => new ScopeNode(n.Id, n.ParentId))),
                ScopeHierarchy.Create(AuthScopeType.CategorySubtree, tree.ActiveNodes.Select(n => new ScopeNode(n.CategoryId, n.ParentId))));
            return new(auth, state, tree, h, tree.ActiveNodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal));
        }
        catch (AuthValidationException) { throw new AssetValidationException("Cây đơn vị/địa điểm/danh mục không hợp lệ; cần đối soát nguồn."); }
    }
    private void RequirePermission(AuthSystemState state, string actor, string permission)
    {
        if (!state.Users.Any(u => u.UserId == actor && u.State == AuthUserState.Active) || !state.RoleAssignments.Any(a => a.UserId == actor && AuthAuthorizationRules.IsEffective(a, clock.GetUtcNow()) && AuthRoleCatalog.HasPermission(a.RoleCode, permission))) throw new AssetForbiddenException();
    }
    private bool Allowed(Context c, string actor, string permission, AssetRecord a) =>
        ValidScope(c, a.CategoryId, a.OrgUnitId, a.LocationId) && authorization.IsAllowed(c.Auth, actor, permission, Scope(a), c.Hierarchies, clock.GetUtcNow());
    private AssetRecord Find(Context c, string actor, string permission, string id) =>
        c.State.Assets.SingleOrDefault(a => a.AssetId == id && Allowed(c, actor, permission, a)) ?? throw new AssetNotFoundException();
    private static bool ValidScope(Context c, string category, string org, string location) =>
        c.Nodes.TryGetValue(category, out var node) && node.State == "DRAFT" && c.State.OrgUnits.Any(o => o.Id == org && o.IsActive) && c.State.Locations.Any(l => l.Id == location && l.OrgUnitId == org && l.IsActive);
    private static ResourceScope Scope(AssetRecord a) => ResourceScope.Required(a.OrgUnitId, a.LocationId, a.CategoryId);
    private static void ValidateDestination(Context c, AssetInput input)
    {
        if (!c.Nodes.ContainsKey(input.CategoryId)) throw new AssetNotFoundException();
        if (!ValidScope(c, input.CategoryId, input.OrgUnitId, input.LocationId)) throw new AssetValidationException("Danh mục/đơn vị/địa điểm phải tồn tại, đang dùng và địa điểm thuộc đúng đơn vị.", "LocationId");
    }
    private static AssetView View(Context c, AssetRecord a) => new(a, c.Nodes[a.CategoryId].Name, c.Nodes[a.CategoryId].State,
        c.State.OrgUnits.Single(o => o.Id == a.OrgUnitId).Name, c.State.Locations.Single(l => l.Id == a.LocationId).Name, c.State.Aliases.Where(x => x.AssetId == a.AssetId).ToArray());
    public static AssetInput ValidateInput(AssetInput? input)
    {
        if (input is null) throw new AssetValidationException("Thiếu dữ liệu tài sản.");
        Text(input.Name, "Name", true, 300); Text(input.CategoryId, "CategoryId", true, 100); Text(input.OrgUnitId, "OrgUnitId", true, 100); Text(input.LocationId, "LocationId", true, 100);
        if (input.Type != "MACHINE") throw new AssetValidationException("Lát TASK-007 chỉ có máy demo MACHINE.", "Type");
        Text(input.CustodianRef, "CustodianRef", false, 200); Text(input.Serial, "Serial", false, 200); Text(input.Model, "Model", false, 200);
        Text(input.Manufacturer, "Manufacturer", false, 200); Text(input.Condition, "Condition", true, 100); Text(input.Notes, "Notes", false, 2000);
        var aliases = input.Aliases ?? [];
        if (aliases.Length > 20) throw new AssetValidationException("Tối đa 20 alias trong lát demo.", "Aliases");
        foreach (var alias in aliases) Text(alias, "Aliases", true, 120);
        var normalized = aliases.Select(a => a.Trim()).ToArray();
        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length) throw new AssetValidationException("Alias bị trùng trong yêu cầu.", "Aliases");
        return input with { Name = input.Name.Trim(), CustodianRef = (input.CustodianRef ?? "").Trim(), Serial = (input.Serial ?? "").Trim(), Model = (input.Model ?? "").Trim(), Manufacturer = (input.Manufacturer ?? "").Trim(), Condition = (input.Condition ?? "").Trim(), Notes = (input.Notes ?? "").Trim(), Aliases = normalized };
    }
    private static void Text(string? value, string field, bool required, int max)
    {
        if (value is null && !required) return;
        if (value is null || required && string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(ch => char.IsControl(ch) && !(field == "Notes" && ch is '\r' or '\n' or '\t'))) throw new AssetValidationException($"{field}: {(required ? "bắt buộc, " : "")}tối đa {max} ký tự, không có ký tự điều khiển.", field);
    }
    private static void ValidateQuery(AssetQuery q)
    {
        if (q.Page < 1 || q.Page > 100000 || q.PageSize < 1 || q.PageSize > 100) throw new AssetValidationException("page 1–100000 và pageSize 1–100.");
        Text(q.Q ?? "", "Q", false, 200); Text(q.CategoryId ?? "", "CategoryId", false, 100); Text(q.OrgUnitId ?? "", "OrgUnitId", false, 100);
        Text(q.LocationId ?? "", "LocationId", false, 100); Text(q.Condition ?? "", "Condition", false, 100); Text(q.CustodianRef ?? "", "CustodianRef", false, 200);
    }
    private static string Normalize(string s) => new(s.Normalize(NormalizationForm.FormD).Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).Select(ch => ch is 'đ' or 'Đ' ? 'D' : char.ToUpperInvariant(ch)).ToArray());
    private sealed record Context(AuthSystemState Auth, AssetCatalogState State, CategoryCatalogState Tree, ScopeHierarchySet Hierarchies, IReadOnlyDictionary<string, CategoryNode> Nodes);
}
