# Thiết kế đăng nhập, admin và phân quyền cho TASK-005

- Status: `ACCEPTED` về nghiệp vụ ngày 2026-10-07; implementation local đã qua review trong TASK-005, live gate chưa chạy.
- Ngày: 2026-10-07.
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md), AC-03/AC-04.
- Task: [TASK-005](../06-tasks/TASK-005.md), trạng thái `IN_REVIEW`.
- Nguồn: yêu cầu người dùng ngày 2026-10-07; [ADR-001](ADR-001-proposed-sheets-drive-web-mobile.md);
  [PERMISSIONS](../01-requirements/PERMISSIONS.md); [SHEETS-SCHEMA](../03-database/SHEETS-SCHEMA.md).

Người dùng đã duyệt ba quyết định nghiệp vụ ở mục 9 và phương án demo ngày
2026-10-07, đồng thời điều chỉnh `Location.ParentId`, định nghĩa admin hoạt động,
khóa writer liên tiến trình và chính sách mật khẩu. Tên file được giữ để không
làm hỏng liên kết lịch sử; nội dung dưới đây là quyết định `ACCEPTED`. Lượt triển
khai local không tạo/chọn/ghi system spreadsheet Google và không chạy lại PoC cũ.

## 1. Tóm tắt phương án

| Nhóm | Phương án demo `ACCEPTED` |
|---|---|
| Role và permission | Năm role cố định; permission cộng theo nhiều role; không cấp permission trực tiếp cho user |
| Scope | Scope gắn với từng lần gán role; OrgUnit/Location/Category là ba chiều dữ liệu độc lập; chức danh không phải scope |
| Tài khoản | Admin tạo tài khoản; không tự đăng ký; yêu cầu mật khẩu duy nhất là tối thiểu 8 ký tự; đổi bắt buộc sau bootstrap/reset |
| Phiên | Web cookie opaque; mobile bearer access token + refresh token opaque; server chỉ lưu hash; revoke kiểm tra `SecurityVersion` |
| Bootstrap/recovery | Lệnh vận hành riêng, secret ngoài repo; không bootstrap khi web startup; không có backdoor thường trực |
| Persistence auth | Spreadsheet hệ thống riêng, ACL hạn chế; ID lấy qua provisioning chủ động hoặc Picker |
| UI TASK-005 | Backend/API + Razor Pages cho login, đổi mật khẩu và quản trị user/role/scope; chưa làm UI mobile |
| Nghiệm thu | Unit/integration bằng fake adapter và fixture scope giả; live system spreadsheet là gate sau khi thiết kế được duyệt |

## 2. Ranh giới danh tính đã xác lập

- Tài khoản đăng nhập ứng dụng do backend quản lý, không phải Google Sign-In.
- Google OAuth của tài khoản vận hành chỉ dùng backend truy cập Sheets/Drive;
  không biến tài khoản Google thành user ứng dụng.
- Web dùng cookie HttpOnly + anti-forgery; mobile dùng token ứng dụng lưu trong
  secure storage như ADR-001 đã chốt.
- Toàn bộ authorization thực thi phía server cho list/detail/search/report/
  export/file và lệnh ghi. Ẩn nút UI không phải kiểm tra quyền.

## 3. Tài khoản và mật khẩu `ACCEPTED`

