using System.Security.Cryptography;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
namespace DuyTien.AssetManagement.Application.Inventory;

public sealed partial class InventoryService(IInventoryStore store, ICategoryCatalogStore categories, IAssetCatalogStore references, IAuthSystemStore authStore, ISingleWriterCoordinator writer, TimeProvider clock)
{
    public const int ChunkSize = 100;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ScopeAuthorizationService authorization = new();
    private sealed record Context(InventoryState State, CategoryCatalogState Tree, AssetCatalogState References, AuthSystemState Auth, ScopeHierarchySet Hierarchies);
    private async Task<Context> Load(string actor, string permission, CancellationToken ct)
    {
        var auth = await authStore.ReadAsync(s => s.Clone(), ct); Require(auth, actor, permission);
        var tree = await categories.ReadAsync(ct); var refs = await references.ReadAsync(ct);
        var hierarchies = new ScopeHierarchySet(ScopeHierarchy.Create(AuthScopeType.OrgUnit, refs.OrgUnits.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.Location, refs.Locations.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.CategorySubtree, tree.ActiveNodes.Select(n => new ScopeNode(n.CategoryId, n.ParentId))));
        return new(await store.ReadAsync(ct), tree, refs, auth, hierarchies);
    }
    private void Require(AuthSystemState a, string actor, string permission)
    { if (!a.Users.Any(u => u.UserId == actor && u.State == AuthUserState.Active && !u.MustChangePassword) || !a.RoleAssignments.Any(r => r.UserId == actor && AuthAuthorizationRules.IsEffective(r, clock.GetUtcNow()) && AuthRoleCatalog.HasPermission(r.RoleCode, permission))) throw new InventoryForbiddenException(); }
    private bool Recovery(Context c, string actor) => AuthAuthorizationRules.IsActiveAdmin(c.Auth, c.Auth.Users.Single(u => u.UserId == actor), clock.GetUtcNow());
    private static ResourceScope CategoryScope(string? id) => new(new(false, null), new(false, null), new(true, id));
    private bool CanItem(Context c, string actor, string permission, ItemRecord item) => item.CategoryId.Length > 0 && c.Tree.ActiveNodes.Any(n => n.CategoryId == item.CategoryId && n.State == "DRAFT") && authorization.IsAllowed(c.Auth, actor, permission, CategoryScope(item.CategoryId), c.Hierarchies, clock.GetUtcNow());
    private void CheckImport(Context c, string actor, ItemRecord item, ItemRecord? before = null)
    {
        if (item.CategoryId.Length == 0) { if (!Recovery(c, actor)) throw new InventoryForbiddenException(); }
        else if (!CanItem(c, actor, "ITEMS_IMPORT", item)) throw new InventoryForbiddenException();
        if (before is not null)
        {
            if (before.CategoryId.Length == 0) { if (!Recovery(c, actor)) throw new InventoryForbiddenException(); }
            else if (!CanItem(c, actor, "ITEMS_IMPORT", before)) throw new InventoryForbiddenException();
            if (before.CategoryId.Length > 0 && item.CategoryId.Length > 0 && !authorization.IsMoveAllowed(c.Auth, actor, "ITEMS_IMPORT", CategoryScope(before.CategoryId), CategoryScope(item.CategoryId), c.Hierarchies, clock.GetUtcNow())) throw new InventoryForbiddenException();
        }
    }
    public Task<ItemPreview> PreviewAsync(string actor, ItemImportRequest request, int page = 1, int pageSize = 20, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    { var c = await Load(actor, "ITEMS_IMPORT", t); var (rows, errors, warnings) = Prepare(c, actor, request); Page(page, pageSize); return Preview(c, request, rows, errors, warnings, page, pageSize); }, ct);
    private (ItemPlannedRow[] Rows, List<InventoryIssue> Errors, List<string> Warnings) Prepare(Context c, string actor, ItemImportRequest request)
    {
        if (request is null || request.Document is null || request.Groups is null || request.Decisions is null || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000) throw new InventoryValidationException("Cần document, group mapping, decisions và lý do<=1000ký tự.");
        var errors = ItemImportReader.Validate(request.Document); var warnings = new List<string> { "DRAFT_DEMO; workbook gốc, stock/ngưỡng và Org/Location chưa xác minh. Không cấp ObjectCode/Asset, không nhập tồn từ BOM/kho mặc định." };
        if (errors.Count > 0) return ([], errors, warnings);
        if (request.Document.Rows.Length != 20744) warnings.Add($"Nguồn có{request.Document.Rows.Length}dòng, khác baseline20744; không ép dữ liệu. Baseline chưa xác minh workbook gốc.");
        if (request.Groups.Any(g => g is null || g.SourceGroup is null || g.SourceGroup.Length > 2000) || request.Groups.Length > 30000 || request.Groups.GroupBy(g => g.SourceGroup, StringComparer.Ordinal).Any(g => g.Count() > 1) || request.Decisions.Any(d => d is null || string.IsNullOrWhiteSpace(d.SourceCode)) || request.Decisions.Length > 30000 || request.Decisions.GroupBy(d => d.SourceCode, StringComparer.Ordinal).Any(g => g.Count() > 1)) throw new InventoryValidationException("Mapping/decision trùng hoặc không hợp lệ.");
        var groups = request.Groups.ToDictionary(g => g.SourceGroup, StringComparer.Ordinal); var decisions = request.Decisions.ToDictionary(d => d.SourceCode, StringComparer.Ordinal); var planned = new List<ItemPlannedRow>();
        var bySourceCode = c.State.Items.ToLookup(i => i.SourceCode, StringComparer.Ordinal); var stockIds = c.State.Balances.Select(b => b.ItemId).ToHashSet(StringComparer.Ordinal);
        var known = c.State.Items.ToDictionary(i => i.Namespace + "\0" + i.SourceIdentity, StringComparer.Ordinal);
        var duplicateCodes = request.Document.Rows.GroupBy(r => r.Cells[0], StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        if (decisions.Keys.Any(k => !duplicateCodes.Contains(k))) errors.Add(new(null, "Decision chỉ được gắn mã đang trùng trong nguồn."));
        foreach (var batch in request.Document.Rows.OrderBy(r => r.SourceRow).GroupBy(r => r.Cells[0], StringComparer.Ordinal))
        {
            var selected = batch.ToArray(); var identities = selected.ToDictionary(r => r.SourceRow, _ => "CODE:" + batch.Key);
            if (selected.Length > 1)
            {
                if (!decisions.TryGetValue(batch.Key, out var decision) || string.IsNullOrWhiteSpace(decision.Reason) || decision.Reason.Length > 1000) { errors.Add(new(selected[0].SourceRow, "Mã trùng cần decision MERGE/SPLIT có lý do: " + batch.Key)); continue; }
                if (decision.Mode == "MERGE")
                { if (selected.Skip(1).Any(r => !r.Cells.SequenceEqual(selected[0].Cells, StringComparer.Ordinal))) { errors.Add(new(selected[0].SourceRow, "MERGE chỉ khi20cột đồng nhất; khác dữ liệu cần SPLIT có identity.")); continue; } selected = [selected[0]]; }
                else if (decision.Mode == "SPLIT")
                { if (decision.Identities is null || !decision.Identities.Keys.Order().SequenceEqual(selected.Select(r => r.SourceRow).Order()) || decision.Identities.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 200 || v != v.Trim()) || decision.Identities.Values.Distinct(StringComparer.Ordinal).Count() != selected.Length) { errors.Add(new(selected[0].SourceRow, "SPLIT cần identity tường minh/duy nhất cho từng row, không lấy row làm ID.")); continue; } identities = decision.Identities.ToDictionary(p => p.Key, p => "SPLIT:" + p.Value); }
                else { errors.Add(new(selected[0].SourceRow, "Decision chỉ MERGE/SPLIT.")); continue; }
            }
            var sourceIdentities = selected.Select(r => identities[r.SourceRow]).ToHashSet(StringComparer.Ordinal);
            if (bySourceCode[batch.Key].Any(i => i.Namespace == request.Document.Namespace && !sourceIdentities.Contains(i.SourceIdentity))) { errors.Add(new(selected[0].SourceRow, "Decision thay identity đã lưu; cần đối soát mapping, không tự thêm bản sao.")); continue; }
            foreach (var r in selected)
            {
                groups.TryGetValue(r.Cells[3], out var group); var category = group?.CategoryId ?? "";
                if (category.Length > 0 && !c.Tree.ActiveNodes.Any(n => n.CategoryId == category && n.State == "DRAFT")) { errors.Add(new(r.SourceRow, "Category mapping thiếu/ngừng dùng trong cây hiện hành.")); continue; }
                if (group?.StockTracked == true && (r.Cells[2] == "Dịch vụ" || string.IsNullOrWhiteSpace(r.Cells[5]))) { errors.Add(new(r.SourceRow, "Dịch vụ/thiếu đơn vị không được stock-tracked.")); continue; }
                var identity = identities[r.SourceRow]; known.TryGetValue(request.Document.Namespace + "\0" + identity, out var old);
                if (old is not null && old.SourceCode != r.Cells[0]) { errors.Add(new(r.SourceRow, "Identity đã gắn mã khác; không remap ngầm.")); continue; }
                var item = new ItemRecord(old?.ItemId ?? "NEW", request.Document.Namespace, identity, category, group?.StockTracked, r.Cells.ToArray(), old?.Version ?? 0, category.Length == 0 ? "QUARANTINED" : "DRAFT_DEMO");
                CheckImport(c, actor, item, old);
                if (old is not null && stockIds.Contains(old.ItemId) && (old.CategoryId != item.CategoryId || old.PrimaryUnit != item.PrimaryUnit || old.StockTracked != item.StockTracked)) { errors.Add(new(r.SourceRow, "Item có snapshot: đổi scope/đơn vị/tracking cần đối soát riêng; không bypass guard.")); continue; }
                if (JsonSerializer.Serialize(item, Json).Length > 40000) { errors.Add(new(r.SourceRow, "Item vượt40000ký tự JSON, chưa ghi.")); continue; }
                var same = old is not null && old.CategoryId == item.CategoryId && old.StockTracked == item.StockTracked && old.State == item.State && old.Cells.SequenceEqual(item.Cells, StringComparer.Ordinal);
                planned.Add(new(r.SourceRow, item, old?.Version ?? 0, old is null ? "ADD" : same ? "SKIP" : "UPDATE"));
            }
        }
        if (planned.GroupBy(r => r.Item.SourceIdentity, StringComparer.Ordinal).Any(g => g.Count() > 1)) errors.Add(new(null, "Source identity bị trùng sau resolve."));
        return (planned.ToArray(), errors, warnings);
    }
    private static ItemPreview Preview(Context c, ItemImportRequest r, ItemPlannedRow[] rows, List<InventoryIssue> errors, List<string> warnings, int page, int size)
    { var hash = Hash(r); return new(hash, Hash(new { hash, c.State.Revision, c.Tree.ActiveTreeVersionId, rows }), c.State.Revision, c.Tree.ActiveTreeVersionId ?? "", r.Document.Rows.Length, rows.Length, rows.Count(x => x.Action == "ADD"), rows.Count(x => x.Action == "UPDATE"), rows.Count(x => x.Action == "SKIP"), rows.Count(x => x.Item.CategoryId.Length == 0), errors, warnings, rows.Skip((page - 1) * size).Take(size).Select(x => new ItemDiff(x.SourceRow, x.Item.SourceCode, x.Item.Name, x.Action, x.ExpectedVersion, x.Item.CategoryId)).ToArray(), page, size); }
    public Task<ItemImportJob> StartAsync(string actor, ItemImportRequest request, long revision, string previewHash, string key, bool confirm, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    {
        Key(key); var c = await Load(actor, "ITEMS_IMPORT", t); var hash = Hash(request); var id = "IMPORT-" + Hash(new { actor, key }); var job = c.State.Jobs.SingleOrDefault(j => j.JobId == id);
        var prior = c.State.Operations.SingleOrDefault(o => o.Actor == actor && o.Key == key); if (prior is not null && (prior.Action != "IMPORT_START" || prior.RequestHash != hash)) throw new InventoryConflictException("Key đã dùng với payload khác.");
        var (rows, errors, warnings) = Prepare(c, actor, request); if (errors.Count > 0) throw new InventoryValidationException("Preview còn lỗi: " + errors[0].Message);
        if (job is not null) { if (job.Actor != actor || job.RequestHash != hash) throw new InventoryConflictException("Key đã dùng với payload khác."); if (job.State != "STAGING") { CheckJob(c, actor, job); return job; } }
        else
        {
            var p = Preview(c, request, rows, errors, warnings, 1, 20); if (!confirm) throw new InventoryValidationException("Cần confirm preview DRAFT/decision."); if (c.State.Revision != revision || p.PreviewHash != previewHash) throw new InventoryConflictException("Preview/revision đã đổi, preview lại.");
            if (string.IsNullOrEmpty(c.Tree.ActiveTreeVersionId)) throw new InventoryValidationException("Cần cây DRAFT hiện hành.");
            job = new(id, actor, hash, c.Tree.ActiveTreeVersionId, request.Reason, JsonSerializer.Serialize(new { MappingVersion = "ITEMS_VTHH20_V1", SourceHashDeclared = request.Document.SourceHash, DocumentHash = Hash(request.Document), SourceVerified = false, Decisions = request.Decisions }, Json), request.Document.Rows.Length, rows.Length, 0, "STAGING", clock.GetUtcNow(), clock.GetUtcNow());
            await Save(c, new("INV-" + Guid.NewGuid().ToString("N"), key, actor, hash, "IMPORT_START", request.Reason, clock.GetUtcNow(), job, [], [], null), t);
        }
        if (c.Tree.ActiveTreeVersionId != job.TreeVersionId) throw new InventoryConflictException("Tree version đổi khi staging; không ghi Items.");
        var staged = JobPlan(c.State, id).ToArray();
        if (staged.Length > rows.Length || staged.Where((p, i) => Hash(p.Row with { Item = p.Row.Item with { ItemId = "NEW" } }) != Hash(rows[i] with { Item = rows[i].Item with { ItemId = "NEW" } })).Any()) throw new InventoryConflictException("Staging/input đã thay đổi; không tiếp tục job.");
        while (staged.Length < rows.Length)
        {
            t.ThrowIfCancellationRequested(); var remaining = rows.Skip(staged.Length); var slice = remaining.Take(NextChunkCount(remaining)).ToArray(); var entries = slice.Select(r => new ItemImportPlanRow(id, r with { Item = r.Item with { ItemId = r.Item.ItemId == "NEW" ? "ITEM-" + Guid.NewGuid().ToString("N") : r.Item.ItemId } })).ToArray();
            job = job with { State = staged.Length + entries.Length == rows.Length ? "VALIDATED" : "STAGING", UpdatedAt = clock.GetUtcNow() };
            await Save(c, new("INV-" + Guid.NewGuid().ToString("N"), key + ":stage:" + staged.Length, actor, hash, "IMPORT_STAGE", request.Reason, clock.GetUtcNow(), job, entries, [], null), t); staged = JobPlan(c.State, id).ToArray();
        }
        return job;
    }, ct);
    public Task<ItemImportJob> JobAsync(string actor, string id, CancellationToken ct = default) => writer.ExecuteAsync(async t => { var c = await Load(actor, "ITEMS_IMPORT", t); var j = c.State.Jobs.SingleOrDefault(j => j.JobId == id) ?? throw new InventoryNotFoundException(); CheckJob(c, actor, j); return j; }, ct);
    public Task<IReadOnlyList<ItemImportJob>> JobsAsync(string actor, CancellationToken ct = default) => writer.ExecuteAsync(async t => { var c = await Load(actor, "ITEMS_IMPORT", t); return (IReadOnlyList<ItemImportJob>)c.State.Jobs.Where(j => j.Actor == actor || Recovery(c, actor)).Where(j => c.State.Plan.Where(p => p.JobId == j.JobId).All(p => p.Row.Item.CategoryId.Length == 0 ? Recovery(c, actor) : CanItem(c, actor, "ITEMS_IMPORT", p.Row.Item))).OrderByDescending(j => j.CreatedAt).Take(50).ToArray(); }, ct);
    private void CheckJob(Context c, string actor, ItemImportJob job)
    { if (job.Actor != actor && !Recovery(c, actor)) throw new InventoryNotFoundException(); var known = c.State.Items.ToDictionary(i => i.ItemId, StringComparer.Ordinal); var checkedScopes = new HashSet<(string, string?)>(); foreach (var p in c.State.Plan.Where(p => p.JobId == job.JobId)) { known.TryGetValue(p.Row.Item.ItemId, out var before); if (checkedScopes.Add((p.Row.Item.CategoryId, before?.CategoryId))) CheckImport(c, actor, p.Row.Item, before); } }
    public Task<ItemImportJob> CommitChunkAsync(string actor, string id, int expectedProgress, string key, CancellationToken ct = default) => writer.ExecuteAsync(async t =>
    {
        Key(key); var c = await Load(actor, "ITEMS_IMPORT", t); var job = c.State.Jobs.SingleOrDefault(j => j.JobId == id) ?? throw new InventoryNotFoundException(); CheckJob(c, actor, job); var hash = Hash(new { id, expectedProgress });
        var prior = c.State.Operations.SingleOrDefault(o => o.Actor == actor && o.Key == key); if (prior is not null) { if (prior.Action != "IMPORT_CHUNK" || prior.RequestHash != hash) throw new InventoryConflictException("Key payload khác."); return prior.Job!; }
        if (job.State == "STAGING" || job.Processed != expectedProgress || job.State == "COMPLETE" || c.Tree.ActiveTreeVersionId != job.TreeVersionId) throw new InventoryConflictException("Job chưa validated/progress/tree đã đổi.");
        var remaining = JobPlan(c.State, id).Skip(job.Processed); var plan = remaining.Take(NextChunkCount(remaining.Select(p => p.Row))).ToArray(); if (plan.Length == 0) throw new InventoryConflictException("Job không có chunk hợp lệ."); var changes = new List<ItemRecord>(); var currentItems = c.State.Items.ToDictionary(i => i.ItemId, StringComparer.Ordinal); var identities = c.State.Items.ToDictionary(i => i.Namespace + "\0" + i.SourceIdentity, StringComparer.Ordinal); var stockIds = c.State.Balances.Select(b => b.ItemId).ToHashSet(StringComparer.Ordinal);
        foreach (var p in plan) { var r = p.Row; currentItems.TryGetValue(r.Item.ItemId, out var old); if (identities.TryGetValue(r.Item.Namespace + "\0" + r.Item.SourceIdentity, out var mapped) && mapped.ItemId != r.Item.ItemId) throw new InventoryConflictException("Identity đã được job khác import; không thêm bản sao."); if (old is not null && stockIds.Contains(old.ItemId) && (old.CategoryId != r.Item.CategoryId || old.PrimaryUnit != r.Item.PrimaryUnit || old.StockTracked != r.Item.StockTracked)) throw new InventoryConflictException("Item có snapshot mới; cần đối soát scope/đơn vị/tracking."); if ((old?.Version ?? 0) != r.ExpectedVersion) throw new InventoryConflictException("Item version đã đổi; không ghi đè chunk."); if (r.Action != "SKIP") changes.Add(r.Item with { Version = r.ExpectedVersion + 1 }); }
        job = job with { Processed = job.Processed + plan.Length, State = job.Processed + plan.Length == job.PlannedCount ? "COMPLETE" : "PARTIAL", UpdatedAt = clock.GetUtcNow() };
        await Save(c, new("INV-" + Guid.NewGuid().ToString("N"), key, actor, hash, "IMPORT_CHUNK", job.Reason, clock.GetUtcNow(), job, [], changes.ToArray(), null), t); return job;
    }, ct);
    private async Task Save(Context c, InventoryOperation op, CancellationToken ct)
    { var s = c.State.Clone(); InventoryIntegrity.Apply(s, op); s.Revision++; await store.CommitAsync(s, c.State.Revision, ct); c.State.Revision = s.Revision; c.State.Items.Clear(); c.State.Items.AddRange(s.Items); c.State.Plan.Clear(); c.State.Plan.AddRange(s.Plan); c.State.Jobs.Clear(); c.State.Jobs.AddRange(s.Jobs); c.State.Balances.Clear(); c.State.Balances.AddRange(s.Balances); c.State.Operations.Clear(); c.State.Operations.AddRange(s.Operations); }
    internal static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    private static IEnumerable<ItemImportPlanRow> JobPlan(InventoryState state, string jobId) =>
        state.Operations.SelectMany(o => o.Plan).Where(p => p.JobId == jobId);
    public static int NextChunkCount(IEnumerable<ItemPlannedRow> rows)
    {
        long budget = 0; var count = 0;
        foreach (var row in rows.Take(ChunkSize))
        {
            // Include nested JSON/stringValue escaping and both materialized+detail rows.
            var cost = 8L * JsonSerializer.SerializeToUtf8Bytes(row, Json).Length + 1024;
            if (budget + cost > 1024 * 1024) { if (count == 0) throw new InventoryValidationException("Một row vượt byte budget chunk; chưa ghi."); break; }
            budget += cost; count++;
        }
        return count;
    }
    private static void Key(string key) { if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new InventoryValidationException("Key bắt buộc, tối đa100ký tự."); }
    private static void Page(int page, int size) { if (page < 1 || page > 100000 || size is < 1 or > 100) throw new InventoryValidationException("Page>=1, pageSize1–100."); }
}
