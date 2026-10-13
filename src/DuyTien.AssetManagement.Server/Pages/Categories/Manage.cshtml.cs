using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.Authorization;
namespace DuyTien.AssetManagement.Server.Pages.Categories;
[Authorize(Roles = "Admin,CatalogManager")]
public sealed class ManageModel(TreeMutationService service, CategoryCatalogService catalog, AssetCrudService assets) : TreeChangePageModel(service, catalog, assets, false);