| Hạng mục | Chính sách đề xuất |
|---|---|
| Tạo tài khoản | Chỉ Admin; không self-registration; không import sheet nhân sự thành user |
| Username | 3–64 ký tự ASCII `[a-z0-9._-]`, so sánh không phân biệt hoa/thường; `admin` dành cho bootstrap |
| Tên hiển thị | Bắt buộc, cho phép Unicode; email/điện thoại không bắt buộc trong demo |
| Mật khẩu | Yêu cầu duy nhất về nội dung/độ dài là tối thiểu 8 ký tự; không có độ dài tối đa, complexity, blocklist hoặc rule chứa username |
| Hash | Dùng `IPasswordHasher<TUser>` của ASP.NET Core; lưu hash có version, không tự viết thuật toán |
| Hết hạn định kỳ | Không ép đổi theo chu kỳ; chỉ đổi khi bootstrap/reset/nghi ngờ lộ hoặc người dùng chủ động |
| Lần đầu/reset | `MustChangePassword=true`; phiên hạn chế chỉ được xem tài khoản, đổi mật khẩu và logout |
| Reset | Server sinh mật khẩu tạm ngẫu nhiên 20 ký tự, hiển thị đúng một lần cho Admin để chuyển qua kênh nội bộ; không log/lưu plaintext |
| Thay đổi/reset mật khẩu | Tăng `SecurityVersion`, revoke mọi phiên; người dùng đăng nhập lại sau khi đổi |
| Sai mật khẩu | 5 lần thất bại liên tiếp trong 15 phút → khóa tự động 15 phút; đăng nhập thành công reset bộ đếm |
| Rate limit | Tối đa 10 request login/15 phút cho mỗi cặp IP + normalized username; vượt ngưỡng trả 429 |
| Khóa thủ công | Admin có thể khóa/mở; khóa revoke mọi phiên; response login không tiết lộ username có tồn tại hay không |

`ACCEPTED`: demo chưa có email reset, quên mật khẩu tự phục vụ, SSO hay MFA.
Những phần này chỉ thêm bằng Task/Requirement riêng sau này.

## 4. Phiên web và mobile `ACCEPTED`

Token/session là chuỗi opaque ngẫu nhiên tối thiểu 256 bit. Client giữ giá trị
gốc; spreadsheet chỉ giữ hash. Không lưu token trong log, HTML, query string hoặc
`localStorage`.

| Kênh | Credential | Idle timeout | Absolute timeout | Ghi chú |
|---|---|---:|---:|---|
| Web mặc định | Cookie session HttpOnly/Secure/SameSite=Lax | 30 phút | 8 giờ | Anti-forgery cho mọi request thay đổi trạng thái |
| Web “Ghi nhớ đăng nhập” | Cookie persistent, mặc định không chọn | 24 giờ | 7 ngày | Không cho dùng khi `MustChangePassword=true` |
| Mobile access | Bearer access token opaque | Không áp dụng | 15 phút | Lưu trong secure storage, không dùng WebView/localStorage |
| Mobile refresh | Refresh token opaque, rotate mỗi lần dùng | 14 ngày | 30 ngày | Reuse token cũ revoke cả token family |

- Tối đa 5 phiên hoạt động cho một user trên mọi kênh; login thứ sáu revoke phiên
  cũ nhất và ghi `SecurityEvents`.
- Logout mặc định chỉ revoke phiên hiện tại. `logout-all`, khóa user, reset/đổi
  mật khẩu hoặc đổi role/scope revoke toàn bộ phiên và tăng `SecurityVersion`.
- Mỗi request phải kiểm tra session chưa revoke, chưa hết hạn và
  `session.SecurityVersion == user.SecurityVersion`. Cache chỉ được dùng trong
  một backend instance và phải invalidation đồng bộ khi có thay đổi quyền.
- `LastSeenAt` chỉ ghi tối đa một lần mỗi 5 phút/phiên để không ghi Sheets ở mọi
  request.

## 5. Bootstrap và phục hồi admin `ACCEPTED`

### 5.1 Bootstrap một lần

Lệnh: `auth-bootstrap-admin`. Lệnh này chỉ chạy chủ động từ terminal của
người vận hành; không phải web endpoint và không tự chạy khi start ứng dụng.

1. Yêu cầu system spreadsheet đã được cấp quyền và khởi tạo schema.
2. Đọc mật khẩu từ `Auth:Bootstrap:AdminPassword` qua user-secrets/secret store.
3. Nếu chưa có username `admin`, tạo user, hash mật khẩu, gán role `Admin`, scope
   `ALL` tường minh ở ba chiều và `MustChangePassword=true` trong một batch.
4. Nếu `admin` đã tồn tại, thoát thành công dạng no-op; không reset mật khẩu,
   role, scope hoặc `SecurityVersion`.
5. Không in secret. Với user-secrets dev, lệnh xóa khóa bootstrap sau khi commit
   thành công; với env/secret manager, lệnh chỉ cảnh báo người vận hành xóa/rotate
   vì process không được tự sửa kho secret bên ngoài.

### 5.2 Phục hồi khi không còn admin hoạt động

