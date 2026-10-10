# Lệnh dự án

Working directory cho mọi lệnh:
`D:\Dev\QuanLyTaiSan_DuyTien`.

Solution dùng SDK được pin trong `global.json`:

- .NET SDK 10.0.400; runtime 10.0.11; MSBuild 18.9.6.
- MAUI workload 10.0.20/10.0.100.
- Hiện có Git worktree: `main`, remote `origin`, HEAD `b324c74` kiểm tra ngày
  2026-10-09; không commit/push/deploy trong lượt này.
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
đã dùng `-k`, nên checkpoint đó chưa có actual response body mới. State code
của checkpoint TASK-004 là `TASK004_DONE`. Source TASK-005 hiện dùng hằng
`TASK005_LOCAL_IN_PROGRESS` ở status endpoint; chưa gọi lại endpoint đó trong
lượt 2026-10-09, không lấy hằng này để quyết định trạng thái Task/UAT. `-k` chỉ dành cho smoke local, không phải cấu
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

Checkpoint local 2026-10-07 chưa chạy các lệnh dưới đây với Google. Ngày
2026-10-09 đã chạy provisioning `system-spreadsheet` và `auth-system-init`
(exit 0), owner/ACL/scope/schema được AI xác minh trực tiếp. Bootstrap/no-op đã chạy hai CLI exit 0; khóa bootstrap xóa. Recovery chưa chạy. Không chạy lại provisioning: ID DEV đã có.
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

## TASK-005 — kiểm tra live/UAT đã làm sạch

```powershell
# Chỉ đọc Google: kiểm scope, DEV riêng, owner-only ACL, 13 tab/schema/5 role và số user.
.\scripts\Test-Task005GoogleDev.ps1

# Sau khi start đúng một backend local; kiểm HTTP chưa đăng nhập, không nhập credential.
python -B .\scripts\Test-Task005WebPreflight.py

# Chỉ chạy tương tác sau bootstrap và đổi mật khẩu admin trong Razor Pages.
# Prompt ẩn; ghi fake user/audit lên system spreadsheet DEV; không in password/token.
python -B .\scripts\Test-Task005MobileLive.py
```

Ngày 2026-10-09: Google probe exit 0; HTTP probe exit 0, 12/12 assertion;
mobile runner **NOT_RUN**, mới kiểm tra syntax. HTTPS certificate exception
chỉ dùng cho URL cố định localhost trong probe, không sửa trust store hoặc
cấu hình triển khai. HTTP probe không chứng minh browser/layout/cookie đăng nhập.
Mobile runner tự giữ token trong RAM, rotate token admin khi gần hết hạn và
checkpoint trước giãn request 16 giây; bản sửa batch hiện giãn 5 giây; không thay timeout auth. Tài khoản giả
`uat005_*` được giữ lại để audit, Admin có thể reset qua flow đã có; không xóa.
Phải chạy Google probe trước để biết đúng đích DEV, không chạy runner cùng lúc
với UAT khác. Runner không chứng minh restart/last-admin/expiry theo thời gian
thực/browser hay MAUI; các gate đó nằm trong
[UAT-CHECKLIST-TASK-005](07-testing/UAT-CHECKLIST-TASK-005.md).

Build và test bản sửa API chạy tuần tự: exit 0, 0 warning/0 error, 58 passed,
0 failed, 0 skipped. Bằng chứng chi tiết tại report TASK-005. Web cuối lượt
đã dừng; không để server giữ lease khi nhập secret rồi chạy bootstrap.

## TASK-005 — tiếp tục bootstrap/UAT sau đổi mật khẩu

Ngày 2026-10-09: hai CLI `auth-bootstrap-admin` đã exit 0. Lần đầu tạo admin;
lần hai no-op với secret khác, snapshot user/hash/version/role/ALL không đổi,
khóa bootstrap đã xóa. Driver phiên bản đầu exit 1 sau 10 assertion bootstrap,
trước nhánh web; không coi toàn driver PASS. Driver hiện chỉ kiểm bootstrap:

