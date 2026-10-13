using System.Security.Cryptography;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Concurrency;
namespace DuyTien.AssetManagement.Application.Categories;

public sealed class TreeMutationService(ITreeMutationStore store, IAuthSystemStore authStore,
    ISingleWriterCoordinator writer, TimeProvider clock)
{
    private readonly ScopeAuthorizationService policy = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<TreeMutationPreview> PreviewAsync(string actor, TreeMutationRequest request, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        request = Normalize(request); var auth = await Auth(actor, Permission(request.Kind), token);
        return Prepare(auth, actor, await store.ReadAsync(token), request).Preview;
    }, ct);
    public Task<TreeMutationResult> CommitAsync(string actor, TreeMutationRequest request,
        long expectedTreeRevision, long expectedAssetRevision, string previewHash, string key,
        bool confirmImpact, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        request = Normalize(request); Text(key, 100, "Idempotency key");
        var auth = await Auth(actor, Permission(request.Kind), token); var current = await store.ReadAsync(token);
        var requestHash = Digest(new { actor, request, expectedTreeRevision, expectedAssetRevision, previewHash });
        var replay = current.Categories.TreeChanges.SingleOrDefault(o => o.Actor == actor && o.IdempotencyKey == key);
        if (replay is not null)
        {
            if (replay.RequestHash != requestHash) throw new CategoryConflictException("Idempotency key đã dùng cho preview/payload khác.");
            RecheckReplay(auth, actor, current, replay);
            return replay.Result with { Replayed = true };
        }
        var plan = Prepare(auth, actor, current, request);
        if (!confirmImpact) throw new CategoryValidationException("Cần xác nhận toàn bộ tác động và lý do trước commit.");
        if (!plan.Preview.CanCommit) throw new CategoryValidationException(string.Join(" ", plan.Preview.Issues));
        if (current.Categories.Revision != expectedTreeRevision || current.Assets.Revision != expectedAssetRevision || previewHash != plan.Preview.PreviewHash)
            throw new CategoryConflictException("Cây/tài sản/reference đã đổi sau preview; tạo preview mới, không ghi đè.");
        var cats = current.Categories.Clone(); var assets = current.Assets.Clone(); var now = clock.GetUtcNow();
        var opId = "TREEOP-" + Guid.NewGuid().ToString("N");
        var version = cats.ActiveTreeVersionId!;
        if (plan.Preview.Categories.Count > 0)
        {
            version = "TREE-" + Guid.NewGuid().ToString("N");
            cats.Categories.AddRange(plan.Nodes.Select(n => n with { TreeVersionId = version }));
            cats.Versions.Add(new(version, "DRAFT", "TREE_CHANGE:" + requestHash, "TASK008_TREE_V1", Digest(cats.Categories.Where(n => n.TreeVersionId == version).ToArray()), now, actor));
            cats.ActiveTreeVersionId = version;
        }
        foreach (var after in plan.ChangedAssets)
        {
            var before = assets.Assets.Single(a => a.AssetId == after.AssetId);
            var saved = after with { Version = before.Version + 1, UpdatedAt = now, UpdatedBy = actor };
            assets.Assets.RemoveAll(a => a.AssetId == saved.AssetId); assets.Assets.Add(saved);
            assets.Operations.Add(new(opId + ":" + saved.AssetId, opId + ":" + saved.AssetId, actor, requestHash,
                !before.IsDeleted && saved.IsDeleted ? "DELETE" : "MOVE", before, saved,
                assets.Aliases.Where(a => a.AssetId == saved.AssetId).OrderBy(a => a.AliasId, StringComparer.Ordinal).ToArray(), "", 0, request.Reason, now));
            assets.Revision++;
        }
        cats.Revision++;
        var result = new TreeMutationResult(version, cats.Revision, assets.Revision, plan.Preview.Categories.Count, plan.ChangedAssets.Length);
        cats.TreeChanges.Add(new(opId, key, actor, requestHash, request.Kind, current.Categories.ActiveTreeVersionId!, version,
            expectedTreeRevision, expectedAssetRevision, request.Reason, now, previewHash, request,
            plan.AffectedCategoryIds, plan.AffectedAssets.Select(a => a.AssetId).ToArray(), plan.Preview.Categories.ToArray(), result));
        await store.CommitAsync(new(cats, assets), expectedTreeRevision, expectedAssetRevision, token);
        return result;
    }, ct);
    private Plan Prepare(AuthSystemState auth, string actor, TreeMutationSnapshot s, TreeMutationRequest request)
    {
        var old = s.Categories.ActiveNodes;
        var existingIssues = CategoryTreeShape.Validate(old); if (existingIssues.Count > 0) throw new CategoryValidationException("Cây hiện có không hợp lệ; cần đối soát trước thay đổi.");
        var byId = old.ToDictionary(n => n.CategoryId, StringComparer.Ordinal); var before = Hierarchies(s.Assets, old);
        var next = byId.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal); var issues = new List<string>();
        var mainPermission = Permission(request.Kind); var affected = new HashSet<string>(StringComparer.Ordinal);
        AssetRecord[] affectedAssets; var changedAssets = new List<AssetRecord>();
        CategoryNode Get(string? id)
        {
            if (id is null || !byId.TryGetValue(id, out var node) || node.State != "DRAFT" || !Allowed(auth, actor, "CATEGORIES_READ", CategoryScope(id), before)) throw new CategoryNotFoundException();
            return node;
        }
        if (request.Kind == "MOVE_ASSETS")
        {
            var target = Get(request.DestinationCategoryId);
            affectedAssets = request.AssetIds!.Select(id => s.Assets.Assets.SingleOrDefault(a => a.AssetId == id) ?? throw new CategoryNotFoundException()).ToArray();
            foreach (var a in affectedAssets)
            {
                RequireAssetRead(auth, actor, s, a, before);
                var destination = a with { CategoryId = target.CategoryId, OrgUnitId = request.DestinationOrgUnitId ?? a.OrgUnitId, LocationId = request.DestinationLocationId ?? a.LocationId };
                ValidateAssetScope(s, destination, byId);
                RequireAssetMove(auth, actor, a, destination, before, before);
                if (a.CategoryId != destination.CategoryId || a.OrgUnitId != destination.OrgUnitId || a.LocationId != destination.LocationId) changedAssets.Add(destination);
            }
        }
        else
        {
            var node = Get(request.CategoryId); affected.UnionWith(old.Where(n => before.Categories.IsWithin(node.CategoryId, n.CategoryId, true)).Select(n => n.CategoryId));
            foreach (var id in affected)
            {
                if (byId[id].State != "DRAFT" && !AuthAuthorizationRules.IsActiveAdmin(auth, auth.Users.Single(u => u.UserId == actor), clock.GetUtcNow())) throw new CategoryNotFoundException();
                if (!Allowed(auth, actor, "CATEGORIES_READ", CategoryScope(id), before) || !Allowed(auth, actor, mainPermission, CategoryScope(id), before)) throw new CategoryNotFoundException();
            }
            affectedAssets = s.Assets.Assets.Where(a => affected.Contains(a.CategoryId)).ToArray();
            foreach (var a in affectedAssets) RequireAssetRead(auth, actor, s, a, before);
            if (request.Kind == "EDIT_CATEGORY")
            {
                if (s.Categories.Categories.Any(n => n.CategoryId != node.CategoryId && n.SegmentCode == request.SegmentCode)) issues.Add("Mã đã thuộc node khác trong lịch sử; không tái dùng alias/mapping.");
                next[node.CategoryId] = node with { Name = request.Name!, SegmentCode = request.SegmentCode!, Definition = request.Definition ?? "" };
            }
            else if (request.Kind == "MOVE_CATEGORY")
            {
                var target = request.DestinationCategoryId is null ? null : Get(request.DestinationCategoryId);
                if (target is null)
                {
                    var allGrant = auth.RoleAssignments.Any(a => a.UserId == actor && AuthAuthorizationRules.IsEffective(a, clock.GetUtcNow()) && AuthRoleCatalog.HasPermission(a.RoleCode, mainPermission) && auth.RoleAssignmentScopes.Any(g => g.AssignmentId == a.AssignmentId && g.ScopeType == AuthScopeType.CategorySubtree && g.ScopeId == "ALL"));
                    if (!allGrant) throw new CategoryNotFoundException();
                }
                else if (!Across(auth, actor, mainPermission, CategoryScope(node.CategoryId), CategoryScope(target.CategoryId), before, before)) throw new CategoryNotFoundException();
                next[node.CategoryId] = node with { ParentId = target?.CategoryId };
            }
            else
            {
                if (request.DeletePlan == "EMPTY_ONLY" && (affected.Count > 1 || affectedAssets.Length > 0)) issues.Add("Nhánh còn con/đối tượng: chọn RELOCATE hoặc SOFT_DELETE, không xóa trực tiếp.");
                CategoryNode? target = null;
                if (request.DeletePlan == "RELOCATE") { target = Get(request.DestinationCategoryId); if (affected.Contains(target.CategoryId)) issues.Add("Đích relocate không thể nằm trong nhánh ngừng dùng."); }
                foreach (var a in affectedAssets)
                {
                    if (request.DeletePlan == "RELOCATE")
                    {
                        var destination = a with { CategoryId = target!.CategoryId };
                        RequireAssetMove(auth, actor, a, destination, before, before); changedAssets.Add(destination);
                    }
                    else if (request.DeletePlan == "SOFT_DELETE")
                    {
                        if (!Allowed(auth, actor, "ASSETS_DELETE", AssetScope(a), before)) throw new CategoryForbiddenException();
                        if (!a.IsDeleted) changedAssets.Add(a with { IsDeleted = true });
                    }
                }
                if (request.DeletePlan != "EMPTY_ONLY" || issues.Count == 0) foreach (var id in affected) next[id] = next[id] with { State = "DRAFT_INACTIVE" };
            }
        }
        var rebuilt = Rebuild(next, issues); issues.AddRange(CategoryTreeShape.Validate(rebuilt));
        if (issues.Count == 0 && request.Kind == "MOVE_CATEGORY")
        {
            var after = Hierarchies(s.Assets, rebuilt);
            foreach (var id in affected) if (!Across(auth, actor, "CATEGORIES_MOVE", CategoryScope(id), CategoryScope(id), before, after)) throw new CategoryNotFoundException();
            foreach (var a in affectedAssets) RequireAssetMove(auth, actor, a, a, before, after);
        }
        rebuilt = rebuilt.Select(n => Different(byId[n.CategoryId], n) ? n with { Version = byId[n.CategoryId].Version + 1 } : n).ToArray();
        var displayAfter = issues.Any(x => x.Contains("Cycle", StringComparison.Ordinal)) ? before : Hierarchies(s.Assets, rebuilt);
        var impacts = rebuilt.Where(n => Different(byId[n.CategoryId], n)).Select(n => new TreeNodeImpact(n.CategoryId, n.Name,
            Path(auth, actor, byId[n.CategoryId], byId, before), Path(auth, actor, n, next, displayAfter), byId[n.CategoryId].FLevel, n.FLevel, byId[n.CategoryId].State, n.State)).OrderBy(n => n.CategoryId, StringComparer.Ordinal).ToArray();
        if (impacts.Length == 0 && changedAssets.Count == 0) issues.Add("Không có thay đổi; chưa commit snapshot mới.");
        var assetImpacts = affectedAssets.OrderBy(a => a.AssetId, StringComparer.Ordinal).Select(a =>
        {
            var after = changedAssets.SingleOrDefault(x => x.AssetId == a.AssetId) ?? a;
            return new TreeAssetImpact(a.AssetId, a.ObjectCode, a.Name, a.Version, byId[a.CategoryId].Name, byId[after.CategoryId].Name,
                s.Assets.Locations.Single(l => l.Id == a.LocationId).Name, s.Assets.Locations.Single(l => l.Id == after.LocationId).Name, after.IsDeleted);
        }).ToArray();
        var hash = Digest(new { actor, request, s.Categories.Revision, AssetRevision = s.Assets.Revision, Nodes = s.Categories.Categories, s.Categories.ActiveTreeVersionId, s.Assets.Assets, s.Assets.Aliases, s.Assets.Counters, s.Assets.Rules, s.Assets.OrgUnits, s.Assets.Locations });
        return new(new(s.Categories.Revision, s.Assets.Revision, hash, s.Categories.ActiveTreeVersionId!, impacts, assetImpacts, changedAssets.Count, issues.Distinct(StringComparer.Ordinal).ToArray(), affected.Count, affectedAssets.Length), rebuilt, changedAssets.ToArray(), affected.Order(StringComparer.Ordinal).ToArray(), affectedAssets);
    }
    private void RecheckReplay(AuthSystemState auth, string actor, TreeMutationSnapshot s, TreeChangeOperation op)
    {
        var hierarchy = Hierarchies(s.Assets, s.Categories.ActiveNodes); var nodes = s.Categories.ActiveNodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
        foreach (var id in op.CategoryIds) if (!nodes.ContainsKey(id) || !Allowed(auth, actor, Permission(op.Kind), CategoryScope(id), hierarchy)) throw new CategoryNotFoundException();
        foreach (var id in op.AssetIds)
        {
            var a = s.Assets.Assets.SingleOrDefault(a => a.AssetId == id) ?? throw new CategoryNotFoundException();
            var permission = op.Kind == "ARCHIVE_CATEGORY" && op.Request.DeletePlan == "SOFT_DELETE" ? "ASSETS_DELETE" : op.Kind == "EDIT_CATEGORY" ? "ASSETS_READ" : "ASSETS_MOVE";
            if (!Allowed(auth, actor, permission, AssetScope(a), hierarchy)) throw new CategoryNotFoundException();
        }
    }
    private async Task<AuthSystemState> Auth(string actor, string permission, CancellationToken ct)
    {
        var s = await authStore.ReadAsync(s => s.Clone(), ct);
        if (!s.Users.Any(u => u.UserId == actor && u.State == AuthUserState.Active && !u.MustChangePassword) || !s.RoleAssignments.Any(a => a.UserId == actor && AuthAuthorizationRules.IsEffective(a, clock.GetUtcNow()) && AuthRoleCatalog.HasPermission(a.RoleCode, permission))) throw new CategoryForbiddenException();
        return s;
    }
    private void RequireAssetRead(AuthSystemState auth, string actor, TreeMutationSnapshot s, AssetRecord a, ScopeHierarchySet h)
    {
        var nodes = s.Categories.ActiveNodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
        ValidateAssetScope(s, a, nodes, allowArchived: a.IsDeleted && AuthAuthorizationRules.IsActiveAdmin(auth, auth.Users.Single(u => u.UserId == actor), clock.GetUtcNow()));
        if (!Allowed(auth, actor, "ASSETS_READ", AssetScope(a), h)) throw new CategoryNotFoundException();
    }
    private void RequireAssetMove(AuthSystemState auth, string actor, AssetRecord source, AssetRecord destination, ScopeHierarchySet before, ScopeHierarchySet after)
    {
        if (!Across(auth, actor, "ASSETS_MOVE", AssetScope(source), AssetScope(destination), before, after) || !Allowed(auth, actor, "ASSETS_UPDATE", AssetScope(source), before) || !Allowed(auth, actor, "ASSETS_UPDATE", AssetScope(destination), after)) throw new CategoryNotFoundException();
    }
    private static void ValidateAssetScope(TreeMutationSnapshot s, AssetRecord a, IReadOnlyDictionary<string, CategoryNode> nodes, bool allowArchived = false)
    {
        if (!nodes.TryGetValue(a.CategoryId, out var n) || n.State != "DRAFT" && !allowArchived || !s.Assets.OrgUnits.Any(o => o.Id == a.OrgUnitId && o.IsActive) || !s.Assets.Locations.Any(l => l.Id == a.LocationId && l.OrgUnitId == a.OrgUnitId && l.IsActive)) throw new CategoryNotFoundException();
    }
    private bool Across(AuthSystemState s, string actor, string permission, ResourceScope source, ResourceScope destination, ScopeHierarchySet before, ScopeHierarchySet after)
    {
        foreach (var a in s.RoleAssignments.Where(a => a.UserId == actor))
        { var isolated = s.Clone(); isolated.RoleAssignments.RemoveAll(x => x.AssignmentId != a.AssignmentId); if (Allowed(isolated, actor, permission, source, before) && Allowed(isolated, actor, permission, destination, after)) return true; }
        return false;
    }
    private bool Allowed(AuthSystemState s, string actor, string permission, ResourceScope scope, ScopeHierarchySet h) => policy.IsAllowed(s, actor, permission, scope, h, clock.GetUtcNow());
    private static ResourceScope CategoryScope(string id) => new(new(false, null), new(false, null), new(true, id));
    private static ResourceScope AssetScope(AssetRecord a) => ResourceScope.Required(a.OrgUnitId, a.LocationId, a.CategoryId);
    private static ScopeHierarchySet Hierarchies(AssetCatalogState assets, IEnumerable<CategoryNode> nodes) => new(ScopeHierarchy.Create(AuthScopeType.OrgUnit, assets.OrgUnits.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.Location, assets.Locations.Select(n => new ScopeNode(n.Id, n.ParentId))), ScopeHierarchy.Create(AuthScopeType.CategorySubtree, nodes.Select(n => new ScopeNode(n.CategoryId, n.ParentId))));
    private string Path(AuthSystemState auth, string actor, CategoryNode n, IReadOnlyDictionary<string, CategoryNode> nodes, ScopeHierarchySet h)
    {
        var parts = new List<string> { n.Name + " [" + n.SegmentCode + "]" }; var parent = n.ParentId; var seen = new HashSet<string>(StringComparer.Ordinal) { n.CategoryId };
        while (parent is not null && seen.Add(parent) && nodes.TryGetValue(parent, out var p) && Allowed(auth, actor, "CATEGORIES_READ", CategoryScope(parent), h)) { parts.Insert(0, p.Name + " [" + p.SegmentCode + "]"); parent = p.ParentId; }
        return (parent is null ? "" : "[nhánh được phép] / ") + string.Join(" / ", parts);
    }
    private static CategoryNode[] Rebuild(Dictionary<string, CategoryNode> nodes, List<string> issues)
    {
        return nodes.Values.Select(n =>
        {
            var chain = new List<CategoryNode> { n }; var parent = n.ParentId; var seen = new HashSet<string>(StringComparer.Ordinal) { n.CategoryId };
            while (parent is not null)
            { if (!nodes.TryGetValue(parent, out var p)) { issues.Add("Thiếu cha."); break; } if (!seen.Add(parent)) { issues.Add("Cycle bị chặn."); break; } chain.Insert(0, p); parent = p.ParentId; }
            return n with { FLevel = chain.Count, FullCode = string.Join('.', chain.Select(p => p.SegmentCode)) };
        }).ToArray();
    }
    private static bool Different(CategoryNode a, CategoryNode b) => a.ParentId != b.ParentId || a.Name != b.Name || a.SegmentCode != b.SegmentCode || a.Definition != b.Definition || a.State != b.State || a.FLevel != b.FLevel || a.FullCode != b.FullCode;
    private static string Permission(string kind) => kind switch { "EDIT_CATEGORY" => "CATEGORIES_UPDATE", "MOVE_CATEGORY" => "CATEGORIES_MOVE", "ARCHIVE_CATEGORY" => "CATEGORIES_DELETE", "MOVE_ASSETS" => "ASSETS_MOVE", _ => throw new CategoryValidationException("Loại thao tác cây không hợp lệ.") };
    public static TreeMutationRequest Normalize(TreeMutationRequest r)
    {
        if (r is null) throw new CategoryValidationException("Thiếu dữ liệu thao tác.");
        _ = Permission(r.Kind); Text(r.Reason, 500, "Lý do");
        if (r.Kind != "MOVE_ASSETS") Text(r.CategoryId, 100, "Danh mục nguồn");
        if (r.Kind == "EDIT_CATEGORY") { Text(r.Name, 300, "Tên danh mục"); Text(r.SegmentCode, 3, "Mã đoạn"); if (r.Definition?.Length > 4000) throw new CategoryValidationException("Definition quá dài."); }
        if (r.Kind == "ARCHIVE_CATEGORY" && r.DeletePlan is not ("EMPTY_ONLY" or "RELOCATE" or "SOFT_DELETE")) throw new CategoryValidationException("Phương án xóa không hợp lệ.");
        if (r.Kind == "MOVE_ASSETS") { if (r.AssetIds is null || r.AssetIds.Length is < 1 or > 100 || r.AssetIds.Distinct(StringComparer.Ordinal).Count() != r.AssetIds.Length) throw new CategoryValidationException("Chọn1–100 AssetId duy nhất."); foreach (var id in r.AssetIds) Text(id, 100, "AssetId"); Text(r.DestinationCategoryId, 100, "Đích"); }
        if (r.DestinationCategoryId is not null) Text(r.DestinationCategoryId, 100, "Đích");
        if (r.DestinationOrgUnitId is not null) Text(r.DestinationOrgUnitId, 100, "Đơn vị đích");
        if (r.DestinationLocationId is not null) Text(r.DestinationLocationId, 100, "Địa điểm đích");
        return r with { Reason = r.Reason.Trim(), Name = r.Name?.Trim(), SegmentCode = r.SegmentCode?.Trim(), Definition = r.Definition?.Trim(), AssetIds = r.AssetIds?.Order(StringComparer.Ordinal).ToArray() };
    }
    private static void Text(string? s, int max, string label) { if (string.IsNullOrWhiteSpace(s) || s.Length > max || s.Any(char.IsControl)) throw new CategoryValidationException(label + ": bắt buộc, tối đa" + max + " ký tự, không ký tự điều khiển."); }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    private sealed record Plan(TreeMutationPreview Preview, CategoryNode[] Nodes, AssetRecord[] ChangedAssets, string[] AffectedCategoryIds, AssetRecord[] AffectedAssets);
}
