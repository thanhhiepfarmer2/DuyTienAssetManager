using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Infrastructure.Local;
namespace DuyTien.AssetManagement.Server.LocalOffline;
// Explicit operator action. Never called on startup; no embedded company data or inferred code policy.
public sealed class LocalDemoInitializer(LocalWorkspace workspace, CategoryCatalogService categories, IAuthSystemStore auth, TimeProvider clock)
{
    public async Task InitializeAsync(string actor, CancellationToken ct = default)
    {
        var state = await auth.ReadAsync(s => s.Clone(), ct); var user = state.Users.SingleOrDefault(u => u.UserId == actor);
        if (user is null || user.MustChangePassword || !AuthAuthorizationRules.IsActiveAdmin(state, user, clock.GetUtcNow())) throw new CategoryForbiddenException();
        var tree = await ((ICategoryCatalogStore)workspace).ReadAsync(ct);
        if (tree.ActiveTreeVersionId is not null && !tree.Operations.Any(o => o.IdempotencyKey == "local-demo-tree-v1")) throw new CategoryConflictException("Workspace đã có cây khác. Không overwrite hoặc gắn nhãn GIẢ cho dữ liệu chưa xác định.");
        CategoryInput[] rows = [new("F1-F3 Mã ngang", 2, "1", null, 1, "GIẢ máy demo local", "FKA", "FAKE_TASK015_V1"), new("F1-F3 Mã ngang", 3, "1.1", "1", 2, "GIẢ thiết bị demo local", "FKB", "FAKE_TASK015_V1"), new("F1-F3 Mã ngang", 4, "2", null, 1, "GIẢ nhánh đích local", "FKC", "FAKE_TASK015_V1")];
        if (tree.ActiveTreeVersionId is null)
        {
            var document = new CategoryDocument("FAKE_TASK015_V1", CategoryImportReader.MappingVersion, rows, ["GIẢ; nguồn/rule công ty OPEN/TBD"]); var preview = await categories.PreviewAsync(actor, document, ct);
            if (!preview.CanCommit) throw new CategoryValidationException("Không thể khởi tạo cây giả; cần đối soát."); await categories.ImportAsync(actor, document, preview.Revision, preview.PreviewHash, "local-demo-tree-v1", true, ct);
        }
        tree = await ((ICategoryCatalogStore)workspace).ReadAsync(ct);
        var roots = tree.ActiveNodes.Where(n => n.ParentId is null && n.Definition == "FAKE_TASK015_V1").Select(n => n.CategoryId).ToArray(); if (roots.Length != 2) throw new CategoryConflictException("Cây demo đã đổi; không seed reference ngầm.");
        var assets = await ((IAssetCatalogStore)workspace).ReadAsync(ct);
        if (assets.OrgUnits.Count == 0 && assets.Locations.Count == 0 && assets.Rules.Count == 0) await workspace.InitializeDemoReferencesAsync(roots, ct);
        else if (assets.OrgUnits.Count != 1 || assets.OrgUnits[0].Id != "OU-FAKE" || assets.Locations.Count != 1 || assets.Locations[0].Id != "LOC-FAKE" || assets.Rules.Count != 2 || assets.Rules.Any(r => !roots.Contains(r.CategoryRootId) || r.Prefix != "LOCAL" || !r.ApprovedForDemo || r.Status != "DEMO")) throw new CategoryConflictException("Reference đã có khác fixture; không overwrite.");
    }
}
