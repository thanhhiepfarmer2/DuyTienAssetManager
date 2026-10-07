# Cấu hình môi trường

Ứng dụng dùng cấu hình phân cấp của ASP.NET Core. File trong repo chỉ chứa giá trị
không nhạy cảm. Secret và token phải được cấp từ user-secrets trong môi trường dev
hoặc secret store/biến môi trường của host; không đưa vào appsettings, tài liệu,
log, APK hay IPA.

## Google Workspace

Backend dùng OAuth 2.0 Desktop app của một tài khoản vận hành công ty để truy cập
Google Sheets và thư mục trong My Drive. Đây là danh tính tích hợp backend, tách
biệt hoàn toàn với tài khoản đăng nhập ứng dụng của admin/người dùng.

| Khóa cấu hình | Biến môi trường tương đương | Loại | Trạng thái |
|---|---|---|---|
| `GoogleWorkspace:OAuthClientId` | `GoogleWorkspace__OAuthClientId` | Secret/config nhạy cảm | Chưa cấp |
| `GoogleWorkspace:OAuthClientSecret` | `GoogleWorkspace__OAuthClientSecret` | Secret | Chưa cấp |
| `GoogleWorkspace:OAuthRefreshToken` | `GoogleWorkspace__OAuthRefreshToken` | Secret/token | Chưa cấp |
| `GoogleWorkspace:SpreadsheetId` | `GoogleWorkspace__SpreadsheetId` | Resource ID | Chưa cấp |
| `GoogleWorkspace:SystemSpreadsheetId` | `GoogleWorkspace__SystemSpreadsheetId` | Resource ID spreadsheet auth riêng | `ACCEPTED`; chưa tạo/cấp/live |
| `GoogleWorkspace:DriveFolderId` | `GoogleWorkspace__DriveFolderId` | Resource ID My Drive | Chưa cấp |
| `GoogleWorkspace:AccessDeniedProbeFileId` | `GoogleWorkspace__AccessDeniedProbeFileId` | Resource ID control chỉ dùng live test | Chưa cấp |

Không nhập giá trị trực tiếp vào command history. Dùng script prompt ẩn; giá trị
để trống giữ nguyên cấu hình hiện có:

```powershell
.\scripts\Set-GoogleWorkspaceSecrets.ps1
```

Bootstrap/recovery admin dùng script prompt ẩn riêng, không yêu cầu gửi mật khẩu
trong chat hoặc đặt mật khẩu vào command line:

```powershell
.\scripts\Set-AuthOperatorSecret.ps1 -Purpose Bootstrap
.\scripts\Set-AuthOperatorSecret.ps1 -Purpose Recovery
```

OAuth bootstrap dùng PKCE và loopback chính xác
`http://127.0.0.1:53682/authorize/`, sau đó tự lưu refresh token vào user-secrets
mà không in token. Code chỉ request đúng `drive.file` cho cả Sheets/Drive, không
bật incremental grant và từ chối token response thiếu/thừa scope. Resource mới
được tạo bằng lệnh provisioning chủ động; resource có sẵn phải được Google Picker
trả về. `SpreadsheetId`/`DriveFolderId` chỉ là địa chỉ, tự nhập ID không phải bằng
chứng đã cấp quyền; chọn folder không cấp quyền file con có sẵn. Chi tiết Data
Access, revoke grant/token rộng, Picker/provisioning và live evidence:
[GOOGLE-DEV-SETUP](GOOGLE-DEV-SETUP.md).

TASK-004 local đã thay readiness-only bằng REST implementation. Người dùng đã
cung cấp kết quả chạy live trên Google thật; fake/stub và
`LiveConnectionVerified=false` vẫn không tự là bằng chứng live. Credential và
resource ID tiếp tục ở ngoài repo. Lượt review không tạo project/client/resource,
không đổi sharing và không chạy lại PoC có ghi.

## Auth ứng dụng cho TASK-005 — `ACCEPTED`

Các tên khóa dưới đây đã được duyệt; không chứa giá trị thật:

| Khóa cấu hình | Biến môi trường tương đương | Loại | Giá trị đề xuất |
|---|---|---|---|
| `Auth:Bootstrap:AdminPassword` | `Auth__Bootstrap__AdminPassword` | Secret dùng một lần | Không có default; xóa/rotate sau bootstrap |
| `Auth:Recovery:AdminPassword` | `Auth__Recovery__AdminPassword` | Secret phục hồi dùng một lần | Chỉ cấp trong maintenance mode rồi xóa/rotate |
| `Auth:Password:MinimumLength` | `Auth__Password__MinimumLength` | Cấu hình cố định demo | 8; không có max/complexity/blocklist/username rule |
| `Auth:Lockout:MaxFailedAttempts` | `Auth__Lockout__MaxFailedAttempts` | Cấu hình | 5 |
| `Auth:Lockout:DurationMinutes` | `Auth__Lockout__DurationMinutes` | Cấu hình | 15 |
| `Auth:RateLimit:LoginAttempts` | `Auth__RateLimit__LoginAttempts` | Cấu hình | 10 |
| `Auth:RateLimit:WindowMinutes` | `Auth__RateLimit__WindowMinutes` | Cấu hình | 15 |
| `Auth:Sessions:WebIdleMinutes` | `Auth__Sessions__WebIdleMinutes` | Cấu hình | 30 |
| `Auth:Sessions:WebAbsoluteHours` | `Auth__Sessions__WebAbsoluteHours` | Cấu hình | 8 |
| `Auth:Sessions:RememberIdleHours` | `Auth__Sessions__RememberIdleHours` | Cấu hình | 24 |
| `Auth:Sessions:RememberAbsoluteDays` | `Auth__Sessions__RememberAbsoluteDays` | Cấu hình | 7 |
| `Auth:Sessions:MobileAccessMinutes` | `Auth__Sessions__MobileAccessMinutes` | Cấu hình | 15 |
| `Auth:Sessions:MobileRefreshIdleDays` | `Auth__Sessions__MobileRefreshIdleDays` | Cấu hình | 14 |
| `Auth:Sessions:MobileRefreshAbsoluteDays` | `Auth__Sessions__MobileRefreshAbsoluteDays` | Cấu hình | 30 |
| `Auth:Sessions:MaxActivePerUser` | `Auth__Sessions__MaxActivePerUser` | Cấu hình | 5 |
| `Auth:SystemWriterLockFilePath` | `Auth__SystemWriterLockFilePath` | Đường dẫn local trên host | Mặc định dưới thư mục binary đang chạy; mọi process dùng cùng system sheet phải dùng đúng cùng đường dẫn tuyệt đối |
| `Auth:MaintenanceMode` | `Auth__MaintenanceMode` | Cờ vận hành | `false`; recovery chỉ chạy khi `true` |

- Username bootstrap cố định là `admin`, không cần secret/config riêng.
- Không đưa mật khẩu bootstrap/recovery vào `appsettings*.json`, tài liệu, command
  history, log, client, Sheet plaintext hay build artifact.
- Lệnh `auth-bootstrap-admin`/`auth-recover-admin` đọc secret qua
  ASP.NET Core configuration. User-secrets dev có thể được lệnh xóa sau thành
  công; env/secret manager phải do người vận hành xóa hoặc rotate.
- `SystemSpreadsheetId` được lưu ngoài client và chỉ có sau khi người vận hành
  chủ động provisioning hoặc Picker. Lượt thiết kế này không tạo/chọn resource.
- Chỉ yêu cầu mật khẩu có tối thiểu 8 ký tự. Không áp dụng bất kỳ điều kiện nội
  dung/độ mạnh nào khác cho create/bootstrap/reset/change.
- Chi tiết chính sách tại
  [AUTH-DESIGN-PROPOSED](02-architecture/AUTH-DESIGN-PROPOSED.md).

## Host và HTTPS

- Local dùng launch profile HTTPS của ASP.NET Core. Chứng thư dev trên máy hiện có
  nhưng chưa được trust toàn hệ thống.
- Môi trường sử dụng phải công bố một HTTPS endpoint dùng được từ mạng công ty và
  Internet. Nhà cung cấp host, domain, TLS/reverse proxy và `ASPNETCORE_URLS`
  chưa được xác lập.
- Demo chạy đúng một backend instance. Web giữ process lease bằng file lock hệ
  điều hành; init/bootstrap/recovery lấy cùng lease và fail nếu web/CLI khác đang
  giữ. Lock RAM chỉ tuần tự hóa trong process; không được scale-out trước khi có
  coordinator dùng chung và ADR bổ sung.
- Mật khẩu bootstrap/recovery admin sau này phải cấp ngoài repo theo proposal
  TASK-005. TASK-003 chưa triển khai đăng nhập và không tạo khóa/mật khẩu mẫu.

## Mobile

Mobile gọi API chung qua HTTPS; không chứa Google credential. Base URL, signing
Android/iOS và cấu hình phân phối sẽ được thêm khi hosting/domain và thiết bị mục
tiêu được xác lập. Build target hiện tại chỉ gồm Android và iOS.
