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
