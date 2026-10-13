using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
namespace DuyTien.AssetManagement.Server.Pages.Assets;
[Authorize(Roles = "Admin,AssetEditor")]
public sealed class MoveModel(TreeMutationService service, CategoryCatalogService catalog, AssetCrudService assets) : TreeChangePageModel(service, catalog, assets, true);
