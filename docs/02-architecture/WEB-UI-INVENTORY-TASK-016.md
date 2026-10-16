# Inventory giao diện/contract — TASK-016

2026-10-15. AI đọc tĩnh source hiện hành; không là runtime/browser/Google PASS. Source baseline HEAD5c4f5ad, worktree đầu lượt sạch.

## Nguồn HTML đã xác minh

File ngoài Git: D:/Dev/01-QuanLyVatTu_DuyTien_v4_Offline.html; 7190714 bytes; SHA256 577423ad5166a11a14597d97650366b2052af366df076c5f6a23bb78e92e22a6, khớp metadata người dùng. Parser HTML tách đúng1stylesheet45899ký tự/1script6585275ký tự. Regex style ban đầu bắt nhầm chuỗi thư viện, không dùng kết quả đó; parser là bằng chứng cuối. Chỉ stylesheet/class/markup UI đọc tĩnh, không chạy Apps Script/dataset.

## Route → fields/actions/access

Mọi read/write nghiệp vụ vẫn service kiểm scope theo [PERMISSIONS](../01-requirements/PERMISSIONS.md); Authorize bên dưới là lớp ngoài. Field names/key/version giữ nguyên; nhãn chỉ đổi presentation.

### Account/AccessDenied.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Account/AccessDenied.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet.
- Access: Kế thừa PageModel/public; Login AllowAnonymous; change/logout phiên có auth/anti-forgery; min8 policy không đổi.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Account/ChangePassword.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Account/ChangePassword.cshtml`; route declaration `@page`.
- Fields (HTML): `Input.ConfirmPassword`, `Input.CurrentPassword`, `Input.NewPassword`.
- Handlers: OnPostAsync.
- Access: [Authorize]; Login AllowAnonymous; change/logout phiên có auth/anti-forgery; min8 policy không đổi.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Account/LocalSetup.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Account/LocalSetup.cshtml`; route declaration `@page`.
- Fields (HTML): `Confirmation`, `Password`.
- Handlers: OnGetAsync, OnPostAsync.
- Access: Kế thừa PageModel/public; Login AllowAnonymous; change/logout phiên có auth/anti-forgery; min8 policy không đổi.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Account/Login.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Account/Login.cshtml`; route declaration `@page`.
- Fields (HTML): `Input.Password`, `Input.RememberMe`, `Input.Username`, `ReturnUrl`.
- Handlers: OnGet, OnPostAsync.
- Access: [AllowAnonymous]; Login AllowAnonymous; change/logout phiên có auth/anti-forgery; min8 policy không đổi.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Account/Logout.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Account/Logout.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet, OnPostAsync.
- Access: [Authorize]; Login AllowAnonymous; change/logout phiên có auth/anti-forgery; min8 policy không đổi.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Admin/Authorization.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Admin/Authorization.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet.
- Access: [Authorize(Roles = AuthRole.Admin)]; USERS_READ/MANAGE/AUTHZ_ASSIGN hoặc AUDIT_READ; Admin; bảo vệ admin cuối, revoke.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Admin/LocalDemo.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Admin/LocalDemo.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet, OnPostAsync.
- Access: [Authorize(Roles = "Admin")]; USERS_READ/MANAGE/AUTHZ_ASSIGN hoặc AUDIT_READ; Admin; bảo vệ admin cuối, revoke.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Admin/SecurityEvents.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Admin/SecurityEvents.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGetAsync.
- Access: [Authorize(Roles = AuthRole.Admin)]; USERS_READ/MANAGE/AUTHZ_ASSIGN hoặc AUDIT_READ; Admin; bảo vệ admin cuối, revoke.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Admin/Users/Detail.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Admin/Users/Detail.cshtml`; route declaration `@page "{id}"`.
- Fields (HTML): `Input.CategoryScopeId`, `Input.DisplayName`, `Input.IncludeDescendants`, `Input.LocationScopeId`, `Input.OrgUnitScopeId`, `Input.RoleCode`.
- Handlers: OnGetAsync, OnPostAssignAsync, OnPostLockAsync, OnPostResetAsync, OnPostUnlockAsync, OnPostUpdateAsync, Assign, Lock, Reset, Unlock, Update.
- Access: [Authorize(Roles = AuthRole.Admin)]; USERS_READ/MANAGE/AUTHZ_ASSIGN hoặc AUDIT_READ; Admin; bảo vệ admin cuối, revoke.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Admin/Users/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Admin/Users/Index.cshtml`; route declaration `@page`.
- Fields (HTML): `Input.DisplayName`, `Input.Username`.
- Handlers: OnGetAsync, OnPostCreateAsync, Create.
- Access: [Authorize(Roles = AuthRole.Admin)]; USERS_READ/MANAGE/AUTHZ_ASSIGN hoặc AUDIT_READ; Admin; bảo vệ admin cuối, revoke.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Assets/Create.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Assets/Create.cshtml`; route declaration `@page`.
- Fields (HTML): `RequestKey`, `TreeVersionId`.
- Handlers: OnGetAsync, OnPostAsync.
- Access: [Authorize(Roles = "Admin,AssetEditor")]; ASSETS_READ/CREATE/UPDATE/DELETE/RESTORE/MOVE; ảnh ASSET_FILES_*; scope ba chiều, move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Assets/Detail.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Assets/Detail.cshtml`; route declaration `@page "{id}"`.
- Fields (HTML): `ExpectedVersion`, `Reason`, `RequestKey`, `TreeVersionId`.
- Handlers: OnGetAsync, OnPostDeleteAsync, OnPostRestoreAsync, OnPostSaveAsync, Save.
- Access: [Authorize]; ASSETS_READ/CREATE/UPDATE/DELETE/RESTORE/MOVE; ảnh ASSET_FILES_*; scope ba chiều, move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Assets/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Assets/Index.cshtml`; route declaration `@page`.
- Fields (HTML): `CategoryId`, `Condition`, `CustodianRef`, `IncludeDeleted`, `IncludeDescendants`, `LocationId`, `OrgUnitId`, `PageSize`.
- Handlers: OnGetAsync.
- Access: [Authorize]; ASSETS_READ/CREATE/UPDATE/DELETE/RESTORE/MOVE; ảnh ASSET_FILES_*; scope ba chiều, move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Assets/Move.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Assets/Move.cshtml`; route declaration `@page "{id?}"`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: GET từ PageModel/base class. TreeChangePageModel: Form.Kind/Reason/CategoryId/DestinationCategoryId/Name/SegmentCode/Definition/DeletePlan/AssetIds/DestinationOrgUnitId/DestinationLocationId; RequestKey/PreviewHash/ExpectedTreeRevision/ExpectedAssetRevision/ConfirmImpact; Preview/Commit.
- Access: [Authorize(Roles = "Admin,AssetEditor")]; ASSETS_READ/CREATE/UPDATE/DELETE/RESTORE/MOVE; ảnh ASSET_FILES_*; scope ba chiều, move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Assets/Photos.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Assets/Photos.cshtml`; route declaration `@page "{id}"`.
- Fields (HTML): `Description`, `ExpectedVersion`, `FileId`, `IsPrimary`, `Reason`, `RequestKey`, `Upload`.
- Handlers: OnGetAsync, OnPostCancelAsync, OnPostDeleteAsync, OnPostReconcileAsync, OnPostUpdateAsync, OnPostUploadAsync, Cancel, Delete, Reconcile, Update, Upload.
- Access: [Authorize,RequestSizeLimit(MediaWeb.RequestCap),RequestFormLimits(MultipartBodyLengthLimit=MediaWeb.RequestCap)]; ASSETS_READ/CREATE/UPDATE/DELETE/RESTORE/MOVE; ảnh ASSET_FILES_*; scope ba chiều, move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Categories/Detail.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Categories/Detail.cshtml`; route declaration `@page "{id}"`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGetAsync.
- Access: [Authorize]; CATEGORIES_READ/IMPORT/EXPORT/UPDATE/MOVE/DELETE; category scope; move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Categories/Import.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Categories/Import.cshtml`; route declaration `@page`.
- Fields (HTML): `ConfirmDuplicateNames`, `PreviewId`, `Upload`.
- Handlers: OnGet, OnPostCommitAsync, OnPostPreviewAsync, Commit, Preview.
- Access: [Authorize(Roles = "Admin,CatalogManager")]; CATEGORIES_READ/IMPORT/EXPORT/UPDATE/MOVE/DELETE; category scope; move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Categories/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Categories/Index.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGetAsync, OnGetExportAsync, Export.
- Access: [Authorize]; CATEGORIES_READ/IMPORT/EXPORT/UPDATE/MOVE/DELETE; category scope; move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Categories/Manage.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Categories/Manage.cshtml`; route declaration `@page "{id?}"`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: GET từ PageModel/base class. TreeChangePageModel: Form.Kind/Reason/CategoryId/DestinationCategoryId/Name/SegmentCode/Definition/DeletePlan/AssetIds/DestinationOrgUnitId/DestinationLocationId; RequestKey/PreviewHash/ExpectedTreeRevision/ExpectedAssetRevision/ConfirmImpact; Preview/Commit.
- Access: [Authorize(Roles = "Admin,CatalogManager")]; CATEGORIES_READ/IMPORT/EXPORT/UPDATE/MOVE/DELETE; category scope; move nguồn+đích.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Documents/Detail.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Documents/Detail.cshtml`; route declaration `@page "{id}"`.
- Fields (HTML): `BusinessCode`, `CategoryId`, `ConfirmDemo`, `Description`, `EffectiveOn`, `ExpectedDocumentVersion`, `ExpectedVersion`, `FileId`, `Kind`, `Label`, `Name`, `Reason`, `RequestKey`, `Upload`, `VersionId`.
- Handlers: OnGetAsync, OnPostArchiveAsync, OnPostCancelAsync, OnPostPublishAsync, OnPostReconcileAsync, OnPostSaveAsync, OnPostUploadAsync, OnPostVersionAsync, Archive, Cancel, Publish, Reconcile, Save, Upload, Version.
- Access: [Authorize,RequestSizeLimit(MediaWeb.RequestCap),RequestFormLimits(MultipartBodyLengthLimit=MediaWeb.RequestCap)]; DOCUMENTS_READ/DOWNLOAD/MANAGE; category scope kế thừa file.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Documents/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Documents/Index.cshtml`; route declaration `@page`.
- Fields (HTML): `BusinessCode`, `CategoryId`, `Description`, `Name`, `RequestKey`.
- Handlers: OnGetAsync, OnPostCreateAsync, Create.
- Access: [Authorize]; DOCUMENTS_READ/DOWNLOAD/MANAGE; category scope kế thừa file.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Error.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Error.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet.
- Access: Kế thừa PageModel/public; Public hoặc links theo phiên; không tự tạo dashboard count.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Files/Content.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Files/Content.cshtml`; route declaration `@page "{id}"`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGetAsync.
- Access: [Authorize]; Theo owner Asset/Document: ASSET_FILES_DOWNLOAD hoặc DOCUMENTS_DOWNLOAD; không URL Drive trực tiếp.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Help/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Help/Index.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet.
- Access: Kế thừa PageModel/public; Public hoặc links theo phiên; không tự tạo dashboard count.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Index.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet.
- Access: Kế thừa PageModel/public; Public hoặc links theo phiên; không tự tạo dashboard count.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Items/Detail.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Items/Detail.cshtml`; route declaration `@page "{id}"`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGetAsync.
- Access: [Authorize]; ITEMS_READ/IMPORT; category scope; staging/chunk idempotent.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Items/Import.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Items/Import.cshtml`; route declaration `@page`.
- Fields (HTML): `Confirm`, `DecisionsJson`, `DocumentJson`, `ExpectedProgress`, `GroupsJson`, `JobId`, `PreviewHash`, `Reason`, `RequestKey`, `Revision`, `Upload`.
- Handlers: OnGetAsync, OnPostChunkAsync, OnPostPreviewAsync, OnPostStartAsync, Chunk, Preview, Start.
- Access: [Authorize, RequestSizeLimit(64 * 1024 * 1024), RequestFormLimits(ValueLengthLimit = 48 * 1024 * 1024, MultipartBodyLengthLimit = 40 * 1024 * 1024)]; ITEMS_READ/IMPORT; category scope; staging/chunk idempotent.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Items/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Items/Index.cshtml`; route declaration `@page`.
- Fields (HTML): `CategoryId`.
- Handlers: OnGetAsync.
- Access: [Authorize]; ITEMS_READ/IMPORT; category scope; staging/chunk idempotent.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Privacy.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Privacy.cshtml`; route declaration `@page`.
- Fields (HTML): Không input trực tiếp; xem partial/DTO bên dưới..
- Handlers: OnGet.
- Access: Kế thừa PageModel/public; Public hoặc links theo phiên; không tự tạo dashboard count.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Reports/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Reports/Index.cshtml`; route declaration `@page`.
- Fields (HTML): `AsOfFrom`, `AsOfTo`, `CategoryId`, `Code`, `Condition`, `GroupLevel`, `IncludeDescendants`, `LocationId`, `OrgUnitId`, `PageSize`, `Section`, `Type`, `Unit`.
- Handlers: OnGetAsync, OnGetExportAsync.
- Access: [Authorize]; REPORTS_READ/EXPORT + data permission cùng assignment; scope trước count/aggregate/page/export.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

### Stock/Index.cshtml
- Source: `src/DuyTien.AssetManagement.Server/Pages/Stock/Index.cshtml`; route declaration `@page`.
- Fields (HTML): `AsOf`, `ExpectedVersion`, `ItemId`, `LocationId`, `Minimum`, `Quantity`, `Reason`, `RequestKey`, `Source`, `Unit`.
- Handlers: OnGetAsync, OnPostAsync.
- Access: [Authorize]; STOCK_READ/UPDATE + ITEMS_READ; ba chiều từ Item/Location.
- States/checks: field validation + safe service error; 401/403/404 không lộ record; write422/409/503 giữ input/key/version, không success giả; GET list empty/page; kiểm cụ thể khi đổi màn hình.

## Partials và DTO/binder (field và validation không đổi)

- `src/DuyTien.AssetManagement.Server/Pages/Assets/Create.cshtml.cs`: Form, RequestKey, TreeVersionId.
- `src/DuyTien.AssetManagement.Server/Pages/Assets/Detail.cshtml.cs`: ExpectedVersion, Form, Reason, RequestKey, TreeVersionId.
- `src/DuyTien.AssetManagement.Server/Pages/Assets/Index.cshtml.cs`: CategoryId, Condition, CustodianRef, IncludeDeleted, IncludeDescendants, LocationId, OrgUnitId, PageNumber, PageSize, Q.
- `src/DuyTien.AssetManagement.Server/Pages/Assets/Photos.cshtml.cs`: Description, ExpectedVersion, FileId, IsPrimary, Reason, Upload.
- `src/DuyTien.AssetManagement.Server/Pages/Admin/Users/Detail.cshtml.cs`: CategoryScopeId, DisplayName, IncludeDescendants, Input, LocationScopeId, OrgUnitScopeId, RoleCode.
- `src/DuyTien.AssetManagement.Server/Pages/Admin/Users/Index.cshtml.cs`: DisplayName, Input, Username.
- `src/DuyTien.AssetManagement.Server/Assets/AssetForm.cs`: AliasesText, CategoryId, Choices, Condition, CustodianRef, LocationId, Manufacturer, Model, Name, Notes, OrgUnitId, RecordedOn, Serial.
- `src/DuyTien.AssetManagement.Server/Categories/TreeChangePageModel.cs`: AssetIds, CategoryId, ConfirmImpact, Definition, DeletePlan, DestinationCategoryId, DestinationLocationId, DestinationOrgUnitId, ExpectedAssetRevision, ExpectedTreeRevision, Form, Kind, Name, PreviewHash, Q, Reason, RequestKey, SegmentCode.
- `src/DuyTien.AssetManagement.Server/Media/MediaPageModel.cs`: RequestKey.

DTO/validation source có hiệu lực:
- `src/DuyTien.AssetManagement.Application/Assets/AssetModels.cs`: AssetAlias, AssetChoice, AssetCodeRule, AssetCounter, AssetFormChoices, AssetInput, AssetLifecycleRequest, AssetList, AssetLocation, AssetOperation, AssetOrgUnit, AssetQuery, AssetRecord, AssetView, AssetWriteRequest, AssetWriteResult.
- `src/DuyTien.AssetManagement.Application/Categories/CategoryModels.cs`: CategoryDiff, CategoryDocument, CategoryDuplicateName, CategoryImportOperation, CategoryImportResult, CategoryInput, CategoryIssue, CategoryList, CategoryNode, CategoryPreview, CategoryTreeVersion, CategoryView.
- `src/DuyTien.AssetManagement.Application/Categories/TreeMutationModels.cs`: CategoryHistoryEntry, TreeAssetImpact, TreeChangeOperation, TreeMutationPreview, TreeMutationRequest, TreeMutationResult, TreeMutationSnapshot, TreeNodeImpact, TreeRegistryCheckpoint, TreeRegistryImpact.
- `src/DuyTien.AssetManagement.Application/Inventory/InventoryModels.cs`: InventoryChoice, InventoryChoices, InventoryIssue, InventoryOperation, InventoryQuery, ItemDiff, ItemDocument, ItemDuplicateDecision, ItemGroupMapping, ItemImportJob, ItemImportPlanRow, ItemImportRequest, ItemList, ItemPlannedRow, ItemPreview, ItemRecord, ItemSourceRow, StockBalance, StockList, StockWriteRequest.
- `src/DuyTien.AssetManagement.Application/Media/MediaModels.cs`: DocumentInput, DocumentPublishRequest, DocumentVersionRequest, DocumentVersionView, DocumentView, DocumentWriteRequest, LibraryDocument, LibraryDocumentVersion, ManagedFile, MediaDownload, MediaFileContent, MediaFileView, MediaLifecycleRequest, MediaMutationResult, MediaOperation, MediaUploadResult, PhotoUpdateRequest.
- `src/DuyTien.AssetManagement.Application/Authentication/AuthModels.cs`: AuthRoleAssignment, AuthRoleAssignmentWithScopes, AuthScopeGrant, AuthSecurityEvent, AuthSession, AuthSystemInitializationResult, AuthUser, AuthenticatedSession, BootstrapResult, LoginRequest, LoginResult, PasswordHashVerification, RefreshResult, RefreshTokenMarker, RoleAssignmentInput, ScopeGrantInput, TemporaryPasswordResult.

## Thành phần, trạng thái và khoảng trống

- _AssetFields và _TreeChangePanel chứa field/preview chung; dùng label Việt, summary+field validation và confirmation. Files.Content là gateway; không lấy tên/path làm ID.
- /Offline và error HTML sinh trong ConnectedRuntime, /api/connection/files là coverage kỹ thuật; phải dùng style local và giữ credential binding. LocalSetup/LocalDemo legacy410, không menu.
- Local Bootstrap/jQuery hiện có, system font, SVG tự vẽ nội bộ; không thêm CDN/dependency frontend. App khác React tool tham chiếu nhưng giữ Razor.
- REF-01 đã giải quyết bằng đúng bytes/hash; MAP-01 đã liệt kê toàn route/fields/handlers/DTO. BE-01 chỉ có bulk MOVE_ASSETS; BE-02 không có dashboard KPI đã duyệt; BE-03 cache partial; BE-04 rule/source OPEN-TBD; BE-05 Items import có Preview/Start/Chunk, không hứa cancel job.
- UI thiếu: sidebar/header/breadcrumb, các label fallback còn tên code; copy demo/task/PoC; xác nhận form nguy hiểm; preview file/feedback; giữ context/scroll/filter. Chịu trách nhiệm017/018/019/020 theo PLAN-003.
- Read-only role Viewer; write AssetEditor/CatalogManager/DocumentManager đúng ma trận, Admin vẫn scope. Không ghép nhiều assignment để tăng scope.
- File input không thể khôi phục sau navigation; AJAX hiện có giữ File trong RAM khi chưa rời trang. UI phải nói rõ khi cần chọn lại.
- Mọi module: loading/empty/service unavailable/403/404/409/422/UNKNOWN tách nhau; đơn vị/asOf/nguồn, DRAFT/DRAFT_DEMO/PUBLISHED label giữ provenance.
