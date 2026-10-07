# Thiết lập Google dev cho TASK-004

Trạng thái: implementation local quyền hẹp ngày 2026-10-07 đã build/test. Người
dùng đã cung cấp kết quả chạy live trên Google thật cho readiness,
access-boundary, init và PoC, rồi xác nhận thủ công tab/TXT/file control.
TASK-004 `DONE`; AI không trực tiếp quan sát Google UI. Không chạy lại lệnh ghi
chỉ để lặp evidence.

## 1. Mô hình quyền đã chốt

- Backend dùng OAuth 2.0 **Desktop app**, PKCE và callback loopback cố định
  `http://127.0.0.1:53682/authorize/`. Đây là danh tính tích hợp, không phải tài
  khoản đăng nhập người dùng ứng dụng.
- Code chỉ request đúng một scope
  `https://www.googleapis.com/auth/drive.file` cho cả Sheets API và Drive API.
  Không request `spreadsheets`, `drive`, `drive.readonly` hay scope rộng khác.
- Request không bật `include_granted_scopes`; token response phải chứa **chính
  xác** `drive.file`. Thiếu scope hoặc có thêm scope rộng đều bị từ chối, không
  fallback/retry bằng quyền rộng.
- Resource mới chỉ được tạo khi người vận hành chủ động gọi lệnh provisioning.
  Resource có sẵn phải được chọn trong Google Picker; nhập một ID chỉ cấu hình
  địa chỉ resource, không tạo grant và không được coi là bằng chứng quyền.
- Chọn một folder chỉ cấp quyền với chính folder. Nó không tự cấp quyền cho file
  con có sẵn. File con mới do ứng dụng upload được app tạo nên nằm trong phạm vi
  `drive.file`; file con có sẵn cần được chọn riêng qua Picker trong flow nghiệp
  vụ phù hợp trước khi ứng dụng truy cập.

