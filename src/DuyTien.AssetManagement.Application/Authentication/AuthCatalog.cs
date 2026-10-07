namespace DuyTien.AssetManagement.Application.Authentication;

public static class AuthRole
{
    public const string Viewer = "Viewer";
    public const string AssetEditor = "AssetEditor";
    public const string CatalogManager = "CatalogManager";
    public const string DocumentManager = "DocumentManager";
    public const string Admin = "Admin";

    public static IReadOnlyList<string> All { get; } =
        [Viewer, AssetEditor, CatalogManager, DocumentManager, Admin];
}

public static class AuthPermission
{
    public const string AssetsRead = "ASSETS_READ";
    public const string AssetsCreate = "ASSETS_CREATE";
    public const string AssetsUpdate = "ASSETS_UPDATE";
    public const string AssetsDelete = "ASSETS_DELETE";
    public const string AssetsRestore = "ASSETS_RESTORE";
    public const string AssetsMove = "ASSETS_MOVE";
    public const string AssetFilesDownload = "ASSET_FILES_DOWNLOAD";
    public const string AssetFilesUpload = "ASSET_FILES_UPLOAD";
    public const string AssetFilesDelete = "ASSET_FILES_DELETE";
    public const string CategoriesRead = "CATEGORIES_READ";
    public const string CategoriesCreate = "CATEGORIES_CREATE";
    public const string CategoriesUpdate = "CATEGORIES_UPDATE";
    public const string CategoriesDelete = "CATEGORIES_DELETE";
    public const string CategoriesMove = "CATEGORIES_MOVE";
    public const string CategoriesImport = "CATEGORIES_IMPORT";
    public const string CategoriesExport = "CATEGORIES_EXPORT";
    public const string ItemsRead = "ITEMS_READ";
    public const string StockRead = "STOCK_READ";
    public const string ItemsImport = "ITEMS_IMPORT";
    public const string StockUpdate = "STOCK_UPDATE";
    public const string DocumentsRead = "DOCUMENTS_READ";
    public const string DocumentsDownload = "DOCUMENTS_DOWNLOAD";
    public const string DocumentsManage = "DOCUMENTS_MANAGE";
    public const string ReportsRead = "REPORTS_READ";
    public const string ReportsExport = "REPORTS_EXPORT";
    public const string UsersRead = "USERS_READ";
    public const string UsersManage = "USERS_MANAGE";
    public const string AuthzAssign = "AUTHZ_ASSIGN";
    public const string AuditRead = "AUDIT_READ";

    public static IReadOnlyList<string> All { get; } =
    [
        AssetsRead, AssetsCreate, AssetsUpdate, AssetsDelete, AssetsRestore, AssetsMove,
        AssetFilesDownload, AssetFilesUpload, AssetFilesDelete,
        CategoriesRead, CategoriesCreate, CategoriesUpdate, CategoriesDelete,
        CategoriesMove, CategoriesImport, CategoriesExport,
        ItemsRead, StockRead, ItemsImport, StockUpdate,
        DocumentsRead, DocumentsDownload, DocumentsManage,
        ReportsRead, ReportsExport, UsersRead, UsersManage, AuthzAssign, AuditRead
    ];
}

public static class AuthRoleCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Permissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [AuthRole.Viewer] = Set(
                AuthPermission.AssetsRead, AuthPermission.AssetFilesDownload,
                AuthPermission.CategoriesRead, AuthPermission.ItemsRead, AuthPermission.StockRead,
                AuthPermission.DocumentsRead, AuthPermission.DocumentsDownload,
                AuthPermission.ReportsRead),
            [AuthRole.AssetEditor] = Set(
                AuthPermission.AssetsRead, AuthPermission.AssetsCreate, AuthPermission.AssetsUpdate,
                AuthPermission.AssetsDelete, AuthPermission.AssetsRestore, AuthPermission.AssetsMove,
                AuthPermission.AssetFilesDownload, AuthPermission.AssetFilesUpload,
                AuthPermission.AssetFilesDelete, AuthPermission.CategoriesRead,
                AuthPermission.ItemsRead, AuthPermission.StockRead, AuthPermission.DocumentsRead,
                AuthPermission.DocumentsDownload, AuthPermission.ReportsRead,
                AuthPermission.ReportsExport),
            [AuthRole.CatalogManager] = Set(
                AuthPermission.AssetsRead, AuthPermission.AssetFilesDownload,
                AuthPermission.CategoriesRead, AuthPermission.CategoriesCreate,
                AuthPermission.CategoriesUpdate, AuthPermission.CategoriesDelete,
                AuthPermission.CategoriesMove, AuthPermission.CategoriesImport,
                AuthPermission.CategoriesExport, AuthPermission.ItemsRead,
                AuthPermission.StockRead, AuthPermission.ItemsImport, AuthPermission.StockUpdate,
                AuthPermission.DocumentsRead, AuthPermission.DocumentsDownload,
                AuthPermission.ReportsRead, AuthPermission.ReportsExport),
            [AuthRole.DocumentManager] = Set(
                AuthPermission.AssetsRead, AuthPermission.AssetFilesDownload,
                AuthPermission.CategoriesRead, AuthPermission.ItemsRead, AuthPermission.StockRead,
                AuthPermission.DocumentsRead, AuthPermission.DocumentsDownload,
                AuthPermission.DocumentsManage, AuthPermission.ReportsRead,
                AuthPermission.ReportsExport),
            [AuthRole.Admin] = new HashSet<string>(AuthPermission.All, StringComparer.Ordinal)
        };

    public static bool RoleExists(string roleCode) => Permissions.ContainsKey(roleCode);

    public static bool HasPermission(string roleCode, string permission) =>
        Permissions.TryGetValue(roleCode, out var permissions) && permissions.Contains(permission);

    public static IReadOnlySet<string> GetPermissions(string roleCode) =>
        Permissions.TryGetValue(roleCode, out var permissions)
            ? permissions
            : throw new AuthValidationException($"Role '{roleCode}' không tồn tại.");

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
