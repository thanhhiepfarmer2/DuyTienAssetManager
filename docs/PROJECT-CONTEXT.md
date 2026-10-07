# Bối cảnh dự án

Ngày khởi tạo bộ mẫu: 2026-10-05. Ngày cập nhật: 2026-10-07.
Trạng thái: `TASK005_LOCAL_IN_REVIEW` — TASK-004 `DONE`; thiết kế auth đã được
duyệt, local format/build/57 test pass. System spreadsheet TASK-005 chưa được
tạo/chọn/ghi nên live gate chưa đạt. TASK-003 vẫn `IN_REVIEW` vì hosting/
domain và thiết bị/signing còn mở.

## Đã được người dùng xác lập

- Tên dự án local: `QuanLyTaiSan_DuyTien`; tên sản phẩm: **Mã số hóa tài sản Công ty TNHH Duy Tiến**.
- Thư mục local: `D:\Dev\QuanLyTaiSan_DuyTien`; người phụ trách: Nguyễn Thanh Hiệp.
- Cây demo lấy từ Excel hiện tại với 15 F1 và giữ trạng thái `DRAFT`; khác biệt với DOCX phải được hiển thị, không tự hòa giải.
- Google Sheets là CSDL; ảnh, DOCX và PDF nằm trong thư mục My Drive của tài khoản vận hành công ty.
- Backend/API dùng ASP.NET Core .NET 10, web dùng Razor Pages và app Android/iOS dùng .NET MAUI.
- Hệ thống phục vụ trong công ty và từ Internet qua HTTPS; nhà cung cấp host/domain chưa được chọn.
- Backend dùng OAuth của tài khoản vận hành để truy cập Google; danh tính Google này tách với tài khoản đăng nhập ứng dụng.
- Demo dùng đúng một backend instance và một kênh ghi Sheets. Scale-out cần quyết định/coordinator khác.
- Có đăng nhập/admin/quyền theo chức năng và phạm vi. Báo cáo bắt buộc gồm toàn bộ tài sản, theo danh mục, hết và sắp hết số lượng.

## Cấu trúc local đã tạo

- `QuanLyTaiSan.DuyTien.sln` và `global.json` pin .NET SDK 10.0.400.
- `src/DuyTien.AssetManagement.Application`: interface nghiệp vụ cho persistence/file và single-writer.
- `src/DuyTien.AssetManagement.Infrastructure.Google`: OAuth refresh, REST adapter
  Sheets/Drive thật, retry/reconciliation PoC và lock một tiến trình.
- `src/DuyTien.AssetManagement.Server`: ASP.NET Core API + Razor Pages, HTTPS redirect và status endpoint không lộ secret.
- `src/DuyTien.AssetManagement.Mobile`: MAUI chỉ target Android/iOS.
- `tests/DuyTien.AssetManagement.ArchitectureTests`: test ranh giới adapter/config,
  concurrency/idempotency/version/retry/reconciliation/file state; fake/stub chỉ
  tồn tại trong test và không là live evidence.
- [CONFIGURATION](CONFIGURATION.md) ghi tên khóa; chưa có credential hay resource ID thật trong repo.

TASK-005 local bổ sung domain auth/RBAC/scope/session, Google system adapter,
process lease liên tiến trình, API mobile bearer, web cookie, Razor Pages
login/change-password/admin và lệnh init/bootstrap/recovery. Fake auth store chỉ
ở test; chưa gọi Google cho system spreadsheet.

Code TASK-004 gọi Google qua scope duy nhất `drive.file`, có provisioning/Picker
và fail-closed với token scope rộng. Người dùng đã chạy readiness,
access-boundary, init và PoC trên Google thật; kết quả chi tiết nằm tại
[TEST-REPORT-TASK-004](07-testing/TEST-REPORT-TASK-004.md). Người dùng đã xác nhận
thủ công tab/record, TXT đúng folder/owner và tính hợp lệ file control; AI không
trực tiếp quan sát. Chưa triển khai auth user, CRUD, import, báo cáo hoặc
TASK-005–TASK-014.
`LiveConnectionVerified=false` khi thiếu cấu hình vẫn là trạng thái runtime đúng
và không tự thay thế evidence môi trường.

## Đã kiểm tra trên máy local

