# Ma trận quyền và phạm vi — TASK-005

Status: `ACCEPTED` ngày 2026-10-07; implementation local đã qua review; live DEV provisioning/ACL/schema pass 2026-10-09, quyền/phiên live và UI UAT còn thiếu.

Mục tiêu đã xác lập: permission hành động và phạm vi dữ liệu phải được kiểm tra
phía server. Năm role dưới đây là role ứng dụng cho demo, không phải chức danh
thật của Công ty Duy Tiến. Tên/ví dụ công việc chỉ để giải thích thiết kế.

## 1. Năm role đề xuất

| Role | Giải thích tiếng Việt | Ví dụ công việc giả |
|---|---|---|
| `Viewer` | Chỉ tra cứu/xem/tải dữ liệu được phép; không sửa hoặc export | Nhân viên tra cứu thiết bị và tải hướng dẫn trong phạm vi được giao |
| `AssetEditor` | Quản lý vòng đời tài sản và ảnh tài sản trong phạm vi | Nhân viên tài sản ghi nhận máy mới, sửa vị trí hoặc chuyển phân loại |
| `CatalogManager` | Quản lý cây danh mục, Items và snapshot số lượng | Nhân viên quản trị dữ liệu nạp cây nháp, nạp Items và cập nhật snapshot tồn |
| `DocumentManager` | Quản lý biểu mẫu, DOCX/PDF và phiên bản tài liệu | Nhân viên hồ sơ tải lên một phiên bản biểu mẫu và phát hành cặp DOCX/PDF |
| `Admin` | Quản trị user/role/scope, xem audit và có toàn bộ permission nghiệp vụ | Người vận hành tạo user, khóa tài khoản bị nghi lộ và gán phạm vi |

Các ví dụ trên không ánh xạ sang chức danh công ty và không tạo tài khoản từ dữ
liệu nhân sự.

## 2. Ma trận permission cụ thể

`Có*` nghĩa permission vẫn phải qua scope của lần gán role. Admin có `ALL` tường
minh, không bỏ qua authorization service.

| Permission | Viewer | AssetEditor | CatalogManager | DocumentManager | Admin |
|---|---:|---:|---:|---:|---:|
| `ASSETS_READ` | Có* | Có* | Có* | Có* | Có |
| `ASSETS_CREATE`, `ASSETS_UPDATE` | Không | Có* | Không | Không | Có |
| `ASSETS_DELETE`, `ASSETS_RESTORE` | Không | Có* | Không | Không | Có |
| `ASSETS_MOVE` | Không | Có* ở scope nguồn và đích | Không | Không | Có |
| `ASSET_FILES_DOWNLOAD` | Có* | Có* | Có* | Có* | Có |
| `ASSET_FILES_UPLOAD`, `ASSET_FILES_DELETE` | Không | Có* | Không | Không | Có |
| `CATEGORIES_READ` | Có* | Có* | Có* | Có* | Có |
| `CATEGORIES_CREATE`, `CATEGORIES_UPDATE`, `CATEGORIES_DELETE` | Không | Không | Có* | Không | Có |
| `CATEGORIES_MOVE` | Không | Không | Có* ở scope nguồn và đích | Không | Có |
| `CATEGORIES_IMPORT`, `CATEGORIES_EXPORT` | Không | Không | Có* | Không | Có |
| `ITEMS_READ`, `STOCK_READ` | Có* | Có* | Có* | Có* | Có |
| `ITEMS_IMPORT`, `STOCK_UPDATE` | Không | Không | Có* | Không | Có |
| `DOCUMENTS_READ`, `DOCUMENTS_DOWNLOAD` | Có* | Có* | Có* | Có* | Có |
| `DOCUMENTS_MANAGE` | Không | Không | Không | Có* | Có |
| `REPORTS_READ` | Có* | Có* | Có* | Có* | Có |
| `REPORTS_EXPORT` | Không | Có* | Có* | Có* | Có |
| `USERS_READ`, `USERS_MANAGE`, `AUTHZ_ASSIGN` | Không | Không | Không | Không | Có |
| `AUDIT_READ` | Không | Không | Không | Không | Có |

## 3. Cộng nhiều role và cấp quyền trực tiếp

- Một user có thể có nhiều `UserRoleAssignment`. Permission hiệu lực là hợp
  (`OR`) permission của các role đang hoạt động.
- Scope gắn với từng assignment, không gắn chung toàn user. Một assignment chỉ
  cấp quyền khi chính assignment đó vừa chứa permission vừa match đủ mọi chiều
  scope áp dụng. Không ghép permission từ role A với scope rộng của role B.
- Demo không cấp permission trực tiếp cho user và không có deny. Muốn thêm quyền,
  Admin gán thêm một role với scope rõ ràng. Thay đổi mô hình này cần ADR và test
  precedence.
- Năm role là system role cố định trong demo. Admin được gán/bỏ role và scope,
  không sửa catalog permission hoặc tạo custom role qua UI TASK-005.

## 4. Chức danh không phải phạm vi dữ liệu

`Position` là chức danh/vị trí công việc, chỉ dùng làm metadata và template gợi ý
role. `PositionRoleTemplate` không tự cấp quyền. Admin phải duyệt và tạo
`UserRoleAssignment` cùng scope cụ thể. Không suy diễn một chức danh được xem mọi
đơn vị, địa điểm hay danh mục.