```powershell
# Chỉ khi cần bootstrap/no-op và web đã dừng, secret bootstrap đã được nhập.
.\scripts\Test-Task005BootstrapLive.ps1
```

Người dùng xác nhận đã đổi mật khẩu bắt buộc và đăng nhập lại. Để AI UAT bằng
mật khẩu mới mà không đưa credential vào args/chat/log, dùng khóa tạm ngoài repo:

```powershell
.\scripts\Set-Task005UatSecret.ps1
python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret --restart-checkpoint
```

Prompt ẩn lưu `UatTask005:AdminPassword` trong user-secrets của server. Runner
đọc trong RAM và xóa đúng khóa này ở `finally`, cả khi fail; không sửa các khóa
khác và giữ replacement nếu người dùng thay credential trong lúc chạy. Nếu bị
kill/host crash trước `finally`, phải xóa khóa qua user-secrets, không in giá trị.
`File.Replace` PowerShell dùng `NullString.Value` cho backup null; Python dùng
`os.replace`. Không chạy lại bootstrap chỉ để nhập mật khẩu admin đã đổi.

Ở checkpoint `RESTART_CHECKPOINT_READY`, AI dừng backend đang giữ lease, xác minh
port/process đã dừng, start lại đúng một backend bằng lệnh Run ở trên; sau khi
listening tạo marker temp theo đường dẫn runner in (không chứa secret). Runner
xóa marker sau khi nhận, timeout 10 phút. Không dùng stdin để chuyển credential. Runner
kiểm mobile session/identity và cookie Razor tồn tại sau restart, đăng nhập lại
bằng password mới; last-admin chỉ thử nếu API chứng minh đúng một user bootstrap.
Các ca bổ sung: cookie flags/default/remember, anti-forgery thiếu/sai, cookie bị
chặn API bearer, Viewer access denied, cookie revoke khi đổi quyền/khóa/reset,
sáu phiên/max 5, logout. Đây là HTTP live; tương tác/layout browser vẫn cần manual.
Chưa gán PASS trước actual output/exit; tạm ngừng UAT browser khác khi runner
đang gọi Google để tránh dồn quota.

Checkpoint cuối: bản sửa Razor scope đã format verify/build sạch/test 61/61 exit 0, chạy tuần tự. Web đã dừng, process/port 0. Runner lần đầu đạt 9 assertion rồi exit 1 tại checkpoint stdin EOF và xóa credential; bản marker đã syntax pass nhưng chưa rerun, cần nhập lại prompt UAT. Các đoạn NOT_RUN phía trên mô tả checkpoint trước, report là nguồn actual hiện tại.


Continuation UAT hiện tại: dùng lại credential tạm người dùng đã lưu; Google
read probe exit 0, đúng một admin. Không provisioning/init/bootstrap lại.
Runner thêm hai fixture hoàn toàn giả riêng để kiểm access sau 15 phút thật,
refresh sau expiry, lần sai thứ5 lock15 phút, correct-password bị chặn trong
lockout, attempt11 trả429 và login sau lockout expiry. Deadline tự trôi trong
khi các case khác chạy; không sửa timeout cấu hình. Cùng lệnh
`python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret --restart-checkpoint`.
Fixture/audit được giữ lại, credential/token chỉ trong RAM; chỉ actual output
mới là bằng chứng, không coi syntax pass hoặc thời gian chưa trôi là PASS.


Checkpoint restart mới: mobile/cookie/identity/login sau restart và last-admin
API409 ba thao tác đã đạt. Runner20pass/1fail(exit1) do sai URL Razor, đã sửa
/Detail và probe anonymous route; chưa full suite PASS. Credential/marker đã
xóa/xác minh; web một instance đang chạy. Nhập lại prompt UAT rồi rerun; không
lặp provisioning/init/bootstrap. Viewer/revoke/token/audit và browser còn thiếu.
Marker precondition phải đếm unique ports2/owner1, localhost có4 rows IPv4+IPv6.


