# TASK-003 — Chốt kiến trúc và thiết lập môi trường dev

- Status: IN_REVIEW
- Type: SETUP
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- ADR: [ADR-001](../02-architecture/ADR-001-proposed-sheets-drive-web-mobile.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created/updated: 2026-10-06
- Phụ thuộc: TASK-002 `DONE`
- Gate còn mở: hosting/domain; rule mã và bằng chứng thiết bị/signing
- AC liên quan: AC-13

## Mục tiêu và phạm vi

Chốt stack .NET và tạo scaffold local cho backend/API, Razor Pages, MAUI
Android/iOS và test. Thiết kế ranh giới Google Sheets/Drive, single writer, HTTPS
và cấu hình secret ngoài repo. Không thực hiện chức năng TASK-004–TASK-014; không
tạo Google resource, deploy public, khởi tạo Git, commit hoặc push.

## Quyết định đã chốt

- ASP.NET Core .NET 10 cho backend/API; Razor Pages cho web; .NET MAUI cho
  Android/iOS.
- Web và mobile dùng một API/backend; demo chạy đúng một backend instance để giữ
  single-writer cho Sheets.
- Google Sheets là CSDL; ảnh/tài liệu trong My Drive của tài khoản vận hành công
  ty. Không có SQL/SQLite.
- Backend dùng Google OAuth của tài khoản vận hành. Google identity/credential
  tách với tài khoản đăng nhập ứng dụng.
- Quyết định mới hơn tại TASK-004: chỉ request `drive.file`; resource mới qua
  provisioning, resource có sẵn qua Picker; không suy diễn quyền file con.
- Hệ thống phải dùng trong công ty và từ Internet qua HTTPS. Nhà cung cấp
  host/domain/TLS chưa được chọn và chưa có public deployment.
- Người dùng cho biết Mac, Xcode và iPhone sẵn sàng; phiên bản/signing/device chưa
  được kiểm tra thực tế trong phiên này.

## Kết quả thực hiện

- [x] ADR-001 chuyển `ACCEPTED`, ghi rõ quyết định và giới hạn single instance.
- [x] Pin SDK 10.0.400 trong `global.json`; tạo `QuanLyTaiSan.DuyTien.sln`.
- [x] Tạo Application contracts, Google infrastructure adapters, ASP.NET Core
  Razor/API, MAUI Android/iOS và project test.
- [x] Tạo `ISingleWriterCoordinator` và implementation lock trong tiến trình.
- [x] Tạo interface/adapter Sheets và Drive. Adapter chỉ readiness, không gọi
  Google thật và không báo live connection.
- [x] Fake adapter chỉ nằm trong project test.
- [x] Tạo status endpoint chỉ trả cờ readiness; không trả ID, token hay secret.
- [x] Tạo user-secrets ID cho server nhưng không đặt giá trị; ghi tên khóa tại
  [CONFIGURATION](../CONFIGURATION.md).
- [x] Điền restore/build/test/run thực tế tại [COMMANDS](../COMMANDS.md).
- [x] Không thêm SQL/SQLite, không tạo Google resource, không deploy và không tạo Git.
- [x] Kết nối Google dev và chạy live test — evidence do người dùng cung cấp đã
  ghi tại TASK-004; người dùng đã xác nhận UI thủ công và TASK-004 `DONE`.
- [ ] Chạy Android trên device/emulator — chưa có thiết bị trong `adb devices`.
- [ ] Kiểm tra Mac/Xcode/signing và chạy iOS trên iPhone/simulator — môi trường Mac
  chưa được kết nối trong phiên.
- [ ] Kiểm chứng Internet endpoint — host/domain chưa được xác lập.

## Acceptance criteria

- [x] ADR được xác lập; các mục còn thiếu có owner/gate rõ trong checkpoint.
- [x] Backend/web project build đúng phạm vi.
- [x] MAUI Android và iOS target compile riêng; trạng thái device test được ghi
  `NOT_RUN`, không dùng Android thay bằng chứng iOS.
- [x] Có interface/adapter Google và single-writer; fake không nằm trong runtime.
- [x] Có tên cấu hình dev; secrets nằm ngoài repo; không dùng SQL/SQLite.
- [x] COMMANDS chứa lệnh thực tế và evidence có exit code/test count.
- [ ] Các gate thuộc phạm vi setup mở rộng về Internet host và thiết bị/signing
  thực tế chưa được kiểm chứng; vì vậy Task chưa chuyển `DONE`.

## Bằng chứng kiểm tra

Working directory: `D:\Dev\QuanLyTaiSan_DuyTien`.

| Kiểm tra | Kết quả |
|---|---|
| `dotnet --info` / workload | SDK 10.0.400; runtime 10.0.11; MSBuild 18.9.6; MAUI 10.0.20/10.0.100 |
| Restore solution | exit 0; 5 project restored/up-to-date |
| Build Server | exit 0; 0 warnings; 0 errors; 1.5s (lần kiểm tra cuối) |
| Test ArchitectureTests | exit 0; passed 3; failed 0; skipped 0; 1.1s |
| Build Android `net10.0-android` | exit 0; 0 warnings; 0 errors; 49.9s |
| ADB | adb 37.0.1; không có device; app run `NOT_RUN` |
| Compile iOS simulator `iossimulator-x64` | exit 0; 0 warnings; 0 errors; 3.0s |
| Restore/build `ios-arm64` | restore exit 0; build exit 0; 0 warnings; 0 errors; 2.1s |
| iOS Mac/device | `NOT_RUN`: chưa kiểm tra Xcode, signing hoặc chạy iPhone |
| HTTPS smoke | HTTPS Razor 200; API `SCAFFOLD`; HTTP 307 sang HTTPS |
| Google live | Người dùng chạy trên Google thật: readiness exit 0; boundary 200/404 báo pass; init/PoC exit 0. Chi tiết và manual pending ở TASK-004 |
| Git | `NOT_A_GIT_WORKTREE`; không khởi tạo/commit/push |
| Tài liệu/manifest | 0 link local hỏng; 5/5 project XML parse; 14 Task/14 ID, 0 lệch trạng thái INDEX |
| Guard tĩnh | 0 package SQL/SQLite; 0 mẫu gán secret có giá trị; fake chỉ khớp trong project test |

Lần build `ios-arm64` đầu tiên exit 1 với `NETSDK1047` vì chưa restore RID;
sau `dotnet restore ... -r ios-arm64`, build lại exit 0. Đây là lỗi môi trường
restore đã được xử lý, không phải bằng chứng signing hoặc chạy iOS.

## Review

- Cấu trúc giữ backend làm cổng duy nhất tới Google; mobile không chứa Google
  credential.
- ASP.NET Core HTTPS redirect/HSTS và allow-list local có trong scaffold. Cấu hình
  reverse proxy/forwarded headers chỉ được thêm khi biết host thực tế.
- Single-writer hiện là singleton `SemaphoreSlim`, phù hợp đúng một process.
  Không được chạy nhiều replicas rồi coi lock này là distributed.
- Test xác nhận danh sách khóa thiếu và fake adapter không tự nhận live; live
  Google đã có user-run evidence tại TASK-004, chưa có quan sát UI độc lập của AI.
- Chưa triển khai auth/authorization nghiệp vụ, CRUD, import, report hay upload.
  Đây là giới hạn phạm vi có chủ ý, không phải feature đã hoàn thành.

## Checkpoint để tiếp tục

TASK-003 giữ `IN_REVIEW`. Google gate đã có evidence tại TASK-004; Task tiếp tục
khi nhận các nhóm còn lại:

1. **Internet:** nhà cung cấp hosting, domain dự kiến, đầu mối quản trị DNS/TLS và
   cách backend kết nối mạng công ty.
2. **Nghiệp vụ + thiết bị:** rule cấp mã demo và người xác nhận; Android device/OS;
   Mac hostname/access, macOS/Xcode, Apple signing team/profile, iPhone model/OS.

Sau khi có dữ liệu, không ghi secret vào tài liệu. Kiểm chứng device/signing cập
nhật bằng evidence riêng. TASK-003 chỉ được chuyển `DONE` sau khi các gate bắt
buộc của setup đã được xác nhận hoặc được người dùng tách chính thức sang Task
phụ thuộc.
