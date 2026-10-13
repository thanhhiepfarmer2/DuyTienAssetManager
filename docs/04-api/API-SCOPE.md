# Contract API đề xuất

Status: PROPOSED cho route nghiệp vụ; chưa là OpenAPI đã triển khai. Riêng
`GET /api/system/status` là endpoint scaffold của TASK-003 và chỉ trả cờ
configured/live, không chứng minh kết nối Google.

Phần auth/admin dưới đây đã được người dùng duyệt cho TASK-005 ngày 2026-10-07
và implementation local đã qua build/test/review. Chi tiết policy tại
[AUTH-DESIGN-PROPOSED](../02-architecture/AUTH-DESIGN-PROPOSED.md).

| Nhóm | Endpoint dự kiến | Quyền |
|---|---|---|
| System scaffold | GET /api/system/status | Chưa chứa dữ liệu nghiệp vụ; chỉ dùng dev/readiness |
| Auth | POST `/api/auth/login`, `/refresh`, `/logout`, `/logout-all`, `/change-password`; GET `/api/me` | Anonymous chỉ cho login/refresh; cookie web có CSRF, mobile bearer token |
| Users | GET/POST `/api/admin/users`; GET/PATCH `/{id}`; POST `/{id}/lock`, `/unlock`, `/reset-password` | `USERS_READ`/`USERS_MANAGE` |
| Authorization admin | GET `/api/admin/authorization-catalog`; PUT/DELETE `/api/admin/users/{id}/role-assignments` | `AUTHZ_ASSIGN`; năm role/permission cố định |
| Security events | GET `/api/admin/security-events` | `AUDIT_READ`; dữ liệu đã làm sạch, không token/password |
| Categories | GET/POST /api/categories; PATCH/DELETE /{id} | CATEGORIES_READ/CREATE/UPDATE/DELETE |
| Tree | POST /api/categories/{id}/move-preview, move; GET /api/categories/export.txt | MOVE/EXPORT+scope nguồn/đích |
| Import | POST /api/imports/preview, /{id}/commit; GET /{id}/status | IMPORT |
| Assets | GET/POST /api/assets; GET/PATCH/DELETE /{id}; POST /{id}/restore | ASSETS actions+scope |
| Asset move | POST /api/assets/{id}/move-preview, move | ASSETS_MOVE |
| Files | POST /api/assets/{id}/photos; GET /api/files/{id}/content; DELETE /api/files/{id} | UPLOAD/DOWNLOAD/DELETE+entity scope |
| Documents | /api/documents; /{id}/versions; /versions/{id}/docx, pdf | Document actions+scope |
| Inventory snapshots | /api/stock-balances | STOCK_READ/UPDATE |
| Reports | GET /api/reports/assets, assets-by-category, stock-empty, stock-low | REPORTS_READ/EXPORT và data scope cùng assignment |

Request mutate có Idempotency-Key và expectedVersion khi update/delete/move; server không nhận sửa ObjectCode/Id trực tiếp sau cấp.
Response list có page/pageSize/total và snapshotAsOf; pageSize cap, filters validate, không trả raw system Sheet/password hash/Google token.
Errors: 401 khi chưa đăng nhập/session invalid; 403 khi đã đăng nhập nhưng thiếu
permission hành động; 404 khi resource không tồn tại hoặc ngoài scope để tránh dò
ID; 409 version/ID conflict; 422 validation; 413 file size; 429 auth rate limit;
503 upstream/maintenance. Không lộ DriveFileId nội bộ không cần thiết qua lỗi.
Download gateway resolve fileId từ metadata entity, kiểm quyền lại; không proxy URL tùy ý từ client.
List/report/export lọc scope trước khi count/aggregate. Auth có rate limits;
stock report thể hiện UNKNOWN/filter; API không truy vấn client-side toàn bộ Sheet.

Razor Pages `ACCEPTED` trong TASK-005: `/Account/Login`,
`/Account/ChangePassword`, `/Account/AccessDenied`, `/Admin/Users`,
`/Admin/Users/{id}`, `/Admin/Authorization`, `/Admin/SecurityEvents`. Các trang
dùng cùng application/authorization service với API; TASK-005 chưa làm UI MAUI.

