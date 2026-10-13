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
| Reports | GET /api/reports/assets, assets-by-category, stock-empty, stock-low | REPORT_READ và data scope |

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
