using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Concurrency;
namespace DuyTien.AssetManagement.Application.Categories;

public sealed partial class CategoryCatalogService(ICategoryCatalogStore store, IAuthSystemStore authStore,
    ISingleWriterCoordinator writer, TimeProvider clock, DuyTien.AssetManagement.Application.Assets.IAssetCatalogStore? assetStore = null, DuyTien.AssetManagement.Application.Inventory.IInventoryStore? inventoryStore = null, ITreeMutationStore? registryStore = null)
{
    private readonly ScopeAuthorizationService authorization = new();
    public Task<CategoryPreview> PreviewAsync(string actor, CategoryDocument document, CancellationToken ct = default) => writer.ReadAsync(t => PreviewCore(actor, document, t), ct);
    private async Task<CategoryPreview> PreviewCore(string actor, CategoryDocument document, CancellationToken ct)
    {
        var auth = await authStore.ReadAsync(s => s.Clone(), ct).ConfigureAwait(false);
        RequirePermission(auth, actor, "CATEGORIES_IMPORT");
        var registry = registryStore is null ? null : await registryStore.ReadAsync(ct).ConfigureAwait(false);
        if (registry is not null) TreeRegistryIntegrity.Validate(registry);
        var state = registry?.Categories ?? await store.ReadAsync(ct).ConfigureAwait(false);
        return Prepare(auth, actor, state, document);
    }
    public Task<CategoryImportResult> ImportAsync(string actor, CategoryDocument document, long expectedRevision,
        string previewHash, string idempotencyKey, bool confirmDuplicateNames, CancellationToken ct = default) => writer.ExecuteAsync(async token =>
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100 || idempotencyKey.Any(char.IsControl))
            throw new CategoryValidationException("Idempotency key phải dài 1–100 ký tự.");
        var auth = await authStore.ReadAsync(s => s.Clone(), token).ConfigureAwait(false);
        RequirePermission(auth, actor, "CATEGORIES_IMPORT");
        var registry = registryStore is null ? null : await registryStore.ReadAsync(token).ConfigureAwait(false);
        if (registry is not null) TreeRegistryIntegrity.Validate(registry);
        var state = registry?.Categories ?? await store.ReadAsync(token).ConfigureAwait(false);
        var prepared = Prepare(auth, actor, state, document);
        if (!prepared.CanCommit) throw new CategoryValidationException("Preview có lỗi; chưa thể import.");
        var previous = state.Operations.SingleOrDefault(o => o.ActorUserId == actor && o.IdempotencyKey == idempotencyKey);
        var requestHash = DocumentHash(document);
        if (previous is not null)
        {
            if (previous.RequestHash != requestHash) throw new CategoryConflictException("Idempotency key đã được dùng cho dữ liệu khác.");
            return new CategoryImportResult(previous.TreeVersionId, state.Revision, true, previous.TotalRows);
        }
        if (expectedRevision != state.Revision || previewHash != prepared.PreviewHash)
            throw new CategoryConflictException("Cây hoặc file đã đổi sau preview; cần preview lại.");
        if (prepared.DuplicateNames.Count > 0 && !confirmDuplicateNames)
            throw new CategoryValidationException("Cần xác nhận giữ riêng các tên nhiều mã ở trạng thái DRAFT.");
        var existing = state.Versions.SingleOrDefault(v => v.SourceHash == document.SourceHash && v.MappingVersion == document.MappingVersion);
        var alreadyImported = existing is not null || prepared.Diff.Count == 0 && state.ActiveTreeVersionId is not null;
        var candidate = state.Clone();
        var versionId = existing?.TreeVersionId ?? (alreadyImported ? state.ActiveTreeVersionId! : "TREE-" + Guid.NewGuid().ToString("N"));
        var draft = alreadyImported ? [] : BuildNodes(state, document.Rows, versionId);
        if (!alreadyImported && assetStore is not null)
        {
            var assets = await assetStore.ReadAsync(token).ConfigureAwait(false);
            foreach (var asset in assets.Assets)
            {
                var before = state.ActiveNodes.SingleOrDefault(n => n.CategoryId == asset.CategoryId);
                var after = draft.SingleOrDefault(n => n.CategoryId == asset.CategoryId);
                if (before is null || after is null || before.State != after.State || !Ancestors(state.ActiveNodes, before).SequenceEqual(Ancestors(draft, after), StringComparer.Ordinal))
                    throw new CategoryConflictException("Import thay cấu trúc đang có Asset; dùng preview/plan TASK-008 để xử lý nguyên tử.");
            }
        }
        if (!alreadyImported && inventoryStore is not null)
        {
            var inventory = await inventoryStore.ReadAsync(token).ConfigureAwait(false);
            foreach (var item in inventory.Items.Where(i => i.CategoryId.Length > 0))
            {
                var before = state.ActiveNodes.SingleOrDefault(n => n.CategoryId == item.CategoryId);
                var after = draft.SingleOrDefault(n => n.CategoryId == item.CategoryId);
                if (before is null || after is null || before.State != after.State || !Ancestors(state.ActiveNodes, before).SequenceEqual(Ancestors(draft, after), StringComparer.Ordinal))
                    throw new CategoryConflictException("Import thay cấu trúc đang có Item; chưa đối soát registry TASK-008, không thể bỏ qua guard.");
            }
        }
        if (!alreadyImported && registry?.Media is { } media)
        {
            foreach (var doc in media.Documents)
            {
                var before = state.ActiveNodes.SingleOrDefault(n => n.CategoryId == doc.CategoryId);
                var after = draft.SingleOrDefault(n => n.CategoryId == doc.CategoryId);
                if (before is null || after is null || before.State != after.State || !Ancestors(state.ActiveNodes, before).SequenceEqual(Ancestors(draft, after), StringComparer.Ordinal))
                    throw new CategoryConflictException("Import thay cấu trúc có hồ sơ/phiên bản/file; dùng plan registry nguyên tử, không bỏ qua guard.");
            }
        }
        if (!alreadyImported && registry?.Inventory?.Jobs.Any(j => j.State != "COMPLETE") == true)
            throw new CategoryConflictException("Hoàn tất import Items đang dở trước đổi tree version.");
        var ids = draft.ToDictionary(n => n.CategoryId, n => n.CategoryId.StartsWith("NEW:", StringComparison.Ordinal)
            ? "CAT-" + Guid.NewGuid().ToString("N") : n.CategoryId, StringComparer.Ordinal);
        draft = draft.Select(n => n with { CategoryId = ids[n.CategoryId], ParentId = n.ParentId is null ? null : ids[n.ParentId] }).ToArray();
        candidate.Categories.AddRange(draft);
        if (!alreadyImported) candidate.Versions.Add(new(versionId, "DRAFT", document.SourceHash, document.MappingVersion, requestHash, clock.GetUtcNow(), actor));
        candidate.Operations.Add(new("IMPORT-" + Guid.NewGuid().ToString("N"), idempotencyKey, actor, requestHash, versionId, document.Rows.Count, clock.GetUtcNow(), document.SourceHash, document.MappingVersion, alreadyImported));
        if (!alreadyImported) candidate.ActiveTreeVersionId = versionId; candidate.Revision++;
        await store.CommitAsync(candidate, state.Revision, token).ConfigureAwait(false);
        return new CategoryImportResult(versionId, candidate.Revision, alreadyImported, document.Rows.Count);
    }, ct);
    public Task<CategoryList> ListAsync(string actor, string? search = null, bool export = false, CancellationToken ct = default) => writer.ReadAsync(t => ListCore(actor, search, export, t), ct);
    private async Task<CategoryList> ListCore(string actor, string? search, bool export, CancellationToken ct)
    {
        if (search?.Length > 200) throw new CategoryValidationException("Từ khóa không vượt 200 ký tự.");
        var auth = await authStore.ReadAsync(s => s.Clone(), ct).ConfigureAwait(false);
        var permission = export ? "CATEGORIES_EXPORT" : "CATEGORIES_READ";
        RequirePermission(auth, actor, permission);
        var state = await store.ReadAsync(ct).ConfigureAwait(false);
        return ListSnapshot(auth, actor, permission, state, search);
    }
    private CategoryList ListSnapshot(AuthSystemState auth, string actor, string permission, CategoryCatalogState state, string? search)
    {
        var nodes = state.ActiveNodes; var hierarchy = Hierarchies(nodes);
        var visible = nodes.Where(n => n.State == "DRAFT" && Allowed(auth, actor, permission, n.CategoryId, hierarchy)).ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
        var views = Ordered(nodes).Where(n => visible.ContainsKey(n.CategoryId)).Select(n => View(n, visible)).ToArray();
        var query = Normalize(search ?? "");
        if (query.Length > 0) views = views.Where(v => Normalize(v.Name + " " + v.SegmentCode + " " + v.FullCode + " " + v.Definition).Contains(query, StringComparison.Ordinal)).ToArray();
        return new(state.Revision, state.ActiveTreeVersionId, views.Length, views);
    }
    public async Task<CategoryView> DetailAsync(string actor, string id, CancellationToken ct = default) =>
        (await ListAsync(actor, ct: ct).ConfigureAwait(false)).Nodes.SingleOrDefault(n => n.CategoryId == id) ?? throw new CategoryNotFoundException();
    public async Task<byte[]> ExportAsync(string actor, CancellationToken ct = default)
    {
        var list = await ListAsync(actor, export: true, ct: ct).ConfigureAwait(false);
        var text = new StringBuilder("Cây danh mục DRAFT — chỉ các nhánh được phép\nSTT nguồn | Tên | Mã đoạn | Mã đường dẫn hiện tại | Đường dẫn được phép | Trạng thái\n");
        foreach (var n in list.Nodes)
            text.Append(' ', n.VisibleDepth * 2).Append(n.PathIsPartial ? "[nhánh được phép] " : "")
                .Append(n.SourceStt).Append(" | ").Append(n.Name).Append(" | ").Append(n.SegmentCode)
                .Append(" | ").Append(n.FullCode).Append(" | ").Append(n.Path).Append(" | DRAFT\n");
        return new UTF8Encoding(false).GetBytes(text.ToString());
    }
    private CategoryPreview Prepare(AuthSystemState auth, string actor, CategoryCatalogState state, CategoryDocument doc)
    {
        var issues = Validate(doc.Rows);
        var duplicates = doc.Rows.GroupBy(r => r.Name.Trim(), StringComparer.Ordinal)
            .Where(g => g.Select(r => r.SegmentCode).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => new CategoryDuplicateName(g.Key, g.Select(r => r.SegmentCode).Distinct(StringComparer.Ordinal).ToArray())).ToArray();
        if (issues.Count > 0) return Preview([], issues);
        var nodes = BuildNodes(state, doc.Rows, "PREVIEW");
        var oldNodes = state.ActiveNodes; var oldHierarchy = Hierarchies(oldNodes); var newHierarchy = Hierarchies(nodes);
        if (oldNodes.Any(n => !Allowed(auth, actor, "CATEGORIES_IMPORT", n.CategoryId, oldHierarchy)) ||
            nodes.Any(n => !Allowed(auth, actor, "CATEGORIES_IMPORT", n.CategoryId, newHierarchy))) throw new CategoryNotFoundException();
        foreach (var node in nodes)
        {
            var old = oldNodes.SingleOrDefault(n => n.CategoryId == node.CategoryId);
            if (old is not null && old.ParentId != node.ParentId &&
                !MoveAllowedAcrossVersions(auth, actor, old.CategoryId, node.ParentId ?? node.CategoryId, oldHierarchy, newHierarchy)) throw new CategoryNotFoundException();
        }
        var diff = new List<CategoryDiff>();
        foreach (var n in nodes)
        {
            var old = oldNodes.SingleOrDefault(o => o.CategoryId == n.CategoryId);
            if (old is null || !SameData(old, n)) diff.Add(new(old is null ? "ADD" : "CHANGE", n.CategoryId, n.Name, old?.FullCode, n.FullCode, old?.Definition, n.Definition));
        }
        diff.AddRange(oldNodes.Where(o => !nodes.Any(n => n.CategoryId == o.CategoryId))
            .Select(o => new CategoryDiff("REMOVE_FROM_VERSION", o.CategoryId, o.Name, o.FullCode, null, o.Definition, null)));
        return Preview(diff, issues);
        CategoryPreview Preview(IReadOnlyList<CategoryDiff> diff, IReadOnlyList<CategoryIssue> errors) => new(
            state.Revision, Hash(DocumentHash(doc) + ":" + state.Revision), doc.SourceHash,
            doc.Rows, errors, duplicates, Warnings(doc, duplicates.Length), diff, doc.Rows.Count,
            doc.Rows.Count(r => r.Level == 1), doc.Rows.Count(r => r.Level == 2), doc.Rows.Count(r => r.Level == 3));
    }
    public static IReadOnlyList<CategoryIssue> Validate(IReadOnlyList<CategoryInput> rows)
    {
        var errors = new List<CategoryIssue>();
        void Add(CategoryInput r, string code, string message) => errors.Add(new(code, r.SourceSheet, r.SourceRow, message));
        if (rows.Count == 0 || rows.Count > CategoryImportReader.MaxNodes) errors.Add(new("ROW_COUNT", "", 0, "Snapshot phải có 1–1000 nút."));
        foreach (var r in rows)
        {
            if (r.SourceRow < 2 || r.SourceSheet != "F1-F3 Mã ngang" || r.SourceSheet.Length > 100 || r.SourceSheet.Any(char.IsControl)) Add(r, "PROVENANCE", "Sheet/dòng nguồn không hợp lệ.");
            if (r.SourceStt.Length > 30 || !CategoryImportReader.SttPattern().IsMatch(r.SourceStt)) Add(r, "STT", "STT cấu trúc phải có dạng 1 / 1.1 / 1.1.1.");
            if (r.Level < 1 || r.Level > 3 || r.Level != r.SourceStt.Split('.').Length) Add(r, "LEVEL", "Cấp F1/F2/F3 không khớp STT; không tự tạo F4 từ ghi chú.");
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 300 || r.Name.Any(char.IsControl)) Add(r, "NAME", "Tên bắt buộc, tối đa 300 ký tự và không có ký tự điều khiển.");
            if (!CodePattern().IsMatch(r.SegmentCode)) Add(r, "CODE", "Mã đoạn phải gồm 3 ký tự A-Z/0-9, bắt đầu bằng A-Z.");
            if (r.Definition.Length > 4000 || r.Definition.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) Add(r, "DEFINITION", "Definition tối đa 4000 ký tự, không chứa ký tự điều khiển không hợp lệ.");
            var expectedParent = r.SourceStt.Contains('.') ? r.SourceStt[..r.SourceStt.LastIndexOf('.')] : null;
            if (r.ParentSourceStt != expectedParent) Add(r, "PARENT_LEVEL", "Cha phải là STT cấp liền trước trong staging.");
        }
        foreach (var g in rows.GroupBy(r => r.SourceStt).Where(g => g.Count() > 1)) foreach (var r in g) Add(r, "DUPLICATE_STT", "STT bị trùng.");
        foreach (var g in rows.GroupBy(r => r.SegmentCode).Where(g => g.Count() > 1)) foreach (var r in g) Add(r, "DUPLICATE_CODE", "Mã nguồn bị trùng; cần mapping rõ, không overwrite.");
        foreach (var g in rows.Where(r => r.CategoryId is not null).GroupBy(r => r.CategoryId).Where(g => g.Count() > 1)) foreach (var r in g) Add(r, "DUPLICATE_ID", "Hai dòng không thể map cùng CategoryId.");
        var unique = rows.GroupBy(r => r.SourceStt).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (r.ParentSourceStt is not null && !unique.ContainsKey(r.ParentSourceStt)) Add(r, "MISSING_PARENT", "Không tìm thấy dòng cha.");
            var visited = new HashSet<string>(StringComparer.Ordinal) { r.SourceStt }; var parent = r.ParentSourceStt;
            while (parent is not null && unique.TryGetValue(parent, out var node))
            { if (!visited.Add(parent)) { Add(r, "CYCLE", "Cây có chu trình."); break; } parent = node.ParentSourceStt; }
        }
        return errors;
    }
    private static CategoryNode[] BuildNodes(CategoryCatalogState state, IReadOnlyList<CategoryInput> rows, string versionId)
    {
        var known = state.Categories.SelectMany(n => new[] { new KeyValuePair<string, string>(n.SourceKey, n.CategoryId), new KeyValuePair<string, string>(CategoryImportReader.MappingVersion + ":" + n.SegmentCode, n.CategoryId) }).GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
        var byStt = rows.ToDictionary(r => r.SourceStt, StringComparer.Ordinal); var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            var key = SourceKey(r);
            if (r.CategoryId is not null && !state.Categories.Any(n => n.CategoryId == r.CategoryId)) throw new CategoryValidationException($"Dòng {r.SourceRow}: CategoryId mapping không tồn tại.");
            if (r.CategoryId is not null && known.TryGetValue(key, out var knownId) && knownId != r.CategoryId) throw new CategoryConflictException("Mã nguồn đã map tới CategoryId khác; không tự remap.");
            mapped[r.SourceStt] = r.CategoryId ?? known.GetValueOrDefault(key, "NEW:" + r.SegmentCode);
        }
        if (mapped.Values.Distinct(StringComparer.Ordinal).Count() != rows.Count) throw new CategoryConflictException("Mapping tạo trùng CategoryId; cần giải quyết trước import.");
        return rows.Select((r, order) =>
        {
            var parts = new List<string> { r.SegmentCode }; var parent = r.ParentSourceStt;
            while (parent is not null) { var p = byStt[parent]; parts.Insert(0, p.SegmentCode); parent = p.ParentSourceStt; }
            var old = state.ActiveNodes.SingleOrDefault(n => n.CategoryId == mapped[r.SourceStt]);
            var result = new CategoryNode(mapped[r.SourceStt], versionId, r.ParentSourceStt is null ? null : mapped[r.ParentSourceStt], r.Level, order, r.Name, r.SegmentCode,
                string.Join('.', parts), r.Definition, old?.State ?? "DRAFT", old?.Version ?? 1, SourceKey(r), r.SourceSheet, r.SourceRow, r.SourceStt);
            return old is not null && !SameData(old, result) ? result with { Version = old.Version + 1 } : result;
        }).ToArray();
    }
    private bool MoveAllowedAcrossVersions(AuthSystemState auth, string actor, string source, string destination, ScopeHierarchySet before, ScopeHierarchySet after)
    {
        foreach (var assignment in auth.RoleAssignments.Where(a => a.UserId == actor))
        {
            var isolated = auth.Clone(); isolated.RoleAssignments.RemoveAll(a => a.AssignmentId != assignment.AssignmentId);
            if (Allowed(isolated, actor, "CATEGORIES_MOVE", source, before) && Allowed(isolated, actor, "CATEGORIES_MOVE", destination, after)) return true;
        }
        return false;
    }
    private static IReadOnlyList<string> Warnings(CategoryDocument doc, int duplicates)
    {
        var warnings = doc.Warnings.ToList();
        var f1 = doc.Rows.Count(r => r.Level == 1); var f2 = doc.Rows.Count(r => r.Level == 2); var f3 = doc.Rows.Count(r => r.Level == 3);
        if (doc.Rows.Count != 665 || f1 != 15 || f2 != 98 || f3 != 552 || duplicates != 39)
            warnings.Add($"Chênh baseline: tổng {doc.Rows.Count - 665:+0;-0;0}, F1 {f1 - 15:+0;-0;0}, F2 {f2 - 98:+0;-0;0}, F3 {f3 - 552:+0;-0;0}, tên nhiều mã {duplicates - 39:+0;-0;0}. Giữ dữ liệu file; cần đối chiếu nguồn authoritative.");
        return warnings;
    }
    private static bool SameData(CategoryNode a, CategoryNode b) => a.ParentId == b.ParentId && a.Name == b.Name && a.SegmentCode == b.SegmentCode && a.FullCode == b.FullCode &&
        a.Definition == b.Definition && a.FLevel == b.FLevel && a.DisplayOrder == b.DisplayOrder && a.SourceStt == b.SourceStt && a.SourceRow == b.SourceRow && a.SourceSheet == b.SourceSheet && a.SourceKey == b.SourceKey && a.State == b.State;
    private static IEnumerable<string> Ancestors(CategoryNode[] nodes, CategoryNode start)
    {
        var byId = nodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal); var parent = start.ParentId;
        while (parent is not null) { yield return parent; parent = byId[parent].ParentId; }
    }
    private static string SourceKey(CategoryInput r) => CategoryImportReader.MappingVersion + ":" + r.SegmentCode;
    private static ScopeHierarchySet Hierarchies(IEnumerable<CategoryNode> nodes) => new(ScopeHierarchy.Create(AuthScopeType.OrgUnit, []), ScopeHierarchy.Create(AuthScopeType.Location, []),
        ScopeHierarchy.Create(AuthScopeType.CategorySubtree, nodes.Select(n => new ScopeNode(n.CategoryId, n.ParentId))));
    private static ResourceScope Resource(string id) => new(new(false, null), new(false, null), new(true, id));
    private bool Allowed(AuthSystemState state, string actor, string permission, string id, ScopeHierarchySet hierarchy) => authorization.IsAllowed(state, actor, permission, Resource(id), hierarchy, clock.GetUtcNow());
    private void RequirePermission(AuthSystemState state, string actor, string permission)
    {
        if (state.Users.All(u => u.UserId != actor || u.State != AuthUserState.Active || u.MustChangePassword) ||
            !state.RoleAssignments.Any(a => a.UserId == actor && AuthAuthorizationRules.IsEffective(a, clock.GetUtcNow()) && AuthRoleCatalog.HasPermission(a.RoleCode, permission))) throw new CategoryForbiddenException();
    }
    private static IEnumerable<CategoryNode> Ordered(CategoryNode[] nodes)
    {
        var children = nodes.ToLookup(n => n.ParentId ?? "", StringComparer.Ordinal);
        foreach (var root in Walk("")) yield return root;
        IEnumerable<CategoryNode> Walk(string parent)
        { foreach (var n in children[parent].OrderBy(n => n.DisplayOrder)) { yield return n; foreach (var child in Walk(n.CategoryId)) yield return child; } }
    }
    private static CategoryView View(CategoryNode n, Dictionary<string, CategoryNode> visible)
    {
        var chain = new List<CategoryNode> { n }; var parent = n.ParentId;
        while (parent is not null && visible.TryGetValue(parent, out var p)) { chain.Insert(0, p); parent = p.ParentId; }
        return new(n.CategoryId, n.ParentId is not null && visible.ContainsKey(n.ParentId) ? n.ParentId : null, n.FLevel, chain.Count - 1,
            n.Name, n.SegmentCode, string.Join('.', chain.Select(p => p.SegmentCode)), string.Join(" / ", chain.Select(p => p.Name)), parent is not null,
            n.Definition, n.State, n.Version, n.SourceSheet, n.SourceRow, n.SourceStt);
    }
    private static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Replace('đ', 'd').Replace('Đ', 'D').ToUpperInvariant().Normalize(NormalizationForm.FormC);
    private static string DocumentHash(CategoryDocument doc) => Hash(JsonSerializer.Serialize(new { doc.SourceHash, doc.MappingVersion, doc.Rows }));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    [GeneratedRegex("^[A-Z][A-Z0-9]{2}$", RegexOptions.CultureInvariant)] private static partial Regex CodePattern();
}