## TASK-006 — routes đã triển khai local (override đề xuất tương ứng)

GET `/api/categories?q=...`, `/api/categories/tree`, `/api/categories/{id}`: CATEGORIES_READ + scoped rows; GET `/api/categories/export`: CATEGORIES_EXPORT + scope, UTF-8 TXT. Không có POST/PATCH/DELETE CRUD hoặc move endpoint riêng trong task này. POST `/api/categories/imports/preview` multipart `file`: CATEGORIES_IMPORT + mọi row phạm vi ảnh hưởng, response preview/previewId; POST `/api/categories/imports/commit` JSON `{previewId,confirmDuplicateNames}`: recheck permission/revision/digest, key server của preview được lưu ledger cả no-op. Preview RAM gắn actor15min; sau restart upload/preview lại, ledger/file history chống duplicate. Cookie gọi API bị403, bearer validated bởi TASK-005; role CatalogManager/Admin chưa đủ nếu scope không hợp lệ.

HTTP401/403/404/409/400 validation và503 upstream an toàn, no-store; list total chỉ phạm vi. Không pagination vì snapshot bounded1000, không raw auth/Google response. Live Google/browser chưa kiểm; xem TEST-REPORT-TASK-006.

## TASK-007 — routes local đã triển khai (override đề xuất Assets tương ứng)

GET /api/assets: q/categoryId/includeDescendants/condition/orgUnitId/locationId/custodianRef/includeDeleted/page/pageSize; GET /{id}; GET /form-choices?editing=true. POST /api/assets và PATCH /{id}: {asset:AssetInput,expectedVersion,treeVersionId}, Idempotency-Key. DELETE /{id} và POST /{id}/restore: {expectedVersion,reason}, cùng header. PATCH là form DTO đầy đủ, không JSON Patch; DTO disallow unknown JSON member/ID/code/state. Success create201, mutations200; replay cùng key giữ result và trả Replayed=true. ExpectedVersion create0, update/delete/restore là version đã đọc. API bearer-only/no-store, cookie403.

Permission ASSETS_READ/CREATE/UPDATE/DELETE/RESTORE và scope đủ OrgUnit+Location+Category cùng assignment. UPDATE thay scope cần MOVE ở nguồn+đích cùng assignment, không tạo route move-preview/move TASK-008. Lọc trước count/paging; scope thiếu/reference invalid bị quarantine, ngoài detail404. 422 validation/service, JSON binder400,403 permission,404 ngoài scope,409 key/version/tree,503 Google an toàn. Không trả journals/request hashes/Google resource IDs qua API.

Asset DRAFT_DEMO và Category DRAFT không được đổi thành approved. Rule thực/nguồn/Google live/browser chưa đạt; evidence54 test local trong TEST-REPORT-TASK-007 không thay Google/browser.

## TASK-008 local — 2026-10-13

Override đề xuất Tree/Asset move: POST `/api/tree-changes/preview`, body TreeMutationRequest: kind/reason/categoryId?/destinationCategoryId?/name?/segmentCode?/definition?/deletePlan(EMPTY_ONLY|RELOCATE|SOFT_DELETE)/assetIds?/destinationOrgUnitId?/destinationLocationId?. Kind EDIT_CATEGORY|MOVE_CATEGORY|ARCHIVE_CATEGORY|MOVE_ASSETS. Response treeRevision/assetRevision/previewHash/treeVersionId/categories/assets/affected counts/issues/CanCommit/State DRAFT.

POST `/api/tree-changes/commit`: `{request,expectedTreeRevision,expectedAssetRevision,previewHash,confirmImpact}`, đúng một header Idempotency-Key. Recheck actor/current permission, hai revision và snapshot digest; key actor/payload bền trong TreeChanges. GET `/api/categories/{id}/history`: READ current + từng old hierarchy, path/code che ancestor ngoài scope. API bearer-only/no-store/cookie403;401,403 permission,404 ngoài scope,400 validation/JSON,409 stale/key,503 upstream. Không nhận approved flag hoặc ID/code Asset mới.