Google khuyến nghị `drive.file` cho truy cập per-file, và Sheets API hỗ trợ scope
này cho spreadsheet cụ thể:
[Drive scopes](https://developers.google.com/workspace/drive/api/guides/api-specific-auth),
[Sheets scopes](https://developers.google.com/workspace/sheets/api/scopes).

## 2. Cloud project, API và OAuth client

1. Chọn/tạo project **dev** thuộc tổ chức công ty; không dùng nhầm production.
2. Bật **Google Sheets API**, **Google Drive API** và **Google Picker API**.
3. Cấu hình Google Auth Platform. Nếu project thuộc Workspace/Cloud Identity và
   chỉ người trong tổ chức dùng, chọn **Internal**; nếu không, dùng
   **External / Testing** và thêm đúng tài khoản vận hành vào Test users.
4. Tạo OAuth client loại **Desktop app**. Lưu client ID/secret ngoài repo.
5. Trong **Google Auth Platform → Data Access → Add or remove scopes**, chỉ giữ
   `.../auth/drive.file` cho integration này; xóa `.../auth/drive` và
   `.../auth/spreadsheets` nếu đã khai báo. Bấm **Update/Save** và kiểm tra scope
   hiện ở nhóm non-sensitive. Xem [Manage App Data Access](https://support.google.com/cloud/answer/15549135).

Data Access trong Console là trần scope được khai báo; request thực tế vẫn do
code tạo. Vì vậy chỉ sửa Console không sửa được một bản code còn request quyền
rộng. Ngược lại, code đã hẹp nhưng Console/grant cũ còn rộng cũng chưa đủ để live
test đạt.

## 3. Thu hồi grant/token rộng cũ

Nếu client/project/tài khoản này từng consent `drive` hoặc `spreadsheets`:

1. Dừng backend và mọi process đang dùng token.
2. Vào **Google Account → Security → Your connections to third-party apps and
   services**, mở đúng ứng dụng dev và **Remove all access**. Việc revoke có thể
   mất một khoảng ngắn mới có hiệu lực. Google nêu rõ revoke một token sẽ thu hồi
   grant liên quan và các token của authorization đó:
   [OAuth desktop apps — token revocation](https://developers.google.com/identity/protocols/oauth2/native-app).
3. Xóa refresh token local cũ, không in giá trị ra màn hình:

   ```powershell
   dotnet user-secrets remove "GoogleWorkspace:OAuthRefreshToken" --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj
   ```

4. Xóa hai scope rộng ở Data Access như mục 2, rồi bootstrap lại theo mục 5 hoặc
   6. Không tái sử dụng refresh token cũ.

Xóa scope ở Console **không** tự thu hồi grant/token đã phát hành, và cũng không
thay đổi tham số `scope` trong request của code. Cần thực hiện cả code hẹp, Data
Access hẹp, revoke grant/token cũ và consent lại.

## 4. Nhập cấu hình ngoài repo

Chạy prompt ẩn:

```powershell
.\scripts\Set-GoogleWorkspaceSecrets.ps1
```

Luồng bình thường chỉ cần nhập client ID/secret trước; refresh token và resource
ID sẽ được lệnh bootstrap/Picker/provisioning lưu tự động. Script vẫn cho phép
nhập ID để phục hồi cấu hình, nhưng ID không phải grant: resource có sẵn vẫn phải
được Picker trao quyền và live request phải thành công.

User-secrets không nằm trong repo nhưng không phải kho mã hóa. Host thật phải dùng
secret store của nhà cung cấp; không copy `secrets.json` vào artifact.

## 5. Resource mới — provisioning chủ động

Đầu tiên lấy refresh token hẹp. Lệnh chỉ consent, chưa tạo resource:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-oauth-bootstrap
```

Sau đó gọi riêng từng lệnh có ghi Google. Mỗi lệnh tạo đúng một resource mới và
lưu ID vào user-secrets, không in ID:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-provision spreadsheet
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-provision folder
```

Provisioning từ chối ghi đè khi loại resource đó đã có ID cấu hình. Hai lệnh là
hai thao tác không có transaction chung; kiểm tra kết quả từng lệnh trước khi
chạy lệnh tiếp theo. Không có lệnh nào tự chạy lúc khởi động web.

## 6. Resource có sẵn — chọn bằng Google Picker

Không chạy `google-oauth-bootstrap` rồi chỉ dán ID. Dùng Picker cho từng resource:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-picker-select spreadsheet
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-picker-select folder
```

Flow mở system browser với `trigger_onepick=true`, `allow_multiple=false`, filter
đúng MIME type và chỉ scope `drive.file`; callback phải trả đúng một
`picked_file_ids`. Lệnh lưu refresh token hẹp cùng ID được Picker trả về. Xem
[Picker cho desktop/mobile](https://developers.google.com/workspace/drive/picker/guides/desktop-mobile-picker).

Không chọn spreadsheet/folder chứa dữ liệu thật cho PoC. Không bật “anyone with
link”. Folder được chọn không làm cho file con hiện hữu trở thành accessible.

## 7. Live read và kiểm chứng ranh giới quyền

Kiểm tra resource được cấp quyền:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-live-check
```

Pass khi exit 0 và Sheets/Drive cùng `configured=True, live=True`. Lệnh không in
ID/token.

Để chứng minh least privilege, người vận hành chuẩn bị một file dev control có
thật mà tài khoản vận hành mở được trong Drive UI nhưng **không** chọn qua Picker
và không do ứng dụng tạo. Nhập ID file này vào
`GoogleWorkspace:AccessDeniedProbeFileId` bằng script prompt rồi chạy:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-live-access-boundary-check
```

Pass chỉ khi spreadsheet đã cấp quyền truy cập được qua Drive API và file control
nhận 403/404. Trước khi ghi PASS, phải xác nhận thủ công ID control là file có thật
và tài khoản vận hành mở được; nếu không, 404 có thể chỉ là ID sai. Nếu file
control truy cập được, dừng kiểm thử, revoke grant/token rộng và bootstrap lại.

Test HTTP stub local mô phỏng cặp 200/403 nhưng không chứng minh hành vi Google.
Checkpoint 2026-10-07: người dùng chạy live và báo file granted 200, file control
404, command least-privilege pass. Exit code của lệnh này không được cung cấp.
Người dùng sau đó xác nhận thủ công file control có thật, tài khoản vận hành mở
được và file chưa được Picker/app cấp quyền; AI không trực tiếp quan sát.

## 8. Live PoC có ghi Google

Chỉ sau khi mục 7 pass trên resource dev:

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-poc-init
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-poc-run D14-VERIFY-01
```

`google-poc-init` tạo/kiểm tra tab `TASK004_POC`. `google-poc-run` kiểm hai write
không trùng, replay idempotent, stale version bị từ chối, rồi tạo file TXT mới
trong folder, download và kiểm SHA-256. File mới do app tạo nên thuộc grant
`drive.file`; thao tác này không chứng minh app đọc được file con có sẵn.

Checkpoint 2026-10-07: người dùng báo init exit 0 và run `DT-DEV-002` exit 0 với
2 unique writes, replay idempotent, stale-version rejection, Drive checksum và
state `AVAILABLE`. AI không chạy lại. Tab/record và file TXT trong đúng
folder/owner dev được người dùng xác nhận thủ công; AI không trực tiếp quan sát.

## 9. Xử lý lỗi an toàn

- Lỗi `exactly drive.file`: token thiếu scope hoặc còn scope rộng; không fallback.
  Revoke grant/token và consent lại.
- `configured=False`: thiếu khóa cấu hình; chạy prompt hoặc Picker/provisioning.
- `live=False`/403/404 với resource có sẵn: xác nhận đã chọn chính resource đó
  bằng Picker; dán ID hoặc chọn folder cha không đủ.
- `redirect_uri_mismatch`: client phải là Desktop app; callback đúng URI mục 1.
- 429 được retry hữu hạn. Timeout/5xx sau write là kết quả chưa biết và phải đối
  soát Operation/Drive `operationId`, không retry mù hoặc báo success.
- Không chạy nhiều backend process. Lock local không phải distributed lock; sửa
  Sheet thủ công phải dừng writer và đối soát trước khi ghi tiếp.

## 10. Live gate system spreadsheet cho TASK-005 — chưa chạy

TASK-005 dùng spreadsheet hệ thống riêng, không dùng spreadsheet nghiệp vụ hoặc
tab PoC TASK-004. Lượt implementation local không tạo/chọn/ghi resource này.

1. Dừng web server và mọi auth CLI. Cơ chế file lock dùng chung sẽ từ chối
   init/bootstrap/recovery nếu còn process writer khác; lock RAM không đủ.
2. Chọn một trong hai flow:
   - file mới: `google-provision system-spreadsheet`;
   - file có sẵn: `google-picker-select system-spreadsheet`.
3. Xác nhận trực tiếp trong Google UI đúng file dev, đúng owner và ACL chỉ dành
   cho tài khoản vận hành/backend cần thiết. Hidden/protected sheet không thay ACL.
4. Chạy `auth-system-init`; kiểm các tab/header hệ thống, đặc biệt tab
   `RefreshTokens` dùng giữ marker rotate/revoke. `Locations.ParentId` thuộc schema
   nghiệp vụ và được kiểm bằng fixture local trong TASK-005, không được tạo bởi
   lệnh system init. Init không tự chạy lúc web startup.
5. Dùng `Set-AuthOperatorSecret.ps1 -Purpose Bootstrap`, rồi chạy
   `auth-bootstrap-admin`. Không gửi password trong chat/command line. Kiểm user
   `admin`, assignment Admin hiệu lực và đủ ba scope `ALL`; không kiểm plaintext.
6. Start web, đăng nhập, đổi mật khẩu bắt buộc, tạo fixture giả và chạy UAT
   trong/ngoài scope, nhiều role, khóa tài khoản và last-admin. Không dùng dữ liệu
   nhân sự/cơ cấu thật.
7. Restart web và đăng nhập lại để chứng minh bootstrap không chạy theo startup.
   Nếu kiểm idempotency bằng cách chạy lại bootstrap, phải dừng web, cấp secret
   tạm qua prompt ẩn và xác nhận lệnh no-op không đổi hash/role/scope.
8. Ghi exit code, expected/actual, ảnh/chứng cứ UI đã làm sạch và xác nhận thủ
   công. Chỉ khi các bước live này đạt mới xét TASK-005 `DONE`.

Recovery chỉ là diễn tập khi fixture không còn admin hoạt động: dừng web, bật
`Auth:MaintenanceMode`, nhập Recovery secret qua prompt ẩn, chạy
`auth-recover-admin`, rồi tắt/xóa cờ maintenance. Lệnh phải từ chối nếu vẫn còn
admin `ACTIVE` có một assignment Admin hiệu lực đủ ba `ALL`.