## Tiếp tục sau sửa quota Google

Build sạch/78 tests local exit0; hiện web dừng, credential UAT đã xóa.
Provisioning/init/bootstrap và restart đã có bằng chứng, không lặp setup.
Prompt nhập lại credential, AI start backend bằng lệnh Run và chạy:

```powershell
python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret
```

Sau batch-read/no-op fix, API/Razor giãn tối thiểu 5 giây; không chạy browser
song song. Runner thêm actual access15 phút/lockout15 phút/rate limit fixtures;
không sửa timeout. Cleanup finally và boolean kiểm khóa absent là bắt buộc.
Các mô tả NOT_RUN/checkpoint cuối phía trên là lịch sử; nguồn actual mới nhất
là Test Report. Browser vẫn cần người dùng xác nhận riêng.


Checkpoint live sau sửa quota: cùng lệnh --operator-secret (không restart flag)
exit0/173 assertions; preflight12/12 exit0; Google read probe sau run exit0/Users4;
credentialUAT/BootstrapAbsent=True đã kiểm riêng. Server giữ1instance cho browser.
Không nhập secret/rerun runner khi chỉ làm browser. TASK-005 IN_REVIEW vì browser
và timeout dài chưa đủ.

Format thực tế cho bản sửa quota (chạy tuần tự, cả hai exit0):

```powershell
dotnet format .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --include src\DuyTien.AssetManagement.Infrastructure.Google\Adapters\GoogleAuthSystemStore.cs src\DuyTien.AssetManagement.Server\Authentication\AuthStorageAvailabilityExtensions.cs src\DuyTien.AssetManagement.Server\Authentication\OpaqueSessionAuthenticationHandler.cs src\DuyTien.AssetManagement.Server\Program.cs src\DuyTien.AssetManagement.Server\Pages\Admin\Users\Index.cshtml.cs
dotnet format .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --include tests\DuyTien.AssetManagement.ArchitectureTests\GoogleAuthSystemStoreTests.cs tests\DuyTien.AssetManagement.ArchitectureTests\AuthApiIntegrationTests.cs
```


## TASK-005 — kiểm timeout bổ sung (2026-10-10)

Chỉ thêm test, không đổi mã server/chính sách. Web tiếp tục chạy; build riêng test
không build lại dependency/output server, rồi chỉ chạy lớp test mới:

```powershell
dotnet build .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --no-dependencies --nologo -v:minimal
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-build --no-restore --nologo -v:minimal --filter FullyQualifiedName~AuthSessionTimeoutTests
```

TimeProvider giả lập, fake store; giữ nguyên default30min/8h/24h/7d/14d/30d.
Không thay W13 real elapsed; không chạy lại toàn suite78 hoặc live173 đã đạt.


## TASK-005 — runner chỉ W10 và W13 thực (2026-10-10)

Không chạy lại suite 173 assertion. Dùng một backend HTTPS7008 đang chạy, resource
DEV owner-only/exact drive.file đã kiểm trước khi ghi; credential operator nhập
qua Set-Task005UatSecret.ps1. Runner không nhận password qua args/stdin; phục hồi
admin chính trong finally, xóa credential ngay sau setup và xác minh absent.

```powershell
python -B -c "import ast; from pathlib import Path; ast.parse(Path('scripts/Test-Task005RemainingLive.py').read_text(encoding='utf-8')); print('SyntaxOK=True')"
python -u -B .\scripts\Test-Task005RemainingLive.py --start-web-timeouts
```

Lệnh thứ hai có thể chạy nền bằng Start-Process -WindowStyle Hidden, stdout/stderr
đến file tạm ngoài repo (output runner chỉ expected/actual/status). Không ghi secret
trong command/log. Script không overwrite artifact đã tồn tại hoặc admin dự phòng
đã có; trường hợp đó phải kiểm checkpoint trước, không rerun mù.