Category UPDATE/MOVE/DELETE + READ mỗi node; bulk Asset MOVE/UPDATE nguồn+đích cùng assignment đủ ba chiều, DELETE khi plan soft-delete. Tree move có Asset phải có quyền Asset tương ứng, không ghép assignment khác. Preview không lộ count ngoài scope; module Items/Documents/Files chưa triển khai không được coi là zero. Import không được thay cấu trúc đang có Asset để bỏ qua plan này. Không thêm endpoint report TASK-011 hoặc create category sâu. [Thiết kế](../02-architecture/TREE-MUTATION-DESIGN-TASK-008.md).

## TASK-009 local — 2026-10-13

GET/POST `/api/documents`, GET/PATCH `/{id}`, POST `/{id}/archive`, POST `/{id}/versions`; POST `/api/document-versions/{id}/publish`; POST `/api/document-versions/{id}/files/{kind}` kind DOCX/PDF multipart file+expectedVersion. DocumentWriteRequest {document:{name,businessCode,categoryId,description},expectedVersion}; version request {label,expectedDocumentVersion}; publish {expectedVersion,expectedDocumentVersion,effectiveOn,reason,confirmDemo}. Category không đổi qua metadata update; không tự chốt format business code.

GET/POST `/api/assets/{id}/photos`: POST multipart đúng một file, expectedVersion(owner Asset), description?,isPrimary?. PATCH `/api/files/{id}/photo` {expectedVersion,description,isPrimary,reason}; DELETE `/api/files/{id}` {expectedVersion,reason} chỉ photo soft-delete; POST `/api/files/{id}/cancel` cùng lifecycle DTO cho PENDING/FAILED; POST `/api/files/{id}/reconcile`; GET `/api/files/{id}/content?preview=true` gateway, không URL/Drive ID client. Cookie UI tải qua `/Files/Content/{id}` cùng service/scope, GET an toàn.

Mọi API bearer-only/no-store, cookie403; writes nhận một Idempotency-Key trừ reconcile vốn dùng durable operation theo file ID. Public result entity ID/version/replayed hoặc safe FileView/state; success200 chỉ khi action/AVAILABLE đã xác nhận.400 validation/binder,413 cap,403 action,404 owner/file ngoài scope hoặc inactive,409 stale/key/counterpart/frozen,503 upstream/PENDING có opaque fileId. DTO disallow unknown JSON; multipart reject field lạ/Drive ID. Không trả raw journal/checksum/session URI. CSRF mọi Razor POST, API multipart không nhận cookie và không disable CSRF app.

ASSET_FILES actions + current Asset đủ ba scope/read; DOCUMENTS_READ/DOWNLOAD/MANAGE + active Category scope. Viewer không thấy draft/future-effective version; manager scoped xem draft, chỉ publish PUBLISHED_DEMO khi hai file AVAILABLE cùng version và confirm. Download re-resolve owner và slot hiện tại, current scopes, metadata/parent/owner/op/size/bytes checksum; safe filename, attachment mặc định, inline preview chỉ PNG/JPEG/PDF, nosniff/CSP/no-store. Không convert DOCX/PDF/native Google Docs hoặc implement Items. [Thiết kế](../02-architecture/MEDIA-DESIGN-TASK-009.md).

## TASK-010 local API — 2026-10-13

GET /api/items?q=&categoryId=&page=&pageSize= (max100), /api/items/{id}; POST /api/item-imports/preview (ItemImportRequest, preview diff có page/pageSize), /api/item-imports/preview-file (multipart một XLSX/JSON + groups/decisions/reason, trả parsedDocument+preview để giữ retry). POST /api/item-imports `{request,revision,previewHash,confirm}` + một Idempotency-Key, GET /api/item-imports (max50 scoped jobs) và /api/item-imports/{id}, POST /api/item-imports/{id}/chunks `{expectedProgress}` + một key. Staging bền, VALIDATED/PARTIAL/COMPLETE rõ; retry key payload khác409, không xóa Item nguồn vắng hoặc cấp Asset/ObjectCode.

GET /api/stock-balances (InventoryQuery paging/scoped), POST /api/stock-balances StockWriteRequest (ItemId/LocationId/Unit/Quantity?/Minimum?/AsOfUTC/Source/ExpectedVersion/Reason) + key; GET /api/inventory/choices?stock=true theo permission/scope. Quantity null UNKNOWN,0 OUT, >0<=min LOW; minnull chưa chốt, không chuyển đổi/cộng đơn vị ngầm hoặc lấy BOM/kho mặc định làm tồn. Stock không phải giao dịch nhập/xuất.