Lệnh: `auth-recover-admin`, chỉ chạy trong maintenance mode và dùng secret
tạm `Auth:Recovery:AdminPassword`. Lệnh từ chối chạy nếu vẫn còn ít nhất một
admin hoạt động. Khi hợp lệ, lệnh reset/mở khóa `admin`, gán lại role Admin + ALL,
tăng `SecurityVersion`, revoke session cũ, bắt buộc đổi mật khẩu và ghi audit.
Secret phục hồi được xóa/rotate sau thành công; không có master password/backdoor
thường trực.

“Admin hoạt động” là user `ACTIVE`, có ít nhất một assignment `Admin` đang hiệu
lực tại thời điểm kiểm tra (`ACTIVE`, chưa revoke, `EffectiveFrom` không ở tương
lai và `EffectiveTo` chưa hết hạn), đồng thời chính assignment đó có `ALL` tường
minh cho OrgUnit, Location và CategorySubtree. Không được ghép ba `ALL` từ các
assignment khác nhau. Mọi thao tác khóa, revoke/hạ role, đổi thời hạn hoặc bỏ ALL
phải tính trạng thái sau thay đổi trong cùng batch và từ chối nếu làm số admin
hoạt động về 0.

### 5.3 Loại trừ writer đồng thời

- Web server giữ độc quyền một process lease dựa trên file lock của hệ điều hành
  trong toàn bộ vòng đời. `auth-system-init`, bootstrap và recovery phải lấy đúng
  lease đó trước khi ghi; nếu web hoặc CLI khác đang giữ thì lệnh fail-closed.
- Semaphore/lock RAM chỉ dùng tuần tự hóa request trong một process, không được
  coi là bảo vệ giữa hai process.
- Demo vẫn chỉ cho phép một backend instance. Đường dẫn lock cấu hình ngoài
  client và phải trỏ tới cùng filesystem trên host; scale-out cần coordinator
  dùng chung và ADR riêng.

## 6. Spreadsheet hệ thống `ACCEPTED`

- Dùng một spreadsheet riêng với CSDL nghiệp vụ để hạn chế ACL và giữ các tab
  auth cùng một batch boundary. Chi tiết tab/field tại
  [SHEETS-SCHEMA](../03-database/SHEETS-SCHEMA.md).
- App user không được cấp quyền trực tiếp vào spreadsheet. Hidden/protected range
  không được coi là ranh giới bảo mật.
- ID cấu hình: `GoogleWorkspace:SystemSpreadsheetId`; không ghi vào client/log.
- Resource mới: lệnh provisioning riêng tạo spreadsheet hệ thống khi người vận
  hành chủ động gọi. Resource có sẵn: phải chọn chính file qua Picker. Dán ID chỉ
  là cấu hình, không phải grant `drive.file`.
- Sau khi có grant, lệnh `auth-system-init` mới tạo/kiểm tra tab và header có
  version. `auth-bootstrap-admin` chỉ chạy sau init thành công.
- Lượt thiết kế này không gọi provisioning/Picker/init và không tạo/ghi Google.

## 7. API và Razor Pages trong TASK-005 `ACCEPTED`

### API

- Auth: login, refresh mobile, logout, logout-all, change-password, `GET /api/me`.
- Admin: list/create/update/lock/unlock/reset user; gán/xóa role assignment và
  scope; đọc catalog role/permission cố định; đọc security events tối thiểu.
- Không cho tạo role/permission tùy biến trong demo. Năm role là system roles;
  Admin chỉ gán role/scope cho user.
- 401: chưa đăng nhập/session invalid. 403: đã đăng nhập nhưng thiếu permission
  hành động. 404: resource không tồn tại hoặc nằm ngoài scope, để tránh dò ID.
  List/report/export phải lọc trước khi đếm tổng.

### Razor Pages