- Thư mục không phải Git worktree; không có branch/remote/commit và TASK-003 không khởi tạo Git.
- SDK 10.0.400, runtime 10.0.11, MSBuild 18.9.6; MAUI workload 10.0.20/10.0.100 đã cài.
- Restore solution: exit 0.
- Build server: exit 0, 0 warnings, 0 errors.
- Architecture tests sau sửa logging: exit 0; passed 31, failed 0, skipped 0;
  gồm request scope, token fail-closed, Picker/provisioning, HTTP stub allow/deny
  và 2 test canary bảo đảm HttpClient log không lộ URL/resource ID, token/header
  hay body nhạy cảm.
- Checkpoint TASK-005 local: `dotnet format` và verify exit 0; build server exit 0,
  0 warnings/0 errors; tests 57 passed, 0 failed, 0 skipped. Có HTTP integration
  Kestrel cho bearer/admin/hash boundary, Google system adapter HTTP stub và unit
  cho password/scope/session/last-admin/file lease. Live Google/UI là `NOT_RUN`.
- Bằng chứng người dùng chạy trên Google thật: live-check configured/live True
  exit 0; access-boundary granted 200/control 404 và command báo pass (không có
  exit code được cung cấp); init exit 0; PoC `DT-DEV-002` unique/replay/stale và
  Drive checksum/AVAILABLE exit 0. AI không chạy lại các lệnh có ghi.
- Xác nhận thủ công của người dùng: tab `TASK004_POC` có record `DT-DEV-002`;
  TXT đúng folder/owner dev; file control tồn tại, tài khoản vận hành mở được,
  chưa qua Picker và không do app tạo. AI không trực tiếp quan sát Google UI.
- Build Android: exit 0, 0 warnings, 0 errors; ADB 37.0.1 không thấy thiết bị, nên chạy app Android là `NOT_RUN`.
- Compile iOS simulator target và `ios-arm64`: exit 0, 0 warnings, 0 errors. Chưa kết nối Mac để kiểm tra Xcode/signing và chưa cài/chạy trên iPhone, nên kiểm thử iOS là `NOT_RUN`.
- TASK-003 trước đó smoke HTTPS 200/status `SCAFFOLD`. Ở checkpoint TASK-004 host
  mới khởi động và lắng nghe HTTPS 7008 + HTTP 5211, HTTP vẫn trả 307; request
  HTTPS bằng curl/Schannel là `NOT_RUN` do `SEC_E_NO_CREDENTIALS` của client trong
  phiên. Build/test và lệnh `google-live-check` không bị ảnh hưởng.
- TASK-002 trước đó đã kiểm tra 29/29 file gói theo manifest và cây 665 nút (15/98/552), toàn bộ `DRAFT`; kết quả vẫn được giữ nguyên.

## Trạng thái quyết định kỹ thuật

| Hạng mục | Trạng thái | Cách tiếp tục |
|---|---|---|
| Persistence nghiệp vụ | **ACCEPTED:** Google Sheets; binary trong My Drive công ty | Live PoC và kiểm tra UI thủ công có user-provided evidence; TASK-004 DONE |
| Web/mobile/backend | **ACCEPTED:** .NET 10, Razor Pages, MAUI Android/iOS, API chung | Theo [ADR-001](02-architecture/ADR-001-proposed-sheets-drive-web-mobile.md) |
| Writer | **ACCEPTED cho demo:** một backend instance; web và auth CLI loại trừ nhau bằng process lease/file lock dùng chung | Lock RAM chỉ tuần tự hóa trong process; không scale-out |
| Phạm vi truy cập | **ACCEPTED:** mạng công ty + Internet qua HTTPS | Host/domain/TLS/reverse proxy vẫn `TBD` |
| Google authorization | **ACCEPTED:** chỉ `drive.file`; Desktop OAuth PKCE/loopback; resource mới provisioning, resource có sẵn Picker; không suy diễn quyền file con | Live 200/404 pass; người dùng xác nhận thủ công file control hợp lệ và resource UI |
| Rule/prefix cấp mã demo | `TBD` | Chốt rule và người phê duyệt trước TASK-007 |
| Ma trận role/scope/chức danh | **ACCEPTED** cho demo tại [PERMISSIONS](01-requirements/PERMISSIONS.md) | Triển khai/test server-side; chức danh không tự cấp quyền |
| Nguồn snapshot tồn và ngưỡng | `TBD` | Chốt trước TASK-010 |
| Thiết bị/signing | Người dùng cho biết Mac/Xcode/iPhone sẵn sàng; chưa xác minh | Kết nối và ghi version/team/device/OS trước khi báo iOS pass |
| Git/remote | `TBD`; hiện không có Git | Không tự khởi tạo hoặc xuất dữ liệu |