## 5. Quy tắc scope chung

- Ba chiều độc lập: `OrgUnit`, `Location`, `CategorySubtree`.
- Mỗi chiều có quan hệ cha–con bằng ID bất biến và `ParentId`; không dùng tên,
  mã hiển thị hoặc prefix chuỗi để suy diễn con cháu.
- `includeDescendants=false`: chỉ match đúng `ScopeId`.
- `includeDescendants=true`: match `ScopeId` và mọi descendant trong version cây
  đang hiệu lực; không match ancestor hoặc nhánh anh em.
- Trong cùng một chiều, nhiều grant là `OR`. Giữa các chiều áp dụng cho cùng
  resource là `AND`, và phải được thỏa trong cùng một role assignment.
- Scope rỗng mặc định không truy cập. `ALL` phải được gán rõ theo từng chiều.
- Nếu chiều bắt buộc của resource bị thiếu, ID không tồn tại hoặc đã ngừng dùng,
  record vào trạng thái cần sửa scope: non-admin không thấy ở list/detail/file/
  report và mọi mutation thường bị chặn. Admin chỉ xem/sửa qua luồng phục hồi dữ
  liệu; record không được tính vào báo cáo nghiệp vụ cho đến khi hợp lệ.
- Chiều ghi `N/A` trong bảng dưới đây là không áp dụng theo loại dữ liệu, không
  đồng nghĩa với dữ liệu bị thiếu.

## 6. Scope theo loại dữ liệu

| Loại dữ liệu/hành động | OrgUnit | Location | CategorySubtree | Quy tắc bổ sung |
|---|---|---|---|---|
| Asset list/detail/write | Bắt buộc từ `Asset.OrgUnitId` | Bắt buộc từ `Asset.LocationId` | Bắt buộc từ `Asset.CategoryId` | Phải match cả ba trong cùng assignment |
| Asset move | Nguồn + đích | Nguồn + đích | Nguồn + đích | Preview dùng cùng policy; không tạo quyền ngầm sau move |
| Category | N/A | N/A | Bắt buộc từ `CategoryId` | Move cần match nhánh nguồn và đích |
| Item | N/A | N/A | Bắt buộc từ `Item.CategoryId` trong demo | Item chưa phân loại bị quarantine, không tự coi là global |
| StockBalance | Từ OrgUnit của Location | Bắt buộc từ `LocationId` | Từ `Item.CategoryId` | Match OrgUnit + Location + Category; `UNKNOWN` không thành 0 |
| Document/DocumentVersion | N/A | N/A | Bắt buộc từ `Document.CategoryId` | Demo scope tài liệu theo nhánh danh mục |
| File | Kế thừa owner | Kế thừa owner | Kế thừa owner | Không nhận scope độc lập; resolve owner rồi kiểm quyền |
| OrgUnit | Chính `OrgUnitId` | N/A | N/A | `includeDescendants` dùng cây đơn vị giả/đã được duyệt |
| Location | Từ `Location.OrgUnitId` | Chính `LocationId` | N/A | Phải match cả OrgUnit và Location |
| Report/export | Theo từng row nguồn | Theo từng row nguồn | Theo từng row nguồn | Lọc trước aggregate/count; không lộ total ngoài scope |
| Import | Theo từng row đích | Theo từng row đích | Theo từng row đích | Row ngoài/thiếu scope bị từ chối, không commit vượt quyền |
| Users/Roles/Sessions/SecurityEvents | N/A | N/A | N/A | Chỉ permission Admin; không dùng scope nghiệp vụ |

## 7. Fixture scope giả cho test

Các ID sau hoàn toàn giả, không mô tả cơ cấu công ty:

| Chiều | Cây giả |
|---|---|
| OrgUnit | `OU-DEMO-A` → `OU-DEMO-A-CHILD`; nhánh khác `OU-DEMO-B` |
| Location | `LOC-DEMO-A` → `LOC-DEMO-A-CHILD`; nhánh khác `LOC-DEMO-B` |
| Category | `CAT-DEMO-EQUIPMENT` → `CAT-DEMO-EQUIPMENT-CHILD`; nhánh khác `CAT-DEMO-VEHICLE` |

Test phải chứng minh exact/descendant/sibling, dữ liệu thiếu scope, nhiều role
không cross-combine scope, revoke tức thời và source/destination của move.

## 8. Kiểm tra bắt buộc

1. API, Razor Pages, list/detail/search/report/export/file dùng cùng authorization service.
2. Quyền vừa bị thu hồi không tiếp tục từ cookie/token/cache cũ; kiểm
   `SecurityVersion` phía server.
3. Mọi user-admin action có audit; không log password/token hoặc response reset.
4. Không khóa, hạ role hoặc bỏ ALL của admin hoạt động cuối cùng.
5. Spreadsheet/Drive dùng ACL vận hành riêng; app user không đọc Sheet trực tiếp.
6. Thiếu permission hành động trả 403; resource không tồn tại/ngoài scope trả 404;
   list/report chỉ trả count/aggregate trong scope.

Chi tiết account/session/bootstrap/UI/test nằm tại
[AUTH-DESIGN-PROPOSED](../02-architecture/AUTH-DESIGN-PROPOSED.md) (tên file lịch sử; nội dung đã `ACCEPTED`).