| Trang | Mục đích |
|---|---|
| `/Account/Login` | Đăng nhập web, tùy chọn “Ghi nhớ đăng nhập” |
| `/Account/ChangePassword` | Đổi mật khẩu thường hoặc bắt buộc lần đầu |
| `/Account/AccessDenied` | Thông báo không có permission, không lộ resource ngoài scope |
| `/Admin/Users` | Danh sách/tạo user, trạng thái và phiên hoạt động |
| `/Admin/Users/{id}` | Sửa tên, khóa/mở, reset mật khẩu, gán role và scope |
| `/Admin/Authorization` | Xem catalog năm role/permission và scope; không sửa permission hệ thống |
| `/Admin/SecurityEvents` | Xem audit auth/admin đã làm sạch |

Razor Pages và API phải gọi cùng application service/authorization service;
không đặt nghiệp vụ riêng trong PageModel để bypass policy. TASK-005 chưa triển
khai UI đăng nhập/quản trị trong app MAUI.

## 8. Nghiệm thu bằng dữ liệu giả `ACCEPTED`

Mọi tên/ID dưới đây là fixture giả, không mô tả cơ cấu Công ty Duy Tiến:

| Loại | Fixture giả |
|---|---|
| OrgUnit | `OU-DEMO-A`, `OU-DEMO-A-CHILD`, `OU-DEMO-B` |
| Location | `LOC-DEMO-A` (`ParentId=null`), `LOC-DEMO-A-CHILD` (`ParentId=LOC-DEMO-A`), `LOC-DEMO-B` (`ParentId=null`) |
| Category | `CAT-DEMO-EQUIPMENT`, `CAT-DEMO-EQUIPMENT-CHILD`, `CAT-DEMO-VEHICLE` |
| User | `viewer_demo`, `asset_editor_demo`, `multi_role_demo`, `locked_demo`, `admin_second_demo` |
| Resource | Asset/Document/File/Stock giả ở nhánh A, A-child và B; không dùng dữ liệu doanh nghiệp |

Các case bắt buộc:

1. Bootstrap tạo admin đúng một lần; chạy lại/restart không reset; thiếu secret
   fail rõ và log/build không có plaintext.
2. Login đúng/sai, generic error, lockout lần thứ năm, tự hết khóa và admin unlock.
3. Must-change session không gọi được API khác; reset/đổi password revoke session.
4. Cookie flags/anti-forgery/idle/absolute/remember-me; mobile access expiry,
   refresh rotation, reuse detection và giới hạn 5 phiên.
5. Viewer đọc trong scope nhưng API create/update/delete/move trả 403; detail/file
   ngoài scope trả 404; list/report/export không lộ row hoặc total ngoài scope.
6. `includeDescendants=false` chỉ exact; `true` thấy A-child nhưng không thấy B.
7. `multi_role_demo`: permission là union, nhưng mỗi lần gán role phải tự match đủ
   scope; không được ghép quyền AssetEditor ở A với scope DocumentManager ở B.
8. Role/scope vừa revoke mất hiệu lực ngay ở cookie/access/refresh token cũ.
9. Không khóa/hạ role/bỏ ALL của admin hoạt động cuối cùng; thao tác hợp lệ khi
   vẫn còn `admin_second_demo` đủ điều kiện.
10. Razor Pages kiểm empty/loading/validation/access denied; gọi cùng policy như API.
11. Fake adapter/fault test không thay live Google. Trước `DONE`, system
    spreadsheet dev phải có evidence provisioning/Picker, schema/init/bootstrap,
    restart và ACL trên Google thật; chưa thực hiện trong lượt này.

## 9. Quyết định nghiệp vụ đã được duyệt

Người dùng đã duyệt ngày 2026-10-07:

1. Năm role và các điểm nhạy cảm trong ma trận: `CatalogManager` cập nhật snapshot
   tồn; ba role quản lý được export report; Viewer được xem/tải file nhưng không
   export.
2. Asset demo bắt buộc có đủ OrgUnit + Location + Category; chức danh chỉ là mẫu
   gợi ý gán role, tuyệt đối không tự cấp quyền.
3. Demo không có self-registration, email forgot-password, SSO hay MFA; reset do
   Admin thực hiện bằng mật khẩu tạm và bắt buộc đổi lần đầu.

Các timeout, token rotation, giới hạn phiên, tên tab/field và cơ chế command theo
phương án demo cũng được duyệt. Chi tiết kỹ thuật còn lại do implementation giải
quyết, nhưng không được làm thay đổi các ràng buộc nghiệp vụ trên.
