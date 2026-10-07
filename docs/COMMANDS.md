# Lệnh dự án

Working directory cho mọi lệnh:
`D:\Dev\QuanLyTaiSan_DuyTien`.

Solution dùng SDK được pin trong `global.json`:

- .NET SDK 10.0.400; runtime 10.0.11; MSBuild 18.9.6.
- MAUI workload 10.0.20/10.0.100.
- Không có Git worktree.
- Không cần Google credential để build/test local. Live integration TASK-004 dùng
  lệnh riêng; user-run evidence và manual user confirmation đã đủ để `DONE`.

## Restore

```powershell
dotnet restore .\QuanLyTaiSan.DuyTien.sln --nologo
```

Restore RID thiết bị iOS được chạy ngay trước build `ios-arm64` ở phần mobile.

## Build backend/web

```powershell
dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
```

Kết quả checkpoint TASK-004 local: exit 0; 0 warnings; 0 errors.

## Test kiến trúc

```powershell
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal
```

Kết quả checkpoint TASK-004 sau sửa logging: exit 0; passed 31; failed 0;
skipped 0.

Checkpoint local TASK-005: exit 0; passed 57; failed 0; skipped 0. Chi tiết tại
[TEST-REPORT-TASK-005](07-testing/TEST-REPORT-TASK-005.md). Test dùng dữ liệu giả
và fake auth store chỉ trong project test; không thay bằng chứng Google live.

## Build mobile theo từng nền tảng

Android:

```powershell
dotnet build .\src\DuyTien.AssetManagement.Mobile\DuyTien.AssetManagement.Mobile.csproj -f net10.0-android --no-restore --nologo -v:minimal
```

Kết quả TASK-003: exit 0; 0 warnings; 0 errors. Kiểm tra
`adb devices -l` không thấy thiết bị, vì vậy cài/chạy Android là `NOT_RUN`.

iOS compile trên Windows:

```powershell
dotnet build .\src\DuyTien.AssetManagement.Mobile\DuyTien.AssetManagement.Mobile.csproj -f net10.0-ios --no-restore --nologo -v:minimal
dotnet restore .\src\DuyTien.AssetManagement.Mobile\DuyTien.AssetManagement.Mobile.csproj -r ios-arm64 --nologo
dotnet build .\src\DuyTien.AssetManagement.Mobile\DuyTien.AssetManagement.Mobile.csproj -f net10.0-ios -r ios-arm64 --no-restore --nologo -v:minimal
```

Kết quả TASK-003: restore và hai build đều exit 0; 0 warnings; 0 errors. Restore
RID không ký, cài hay chạy app. Đây chỉ là bằng chứng compile target.
Mac/Xcode version, Apple signing team/profile và cài/chạy trên
iPhone chưa được kiểm tra, nên kiểm thử iOS là `NOT_RUN`. Khi kết nối Mac, phải
ghi lại lệnh/thiết bị/version thực tế; không suy diễn từ build Windows.

## Run và smoke local

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https
```

Launch profile hiện dùng `https://localhost:7008` và
`http://localhost:5211`. Smoke đã chạy:

```powershell
curl.exe -k -s -o NUL -w "%{http_code}" https://localhost:7008/
curl.exe -k -s https://localhost:7008/api/system/status
curl.exe -s -o NUL -w "%{http_code} %{redirect_url}" http://localhost:5211/
```

Kết quả TASK-003 trước thay đổi: HTTPS trang Razor 200; API trả `SCAFFOLD`.
Checkpoint TASK-004: host khởi động/lắng nghe HTTPS 7008 và HTTP 5211; HTTP trả
307. HTTPS curl/status `NOT_RUN` do curl Schannel báo `SEC_E_NO_CREDENTIALS` dù
đã dùng `-k`, nên chưa có actual response body mới trong phiên. State code hiện
được cập nhật thành `TASK004_DONE`; chưa chạy lại smoke HTTP
trong lượt review live evidence. `-k` chỉ dành cho smoke local, không phải cấu
hình production.

## Cấu hình ngoài repo và lệnh Google dev

Nhập cấu hình bằng prompt ẩn. ID nhập tay chỉ là tham chiếu, không thay cho
Picker/provisioning:

```powershell
.\scripts\Set-GoogleWorkspaceSecrets.ps1
```

Bootstrap OAuth Desktop app; callback local cố định
`http://127.0.0.1:53682/authorize/`, chỉ request `drive.file` và refresh token tự
lưu user-secrets. Dùng flow này trước khi provisioning resource mới:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-oauth-bootstrap
```

Provisioning chủ động resource mới — **có ghi Google**:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-provision spreadsheet
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-provision folder
```