Tất cả bearer-only/no-store; cookie API403,401 auth,403 permission,404 ngoài scope,409 stale/key/tree/progress,422 validation,400 malformedJSON/binding,503 upstream an toàn. Scope source/destination và stock ba chiều cùng assignment; check trước replay. Fixtures local, chưa Google live/browser. Không endpoint báo cáo TASK-011.

## TASK-011 local reports API — 2026-10-13

GET /api/reports/assets(R01), assets-by-category(R02), stock-empty(R03), stock-low(R04); cùng route thêm /export CSV UTF8BOM. Query: q/categoryId/includeDescendants/orgUnitId/locationId/page/pageSize(max100); Asset thêm type/condition, R02 groupLevel1–10; Stock thêm unit/asOfFrom/asOfToUTC/section. Asset không nhận stock-only filters; date range lọc current snapshot timestamp, không historical asOf lookup. Section MATCHES(default), UNKNOWN/MISSING_MINIMUM/NOT_TRACKED/TRACKING_UNCONFIRMED/SOURCE_MISSING/INVALID_SNAPSHOT; missing-min counts có thể overlap OUT/UNKNOWN, không cộng status counts.

Response ReportResult có ReadAtUTC/DataState/warnings, rows projected, summary scoped asset/item/snapshot counts, status checks, unit summaries không grandQuantity, per-row AsOf/Unit/Source/Quantity?/Minimum?, partial allowed path, choices/paging/total. R02 direct/subtree perF và uniqueAssetCount riêng, không cộng cấpF. Scope trước toàn bộ source filters/count/aggregate/page/CSV, report action+source permissions cùng assignment. Không public global revision counters/raw Sheets/Items20cells/BOM/token.

Bearer-only/no-store;401auth,403permission/cookieAPI,404unknown/out-of-scope filter/report,422invalid business filter,400binding,503upstream safe không báo0. Export current authority REPORTS_EXPORT, toàn selected scoped result theo filters không chỉ page, attachment/safe filename/nosniff và formula-injection guard;32MiB byte bound không silent partial. Razor cookie GET read/export dùng cùng service; không GET mutation hoặc thêm POST. R05–R15 chưa chọn; Google/browser NOT_RUN.

## Client Android TASK-012 local — 2026-10-13

Không thêm/thay route server. MAUI dùng Auth API username/password → access/refresh/expires/mustChange; refresh body refreshToken, logout/change-password và GET /api/me bearer. Token pair/refresh-in-flight marker trong SecureStorage, rotation tuần tự; mất response không replay token cũ. Assets GET list/detail/form-choices, POST/PATCH AssetWriteRequest đầy đủ/header Idempotency-Key; create expectedVersion0, update bản đã đọc/treeVersion. Không client ID/code/state/scope allocation. Photos GET/POST multipart file+expectedVersion(owner)+description/isPrimary/header key, POST reconcile, content?preview=true bearer; AVAILABLE mới success. DTO/payload/key giữ khi retry,409 explicit reload; backend rechecks quyền/scopes. Client HTTPS/no redirect/cookie/credential URL. Local contract tests fake stores không Google/device proof. [Report](../07-testing/TEST-REPORT-TASK-012.md).

## TASK-014 pha Web — 2026-10-14

Tree preview thêm Registry impacts (ITEM/STOCK/DOCUMENT/VERSION/PHOTO/DOCX/PDF) chỉ saupermission/scopes; Result thêm changedItem/document/registrycounts/Reconciled. Hash gắn fullInventory/Media, checkpointbền trongTreeRegistryDetails; commitvẫn2revision+hash/key vàrecheck4registry. Chỉfullknownschema, owner/journal/rowcaps đúng; unknown/partialfail503, stale409, scope404. Public /api/system/status config-only, State WEB_DEMO_LOCAL, LiveConnectionVerifiedfalse; không implicitGoogleprobe/hằngtaskstate. Routesbusiness/auth/scopecũ giữ. [Thiết kế](../02-architecture/WEB-COMPLETION-DESIGN-TASK-014.md).