W10: tạo admin_second_demo nếu absent, Admin/ALL ba chiều hiệu lực, đổi password
đầu tiên, xác minh login/API/Razor HTTP; khóa/hạ role/bỏ ALL của admin chính được
phép khi còn backup rồi phục hồi. Credential backup chỉ RAM; muốn dùng tiếp qua
browser, admin chính có thể reset qua UI và người vận hành tự đổi password.
W13: bốn user giả độc lập, cookie chỉ RAM; idle thật30ph/24h không hoạt động,
absolute thật8h/7ngày giữ idle bằng GET mỗi20ph/12h. Không đổi chính sách hoặc
đồng hồ. Artifact TASK-005-REMAINING-LIVE-EVIDENCE.json ghi PENDING đến deadline,
đứt process/server hoặc request lỗi không là PASS. HTTP không chứng minh browser.

Lệnh launch thực tế 2026-10-10 (không có secret args):

```powershell
$runnerPython = (Get-Command python -ErrorAction Stop).Source
$runnerStdout = Join-Path ([IO.Path]::GetTempPath()) 'DuyTien-Task005-Remaining-20261010.out.log'
$runnerStderr = Join-Path ([IO.Path]::GetTempPath()) 'DuyTien-Task005-Remaining-20261010.err.log'
# Đã kiểm cả hai path absent trước launch; không overwrite checkpoint.
$runnerProcess = Start-Process -FilePath $runnerPython -ArgumentList @('-u','-B','scripts\Test-Task005RemainingLive.py','--start-web-timeouts') -WorkingDirectory (Get-Location).Path -WindowStyle Hidden -RedirectStandardOutput $runnerStdout -RedirectStandardError $runnerStderr -PassThru
$runnerProcess.Id
```

Launch exit0/PID18964; không đồng nghĩa runner exit0. Artifact clean ghi tiến độ,
runner còn chờ deadline. Không rerun/create duplicate khi process còn sống.

```powershell
dotnet format .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --verify-no-changes --include tests/DuyTien.AssetManagement.ArchitectureTests/AuthSessionTimeoutTests.cs --verbosity minimal
```

Verify riêng file mới exit0; không chạy lại suite78 hoặc live173.

## TASK-006 local — 2026-10-10

Dừng đúng backend của workspace trước build để tránh tranh chấp output. Không dùng credential UAT/Google live trong test local. Build/test tuần tự:

```powershell
dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~Category"
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-build --no-restore --nologo -v:minimal --filter "FullyQualifiedName~ScopeAuthorizationTests|FullyQualifiedName~AuthApiIntegrationTests|FullyQualifiedName~AuthRazorUserDetailTests"
git diff --check
```

Razor `/Categories`, `/Categories/Detail/{id}`, `/Categories/Import` có antiforgery.
API bearer: GET `/api/categories?q=...`, `/tree`, `/{id}`, `/export`; POST `/api/categories/imports/preview` multipart trường `file`; POST `/imports/commit` JSON `{previewId, confirmDuplicateNames}`. Preview gắn actor/TTL 15 phút (không đổi chính sách phiên auth), commit retry trong TTL dùng ledger key server. Restart trước commit cần upload/preview lại; retry file đã commit không tạo bản sao. Không auto import staging/startup.

Format TASK-006: từng csproj Application/Infrastructure.Google/Server/ArchitectureTests, `dotnet format <csproj> --no-restore --include <absolute paths các file mới TASK-006> --verbosity minimal`; bốn lệnh exit0. Include Application/Categories; adapter GoogleCategoryCatalogStore.cs; Server/Categories và Pages/Categories; bốn Category* test files. Không format toàn repo hoặc auth/user file đang có. `node --check .\src\DuyTien.AssetManagement.Server\wwwroot\js\category-import.js` kiểm syntax JS, không chứng minh browser. Không tiếp tục runner W13 hoặc secret UAT theo chỉ đạo dừng hiện hành.