Hoặc chọn resource có sẵn qua Picker; không thay bằng việc chỉ nhập ID:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-picker-select spreadsheet
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-picker-select folder
```

Kiểm tra live chỉ đọc và ranh giới file granted/ungranted, sau đó hai lệnh PoC có
ghi rõ ràng:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-live-check
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-live-access-boundary-check
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-poc-init
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-poc-run D14-VERIFY-01
```

`google-poc-init` và `google-poc-run` ghi vào Google dev; không chạy trước khi xác
nhận đúng spreadsheet/folder. Chi tiết audience/client/scopes/resource/expected:
[GOOGLE-DEV-SETUP](GOOGLE-DEV-SETUP.md).

Checkpoint 2026-10-07 có bằng chứng người dùng chạy trên Google thật:

- `google-live-check`: configured/live True; exit 0;
- `google-live-access-boundary-check`: granted 200, control 404, command báo pass;
  không ghi exit code vì người dùng không cung cấp;
- `google-poc-init`: initialized or verified; exit 0;
- `google-poc-run DT-DEV-002`: unique/replay/stale/checksum/AVAILABLE pass; exit 0.

Không chạy lại hai lệnh PoC có ghi chỉ để lặp bằng chứng. Người dùng đã xác nhận
thủ công tab/TXT và tính hợp lệ file control; AI không trực tiếp quan sát. Xem
test report TASK-004.

## TASK-005 — system spreadsheet và auth

Các lệnh dưới đây đã được chuẩn bị nhưng **chưa chạy với Google** trong lượt local.
Trước mọi lệnh `auth-system-init`, bootstrap hoặc recovery, phải dừng web server
và bảo đảm không có auth CLI khác chạy. Web và CLI lấy cùng process lease/file
lock; nếu còn writer khác, lệnh sẽ fail thay vì dựa vào lock RAM.

Chọn đúng một cách cấp system spreadsheet dev. Provisioning tạo file mới và có
ghi Google:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-provision system-spreadsheet
```

Hoặc chọn chính spreadsheet hệ thống có sẵn qua Picker:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-picker-select system-spreadsheet
```

Hai flow tự lưu `GoogleWorkspace:SystemSpreadsheetId` ngoài repo mà không in ID.
Không chỉ dán ID để thay cho grant `drive.file`. Sau khi kiểm tra đúng file/ACL,
khởi tạo schema — lệnh này có ghi system spreadsheet:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- auth-system-init
```

Nhập bootstrap secret qua prompt ẩn, không đặt mật khẩu trên command line/chat;
sau đó chạy bootstrap một lần:

```powershell
.\scripts\Set-AuthOperatorSecret.ps1 -Purpose Bootstrap
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- auth-bootstrap-admin
```

Lệnh xóa khóa bootstrap khỏi user-secrets local sau thành công/no-op; secret từ
biến môi trường/secret manager phải được người vận hành xóa hoặc rotate. Start web,
đăng nhập `admin`, đổi mật khẩu bắt buộc và thực hiện UAT bằng dữ liệu giả:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https
```

Phục hồi chỉ dùng khi thực sự không còn admin hoạt động. Dừng web, bật maintenance,
nhập secret qua prompt ẩn, chạy recovery rồi xóa cờ maintenance:

```powershell
dotnet user-secrets set "Auth:MaintenanceMode" "true" --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj
.\scripts\Set-AuthOperatorSecret.ps1 -Purpose Recovery
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- auth-recover-admin
dotnet user-secrets remove "Auth:MaintenanceMode" --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj
```

Recovery bị từ chối nếu còn một user `ACTIVE` có assignment Admin đang hiệu lực
và chính assignment đó đủ `ALL` OrgUnit/Location/CategorySubtree. Sau recovery,
start web và bắt buộc đổi mật khẩu; không có master password/backdoor thường trực.

## Kiểm tra repo/tài liệu

```powershell
dotnet --info
dotnet workload list
rg --files -g '!**/bin/**' -g '!**/obj/**'
rg -n --glob '*.md' 'TBD|PROPOSED|DRAFT|NOT_RUN|IN_REVIEW' .
rg -n -i --glob '*.csproj' --glob '!**/obj/**' 'EntityFramework|Sqlite|SqlServer|Dapper'
```

`TBD`, `DRAFT`, `PROPOSED` và `NOT_RUN` có thể là trạng thái hợp lệ; kiểm
tra nhằm phát hiện tuyên bố mâu thuẫn. Build artifacts trong `bin/obj` được loại
khỏi kiểm tra tài liệu/secret.