## Công việc hiện tại

- [TASK-001](06-tasks/TASK-001-bootstrap.md): `DONE`.
- [TASK-002](06-tasks/TASK-002.md): `DONE`.
- [TASK-003](06-tasks/TASK-003.md): `IN_REVIEW` — giữ nguyên; Google đã có
  evidence, hosting/domain và thiết bị/signing còn mở.
- [TASK-004](06-tasks/TASK-004.md): `DONE` — local build/31 tests, live Google và
  manual tab/TXT/control-file có bằng chứng do người dùng cung cấp.
- [TASK-005](06-tasks/TASK-005.md): `IN_REVIEW` — local format/build/57 tests
  pass; live system spreadsheet và UI/UAT là gate trước `DONE`.
- TASK-006–TASK-014: `TODO`; chưa triển khai.

TASK-005 có [thiết kế auth `ACCEPTED`](02-architecture/AUTH-DESIGN-PROPOSED.md):
năm role cố định, scope gắn role assignment, password chỉ min 8, session/refresh
rotation, bootstrap/recovery command, system spreadsheet riêng, API + Razor Pages
và test bằng dữ liệu giả. `Location.ParentId`, active admin theo assignment hiệu
lực/đủ ALL và process lease liên tiến trình đã được code/test local. Chưa tạo/
chọn/ghi system spreadsheet Google trong lượt local.

## Lịch sử quyết định

- 2026-10-05: dùng `AGENTS.md` và hệ tài liệu `docs/` làm workflow authoritative; TASK-001 hoàn tất.
- 2026-10-06: TASK-002 xác lập cây Excel nháp, Sheets/Drive, web + app Android/iOS và yêu cầu nghiệp vụ.
- 2026-10-06: người dùng chấp nhận ASP.NET Core .NET 10, Razor Pages, .NET MAUI, My Drive qua OAuth tài khoản vận hành, API chung và truy cập Internet; ADR-001 chuyển `ACCEPTED`.
- 2026-10-06: TASK-003 tạo và kiểm tra scaffold local; giữ `IN_REVIEW` do chưa có Google target/credential, host/domain và bằng chứng thiết bị/signing.
- 2026-10-06: người dùng cho phép bắt đầu local TASK-004 mà không đóng các gate
  TASK-003. Đã triển khai OAuth/REST/PoC/fault tests local; live Google chưa chạy
  nên TASK-004 `BLOCKED`, TASK-003 vẫn `IN_REVIEW`.
- 2026-10-07: người dùng thay scope `drive` + `spreadsheets` bằng duy nhất
  `drive.file`, yêu cầu provisioning/Picker và live allow/deny. Local build và
  29 test pass; chưa consent/tạo/ghi Google nên TASK-004 vẫn `BLOCKED`.
- 2026-10-07: người dùng cung cấp kết quả chạy live Google thật cho readiness,
  boundary 200/404, init và PoC `DT-DEV-002`. Sửa HttpClient logging để không ghi
  URL/resource ID hoặc dữ liệu nhạy cảm; build và 31/31 test pass. TASK-004 chuyển
  `IN_REVIEW`, chưa `DONE` vì chưa kiểm tra thủ công tab/TXT và file control.
- 2026-10-07: người dùng xác nhận trực tiếp tab/record `DT-DEV-002`, TXT đúng
  folder/owner và file control hợp lệ nhưng chưa qua Picker/không do app tạo.
  TASK-004 chuyển `DONE`; đây là xác nhận thủ công, không phải AI quan sát.
- 2026-10-07: chuẩn bị thiết kế `PROPOSED` cho TASK-005 theo tám nhóm auth/quyền;
  giữ các quyết định mới ở trạng thái chưa duyệt, không code hoặc gọi Google.
- 2026-10-07: người dùng duyệt ba quyết định nghiệp vụ và phương án demo TASK-005;
  điều chỉnh Location.ParentId, active admin, khóa writer liên tiến trình và mật
  khẩu chỉ tối thiểu 8 ký tự. TASK-005 chuyển `IN_PROGRESS`; live Google để sau.
- 2026-10-07: local TASK-005 hoàn tất implementation/review với format/build và
  57/57 test pass; chuyển `IN_REVIEW`, không `DONE` vì chưa chạy live Google/UI.
