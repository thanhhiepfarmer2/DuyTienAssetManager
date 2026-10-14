# QuanLyTaiSan_DuyTien

Web LocalOffline (TASK-015): sau prepare Release một lần, chạy [DuyTien-LocalOffline.cmd](DuyTien-LocalOffline.cmd) để mở https://localhost:7008, không build/download mỗi lần và không cần Google/Internet. Workspace ở `%LOCALAPPDATA%\DuyTienAssets\LocalOffline\default` ngoài Git. Xem [cấu hình](docs/CONFIGURATION.md), [hướng dẫn](docs/WEB-USER-GUIDE.md), [lệnh](docs/COMMANDS.md), [bằng chứng và giới hạn](docs/07-testing/TEST-REPORT-TASK-015.md). Không tự seed/reset/sync; Android tạm dừng, iOS DEFERRED_BY_USER.


Ứng dụng **Mã số hóa tài sản Công ty TNHH Duy Tiến** đang được phát triển theo
từng Task nhỏ, có kiểm thử và bằng chứng.

## Trạng thái hiện tại

- TASK-001 và TASK-002 là `DONE`; cây Excel hiện tại gồm 665 nút vẫn là `DRAFT`.
- [ADR-001](docs/02-architecture/ADR-001-proposed-sheets-drive-web-mobile.md) đã
  `ACCEPTED`: ASP.NET Core .NET 10, Razor Pages, .NET MAUI Android/iOS, một
  backend/API và single writer cho Google Sheets.
- Google Sheets là CSDL; ảnh/tài liệu ở My Drive của tài khoản vận hành công ty.
  Backend dùng Google OAuth riêng với tài khoản đăng nhập ứng dụng và chỉ request
  `drive.file`; resource mới qua provisioning, resource có sẵn qua Picker.
- TASK-003 đã tạo solution và scaffold local nhưng đang `IN_REVIEW`: Google đã có
  user-run evidence; host/domain và chạy app trên thiết bị Android/iOS còn mở.
- TASK-004 đang `IN_REVIEW`: local pass 31 test; live Google quyền hẹp/PoC có
  evidence do người dùng chạy, còn manual tab/TXT/control-file validation.
- Thư mục chưa phải Git worktree; không có remote/commit và TASK-003 không khởi tạo Git.

## Cấu trúc

- `src/DuyTien.AssetManagement.Application`: interface và contract dùng chung.
- `src/DuyTien.AssetManagement.Infrastructure.Google`: adapter Sheets/Drive và single-writer.
- `src/DuyTien.AssetManagement.Server`: ASP.NET Core API + Razor Pages.
- `src/DuyTien.AssetManagement.Mobile`: .NET MAUI Android/iOS.
- `tests/DuyTien.AssetManagement.ArchitectureTests`: test scaffold/ranh giới kiến trúc.
- `docs/`: Requirement, ADR, Plan, Task, cấu hình và bằng chứng.

Adapter Google đã có REST implementation, OAuth refresh, Picker/provisioning CLI,
single-writer và PoC. Kết quả live được ghi tách khỏi fake/HTTP stub trong test
report; ID/token không nằm trong repo. Không có SQL/SQLite làm persistence.

## Bắt đầu

1. Đọc [bối cảnh](docs/PROJECT-CONTEXT.md), [REQ-001](docs/01-requirements/REQ-001-asset-digitization.md) và [ADR-001](docs/02-architecture/ADR-001-proposed-sheets-drive-web-mobile.md).
2. Xem [TASK-004](docs/06-tasks/TASK-004.md) và [INDEX](docs/06-tasks/INDEX.md).
3. Dùng các lệnh thật tại [COMMANDS](docs/COMMANDS.md).
4. Cấp secret ngoài repo theo [CONFIGURATION](docs/CONFIGURATION.md), chỉ khi đã có tài nguyên dev được phê duyệt.
