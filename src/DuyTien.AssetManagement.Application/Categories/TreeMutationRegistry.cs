using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
namespace DuyTien.AssetManagement.Application.Categories;

public sealed partial class TreeMutationService
{
    private sealed record RegistryPlan(TreeRegistryImpact[] Impacts, ItemRecord[] ChangedItems, LibraryDocument[] ChangedDocuments);
    private RegistryPlan PrepareRegistry(AuthSystemState auth, string actor, TreeMutationSnapshot s, TreeMutationRequest request,
        CategoryNode[] next, HashSet<string> affected, AssetRecord[] affectedAssets, List<AssetRecord> changedAssets, List<string> issues)
    {
        var before = Hierarchies(s.Assets, s.Categories.ActiveNodes); var after = CategoryTreeShape.Validate(next).Count == 0 ? Hierarchies(s.Assets, next) : before;
        var items = (s.Inventory?.Items ?? []).Where(i => affected.Contains(i.CategoryId)).OrderBy(i => i.ItemId, StringComparer.Ordinal).ToArray();
        var documents = (s.Media?.Documents ?? []).Where(d => affected.Contains(d.CategoryId)).OrderBy(d => d.DocumentId, StringComparer.Ordinal).ToArray();
        var changedItems = new List<ItemRecord>(); var changedDocuments = new List<LibraryDocument>(); var impacts = new List<TreeRegistryImpact>();
        var relocate = request.Kind == "ARCHIVE_CATEGORY" && request.DeletePlan == "RELOCATE";
        var archive = request.Kind == "ARCHIVE_CATEGORY" && request.DeletePlan == "SOFT_DELETE";
        var moving = request.Kind is "MOVE_CATEGORY" or "MOVE_ASSETS" || relocate || archive;
        string TargetCategory(string old) => relocate ? request.DestinationCategoryId! : old;
        void Require(string read, string change, ResourceScope source, ResourceScope target)
        {
            if (!Allowed(auth, actor, read, source, before) || !Across(auth, actor, change, source, target, before, after))
                throw new CategoryNotFoundException(); // Whole plan refused before any counters/names escape.
        }
        foreach (var item in items)
        {
            var target = TargetCategory(item.CategoryId);
            Require("ITEMS_READ", moving ? "ITEMS_IMPORT" : "ITEMS_READ", CategoryScope(item.CategoryId), CategoryScope(target));
            impacts.Add(new("ITEM", item.ItemId, item.Name, item.Version, item.CategoryId, target));
            if (relocate && target != item.CategoryId) changedItems.Add(item with { CategoryId = target, Version = item.Version + 1 });
            foreach (var b in (s.Inventory?.Balances ?? []).Where(b => b.ItemId == item.ItemId).OrderBy(b => b.BalanceId, StringComparer.Ordinal))
            {
                var location = s.Assets.Locations.Single(l => l.Id == b.LocationId);
                Require("STOCK_READ", moving ? "STOCK_UPDATE" : "STOCK_READ", ResourceScope.Required(location.OrgUnitId, b.LocationId, item.CategoryId), ResourceScope.Required(location.OrgUnitId, b.LocationId, target));
                impacts.Add(new("STOCK", b.BalanceId, item.Name + " / " + b.Unit, b.Version, item.CategoryId, target,
                    BeforeOrgUnitId: location.OrgUnitId, BeforeLocationId: b.LocationId, AfterOrgUnitId: location.OrgUnitId, AfterLocationId: b.LocationId));
            }
        }
        foreach (var document in documents)
        {
            var target = TargetCategory(document.CategoryId);
            // Draft/archived registry metadata is a manager surface, not Viewer unpublished access.
            Require("DOCUMENTS_READ", "DOCUMENTS_MANAGE", CategoryScope(document.CategoryId), CategoryScope(target));
            var hidden = document.IsDeleted || archive;
            impacts.Add(new("DOCUMENT", document.DocumentId, document.Name, document.Version, document.CategoryId, target, hidden));
            if (target != document.CategoryId || hidden != document.IsDeleted) changedDocuments.Add(document with { CategoryId = target, IsDeleted = hidden });
            foreach (var version in (s.Media?.Versions ?? []).Where(v => v.DocumentId == document.DocumentId).OrderBy(v => v.VersionId, StringComparer.Ordinal))
            {
                impacts.Add(new("VERSION", version.VersionId, document.Name + " / " + version.Label, version.Version, document.CategoryId, target, hidden));
                foreach (var file in (s.Media?.Files ?? []).Where(f => f.OwnerType == "DOCUMENT_VERSION" && f.OwnerId == version.VersionId).OrderBy(f => f.FileId, StringComparer.Ordinal))
                {
                    Require("DOCUMENTS_DOWNLOAD", "DOCUMENTS_MANAGE", CategoryScope(document.CategoryId), CategoryScope(target));
                    impacts.Add(new(file.Kind, file.FileId, file.FileName + " / " + file.State, file.Version, document.CategoryId, target, hidden || file.IsDeleted));
                }
            }
        }
        var owners = affectedAssets.ToDictionary(a => a.AssetId, StringComparer.Ordinal);
        foreach (var file in (s.Media?.Files ?? []).Where(f => f.OwnerType == "ASSET" && owners.ContainsKey(f.OwnerId)).OrderBy(f => f.FileId, StringComparer.Ordinal))
        {
            var source = owners[file.OwnerId]; var target = changedAssets.SingleOrDefault(a => a.AssetId == source.AssetId) ?? source;
            Require("ASSET_FILES_DOWNLOAD", moving ? "ASSET_FILES_UPLOAD" : "ASSET_FILES_DOWNLOAD", AssetScope(source), AssetScope(target));
            impacts.Add(new("PHOTO", file.FileId, file.FileName + " / " + file.State, file.Version, source.CategoryId, target.CategoryId, target.IsDeleted || file.IsDeleted,
                source.OrgUnitId, source.LocationId, target.OrgUnitId, target.LocationId));
        }
        if (request.Kind == "ARCHIVE_CATEGORY" && request.DeletePlan == "EMPTY_ONLY" && impacts.Count > 0)
            issues.Add("Nhánh còn Item/snapshot/hồ sơ/ảnh hoặc lịch sử; không là danh mục rỗng. Chọn phương án đối soát.");
        if (archive && items.Length > 0) issues.Add("Item chưa có quy tắc xóa/ngừng được duyệt. Chọn RELOCATE để giữ Item/snapshot, không tự xóa.");
        if (next.Any(n => Different(s.Categories.ActiveNodes.Single(old => old.CategoryId == n.CategoryId), n)) && s.Inventory?.Jobs.Any(j => j.State != "COMPLETE") == true)
            issues.Add("Có import Items chưa hoàn tất; hoàn tất job trước thay cây để không mất checkpoint TreeVersion.");
        return new(impacts.ToArray(), changedItems.ToArray(), changedDocuments.ToArray());
    }
    private static (InventoryState? Inventory, MediaCatalogState? Media) ApplyRegistry(TreeMutationSnapshot current, RegistryPlan plan,
        string opId, string actor, string hash, string reason, DateTimeOffset now)
    {
        var inventory = current.Inventory?.Clone(); var media = current.Media?.Clone();
        if (plan.ChangedItems.Length > 0)
        {
            if (inventory is null) throw new CategoryConflictException("Item registry chưa được đối soát.");
            InventoryIntegrity.Apply(inventory, new(opId + ":ITEMS", opId + ":ITEMS", actor, hash, "ITEM_RECLASSIFY", reason, now, null, [], plan.ChangedItems, null)); inventory.Revision++;
            InventoryIntegrity.Validate(inventory);
        }
        foreach (var document in plan.ChangedDocuments)
        {
            if (media is null) throw new CategoryConflictException("Document registry chưa được đối soát.");
            var before = media.Documents.Single(d => d.DocumentId == document.DocumentId);
            var saved = document with { Version = before.Version + 1, UpdatedAt = now, UpdatedBy = actor };
            media.Apply(new(opId + ":DOC:" + saved.DocumentId, opId + ":DOC:" + saved.DocumentId, actor, hash,
                before.CategoryId != saved.CategoryId ? "DOCUMENT_RELOCATE" : "DOCUMENT_ARCHIVE", reason, now, [], saved));
        }
        if (media is not null) MediaCatalogIntegrity.Validate(media);
        return (inventory, media);
    }
    private void RecheckRegistryReplay(AuthSystemState auth, string actor, TreeMutationSnapshot s, TreeChangeOperation op)
    {
        var oldNodes = s.Categories.Categories.Where(n => n.TreeVersionId == op.BeforeTreeVersionId).ToArray();
        var savedNodes = s.Categories.Categories.Where(n => n.TreeVersionId == op.TreeVersionId).ToArray();
        var before = Hierarchies(s.Assets, oldNodes); var after = Hierarchies(s.Assets, savedNodes); var current = Hierarchies(s.Assets, s.Categories.ActiveNodes);
        var moving = op.Kind is "MOVE_CATEGORY" or "MOVE_ASSETS" || op.Kind == "ARCHIVE_CATEGORY" && op.Request.DeletePlan != "EMPTY_ONLY";
        foreach (var impact in op.RegistryImpacts ?? [])
        {
            ResourceScope Scope(string category, string? org, string? location) => org is null && location is null ? CategoryScope(category) : ResourceScope.Required(org!, location!, category);
            var source = Scope(impact.BeforeCategoryId, impact.BeforeOrgUnitId, impact.BeforeLocationId);
            var destination = Scope(impact.AfterCategoryId, impact.AfterOrgUnitId, impact.AfterLocationId);
            string permission; ResourceScope fresh;
            switch (impact.Kind)
            {
                case "ITEM":
                    permission = moving ? "ITEMS_IMPORT" : "ITEMS_READ";
                    var item = s.Inventory?.Items.SingleOrDefault(i => i.ItemId == impact.Id) ?? throw new CategoryNotFoundException(); fresh = CategoryScope(item.CategoryId); break;
                case "STOCK":
                    permission = moving ? "STOCK_UPDATE" : "STOCK_READ";
                    var balance = s.Inventory?.Balances.SingleOrDefault(b => b.BalanceId == impact.Id) ?? throw new CategoryNotFoundException();
                    var owner = s.Inventory!.Items.Single(i => i.ItemId == balance.ItemId); var loc = s.Assets.Locations.Single(l => l.Id == balance.LocationId);
                    fresh = ResourceScope.Required(loc.OrgUnitId, balance.LocationId, owner.CategoryId); break;
                case "DOCUMENT":
                case "VERSION":
                case "DOCX":
                case "PDF":
                    permission = "DOCUMENTS_MANAGE";
                    string docId;
                    if (impact.Kind == "DOCUMENT") docId = impact.Id;
                    else { var versionId = impact.Kind == "VERSION" ? impact.Id : s.Media?.Files.SingleOrDefault(f => f.FileId == impact.Id)?.OwnerId ?? throw new CategoryNotFoundException(); docId = s.Media?.Versions.SingleOrDefault(v => v.VersionId == versionId)?.DocumentId ?? throw new CategoryNotFoundException(); }
                    fresh = CategoryScope(s.Media?.Documents.SingleOrDefault(d => d.DocumentId == docId)?.CategoryId ?? throw new CategoryNotFoundException()); break;
                case "PHOTO":
                    permission = moving ? "ASSET_FILES_UPLOAD" : "ASSET_FILES_DOWNLOAD";
                    var file = s.Media?.Files.SingleOrDefault(f => f.FileId == impact.Id) ?? throw new CategoryNotFoundException(); fresh = AssetScope(s.Assets.Assets.SingleOrDefault(a => a.AssetId == file.OwnerId) ?? throw new CategoryNotFoundException()); break;
                default: throw new CategoryNotFoundException();
            }
            if (!Across(auth, actor, permission, source, destination, before, after) || !Allowed(auth, actor, permission, fresh, current)) throw new CategoryNotFoundException();
        }
    }
}
