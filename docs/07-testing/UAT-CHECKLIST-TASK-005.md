# Checklist live/UAT TASK-005

## Quyết định hiện hành 2026-10-10 — hoãn nghiệm thu theo người dùng

TASK-005 giữ `IN_REVIEW`. Người dùng chấp nhận hoãn các gate còn lại để bắt đầu
TASK-006 local; đây là ngoại lệ phụ thuộc đã được cho phép, không phải TASK-005 DONE.
G2a (bốn bước đã xác nhận) và G2b là `PASS_MANUAL`. G1a/G1b, Reset/Login và các
xác nhận trước được giữ nguyên làm bằng chứng lịch sử. Theo chỉ đạo mới nhất,
G2c chưa hoàn tất nghiệm thu và ghi `DEFERRED_BY_USER`; các xác nhận G2c/G2d trước
đó không bị xóa, không được dùng để suy diễn hoàn tất checklist hiện tại.
Các mục browser còn mở (kể cả G2e/revoke/logout/audit/validation chưa xác nhận)
và timeout dài chưa đủ bằng chứng: `DEFERRED_BY_USER`, không PASS.

Runner W13 do task tạo: PID 18964, tên/script/creation time đã đối chiếu trước
Stop-Process; lệnh stop exit 0, kiểm tra sau thấy runner không còn. Không quan sát
exit code của runner. Ba timer 8 giờ/24 giờ/7 ngày: `STOPPED_BY_USER`, giữ các
keepalive đã quan sát. Idle 30 phút thực: `PASS_REAL_ELAPSED` tại
2026-10-10T07:44:57.311761Z (302 login); 14 boundary tests dùng thời gian giả lập
không thay bằng chứng thời gian thực. AI kiểm tra secret store chỉ xuất boolean:
credential UAT và bootstrap đều không còn. Không xóa user, audit hoặc kết quả.
Chi tiết: [artifact W13](TASK-005-REMAINING-LIVE-EVIDENCE.json).


Checkpoint: 2026-10-10; TASK-005 **IN_REVIEW**. Dữ liệu/tài khoản hoàn toàn giả.
Không gửi password/token/hash vào chat, screenshot hoặc report. Cột Actual chỉ
điền điều được quan sát; không tự gán PASS/exit code. Browser automation hiện
không có browser khả dụng. [Test Report](TEST-REPORT-TASK-005.md) là nguồn bằng
chứng; checklist này chỉ hướng dẫn thao tác và expected.

## Các gate hiện hành sau quyết định dừng

| Mục | Kết quả hiện hành | Bằng chứng giữ nguyên |
|---|---|---|
| G2a / W02 | PASS_MANUAL bốn bước; phần chưa xác nhận DEFERRED_BY_USER | Người dùng: rỗng, thiếu tên, tạo hợp lệ, trùng không thêm bản sao |
| G2b / W03-W04 | PASS_MANUAL | Người dùng: tên/Viewer/scope/cấp con/GET reopen đúng |
| G2c / B05 / một phần W01-W05 | DEFERRED_BY_USER theo chỉ đạo mới; chưa hoàn tất nghiệm thu | Xác nhận manual trước vẫn nằm trong lịch sử, không xóa hoặc suy diễn hoàn tất |
| G2d | Xác nhận PASS_MANUAL lịch sử giữ nguyên | Viewer ba URL và form password theo người dùng; không dùng để hoàn tất các case khác |
| G2e trở đi; phần còn thiếu B04/B05/B06/W01-W08/W11/W12 | DEFERRED_BY_USER | HTTP/Google đã pass giữ nguyên; browser invalid/one-response/editor/revoke/lock/reset/logout/audit/flags/layout chưa đủ không PASS |
| W09 | PASS_HTTP_GOOGLE + PASS_MANUAL | Bảo vệ admin cuối cùng, G1b đã xác nhận |
| W10 | PASS_HTTP_GOOGLE | Admin dự phòng/login/assignment, phục hồi primary; không xóa user/audit |
| W13 default idle 30 phút | PASS_REAL_ELAPSED | HTTP trực tiếp UTC07:44:57.311761Z, cookie từ RAM redirect login |
| W13 default absolute8h/remember idle24h/absolute7d | STOPPED_BY_USER; gate DEFERRED_BY_USER | Giữ keepalive trước stop; runner không còn, không quan sát expiry hoặc exit code runner |

## Checkpoint lịch sử trước quyết định hoãn — 2026-10-10

- G2d (2026-10-10): PASS_MANUAL theo người dùng cho uat005_ui_1010_02
  (Viewer): /Admin/Users, /Admin/Authorization, /Admin/SecurityEvents đều
  chuyển AccessDenied/Không có quyền, không lộ dữ liệu quản trị/không về login.
  /Account/ChangePassword vẫn mở được, không cảnh báo bắt buộc đổi. Không có
  exit code. Editor/browser revoke/lock/reset/logout/layout còn chờ G2e trở đi.
- G2c (2026-10-10): PASS_MANUAL theo người dùng cho cả bước1/2/3, đúng
  uat005_ui_1010_02 sau reset: /Admin/Users trước đổi quay về ChangePassword;
  current sai, new7, confirm khác đều validation/không đổi password; password
  tự chọn8 chữ thường đổi thành công/về login; password tạm trước đổi bị từ
  chối, password mới login được/không bắt đổi lại. Không có exit code. Đây là
  flow sau reset, không phải login bằng password tạo ban đầu đã mất. G2d Viewer
  access denied đã PASS_MANUAL; revoke/lock/reset/logout/layout còn chờ.
- Reset/Login (2026-10-10): PASS_MANUAL theo người dùng, đúng
  uat005_ui_1010_02: admin reset, user login bằng password tạm mới và được
  yêu cầu đổi password. Không có exit code. Đã giải quyết việc mất password
  tạm; G2c validation đổi password/login sau đổi nay PASS_MANUAL. Chưa có browser
  evidence thu hồi cookie đang hoạt động/reset-old-password/audit riêng W08.
- Lịch sử G2c: người dùng quên password tạm, đã hướng dẫn reset đúng user;
  bước Reset/Login nay PASS_MANUAL. Không suy diễn PASS cho validation hoặc
  thu hồi phiên cũ trước khi có kết quả riêng.
- W13 default idle30ph PASS_REAL_ELAPSED: runner gửi cookie gốc từ RAM tới
  /Account/ChangePassword lúc 2026-10-10 07:44:57 UTC (14:44:57 giờVN), nhận302
  về /Account/Login; session tạo07:14:50 UTC, idle deadline07:44:50 UTC. Trước
  đó protected GET200, không request giữ idle của fixture này trong30ph. Nguồn
  AI chạy/quan sát HTTP thực, không là browser. Ba mốc8h/24h/7ngày vẫn pending;
  runner PID18964 còn sống, chưa có exit code. Credential tạm đã cleanup.
- G2b (2026-10-10): PASS_MANUAL theo xác nhận người dùng cho
  uat005_ui_1010_02: đổi tên UAT Viewer 1010-02, gán Viewer/Active với
  OU-DEMO-A, LOC-DEMO-A, CAT-DEMO-EQUIPMENT/cấp con; GET mở lại giữ đúng
  tên, role, scope và checkbox. Không có exit code; chưa chứng minh restart
  scope fake. G2c validation sau reset đã PASS_MANUAL, G2d đã PASS_MANUAL, G2e đang chờ.
- G2a (2026-10-10): PASS_MANUAL cho submit rỗng, thiếu tên hiển thị, tạo user
  hợp lệ và username trùng bị chặn/không thêm bản sao. Username người dùng báo:
  uat005_ui_1010, uat005_ui_1010_02. Nguồn người dùng xác nhận thủ công; không
  có exit code. Username sai định dạng và password tạm chỉ một response chưa
  được người dùng xác nhận riêng, không suy diễn PASS. G2b sửa tên/gán Viewer/
  scope/mở lại đã PASS_MANUAL; W13 default idle30ph đạt thật, ba mốc khác pending theo artifact hiện tại.
- G1a PASS_MANUAL: người dùng login admin mới và xác nhận cả /Admin/Users,
  /Admin/Authorization (5 role), /Admin/SecurityEvents đều đúng expected, không
  quay về login. G1b PASS_MANUAL: đủ ba guard khóa/hạ Viewer/bỏ một ALL; GET lại
  vẫn Admin/ba ALL/cấp con. Nguồn người dùng xác nhận thủ công; không có exit code.
- Lần thử G1a đầu hôm nay ERR_CONNECTION_REFUSED: AI kiểm BackendCount0 và
  ListenerCount0, start lại đúng một backend bằng dotnet run --no-build --launch-profile
  https; login GET200 và BackendCount1/2ports/owner1. Server vẫn chạy, không gán exit0.
- Chỉ thêm AuthSessionTimeoutTests.cs, không đổi product/policy: build test project
  --no-restore --no-dependencies exit0, 0warning/0error; filtered test --no-build
  --no-restore --filter FullyQualifiedName~AuthSessionTimeoutTests exit0,
  14passed/0failed/0skipped. TimeProvider giả lập kiểm boundary idle/absolute web
  30ph/8h, remember24h/7ngày, refresh14/30ngày; không là bằng chứng thời gian live.
  Baseline78/78 không chạy lại; không báo tổng92/92 đã chạy.
- Probe chỉ đọc trước W10 exit0: đúng DEV riêng/owner-only1permission/exactdrive.file,
  schema1/13tab/5role, Users4. Runner W10/W13 mới đã kiểm syntax exit0, đang chạy
  nền ẩn PID18964; command launch exit0 chỉ chứng minh tạo process, chưa exit runner.
- Còn mở: G2 browser/validation/Viewer/revoke/layout/flags và scope fake sau restart;
  W13 default idle30ph PASS_REAL_ELAPSED; ba mốc8h/24h/7ngày còn chờ; W10 đã PASS_HTTP_GOOGLE. Refresh14/30ngày
  đã kiểm boundary giả lập; checklist W13 yêu cầu elapsed web/remember, không suy
  diễn đã kiểm refresh dài trên Google. Credential UAT vừa được người dùng nhập
  qua prompt ẩn, đã xóa sau setup và xác minh absent độc lập; finally vẫn kiểm cleanup.
- TASK-005 IN_REVIEW; TASK-003 IN_REVIEW, TASK-004 DONE. Không lặp setup/bootstrap/
  suite173, không commit/push/deploy/TASK-006.

## Trạng thái đã đạt lịch sử

AI trực tiếp chạy Google provisioning/init exit 0, xác minh scope duy nhất
`drive.file`, DEV System riêng/owner-only ACL, đủ 13 tab/schema 1/5 role,
Bootstrap/no-op Google đã đạt: đúng một admin, Admin/ba ALL và must-change;
snapshot no-op không đổi, khóa bootstrap xóa. Local build sạch/test 61/61 và HTTP anonymous probe 12/12 pass.
HTTP probe không thay tương tác browser/layout. Người dùng đã xác nhận đổi mật
khẩu bắt buộc và login lại; web đã dừng để build bản sửa UI; runner chờ credential nhập lại sau cleanup. Người dùng vẫn trên máy đã setup, chưa chuyển máy.
Không chạy lại PoC TASK-004.


## Browser UAT — hướng dẫn/kết quả lịch sử trước quyết định hoãn (không tiếp tục giao thao tác)

Chỉ bắt đầu khi AI báo runner kết thúc/cleanup xong; dùng https://localhost:7008.
Không gửi mật khẩu tạm, password, cookie, token hoặc screenshot chứa chúng. Ghi
PASS hoặc bước lỗi/actual; không cần thao tác Google spreadsheet trực tiếp.

**G1 — kiểm lại lỗi tải trang, quyền admin và audit**
1. Login admin mới; mở Tài khoản/Phân quyền/Nhật ký. Expected: không quay lại login
   khi phiên hợp lệ; ghi thời gian tải ước lượng nếu còn chậm.
2. Tài khoản → Quản lý admin: assignment Admin, ba ALL và cấp con. Thử Khóa; thử
   gán Viewer; thử Admin nhưng đổi một ALL sang CAT-DEMO-EQUIPMENT. Mỗi submit phải
   báo bảo vệ admin cuối cùng; assignment đã lưu vẫn Admin/ALL. Refresh nạp lại
   giá trị đã lưu (form POST lỗi có thể giữ input đang thử).
3. /Admin/Authorization đủ năm role; /Admin/SecurityEvents có login/password-change
   và sự kiện fixture; không có password/token/hash trong danh sách.

**G2 — tài khoản giả, validation, Viewer và thu hồi cookie**
1. Tạo user hoàn toàn giả mới, ví dụ uat005_ui_1010 (nếu đã có chọn suffix khác).
   Trước đó submit rỗng/username sai hoặc trùng: phải có lỗi rõ, không tạo thêm user.
   Password tạm chỉ hiện một response; giữ nội bộ an toàn, không gửi cho AI.
2. Sửa tên hiển thị; gán Viewer + OU-DEMO-A/LOC-DEMO-A/CAT-DEMO-EQUIPMENT/cấp con,
   refresh phải còn đúng. Trong cửa sổ riêng login user: bắt đổi password. Thử sai
   current/7 ký tự/confirm khác: có validation. Đổi thành password tự chọn 8 ký tự
   đơn giản được chấp nhận, login lại. Các URL /Admin/Users, /Admin/Authorization,
   /Admin/SecurityEvents phải access denied, không lộ quản trị.
3. Admin đổi role/scope user. Cửa sổ user mở /Admin/Users phải về login vì cookie
   cũ bị thu hồi; login lại dùng quyền mới. Admin khóa user: cookie cũ về login,
   locked user không login được. Mở khóa không hồi sinh cookie cũ; login mới được.
4. Với user đã login mới, admin Reset: cookie cũ mất hiệu lực, password cũ không
   dùng được; password tạm bắt đổi lại; password mới login được. Kiểm audit.
5. Logout user, mở URL bảo vệ/refresh/back rồi tải lại: phải login. Kiểm form/menu/
   bảng ở desktop và cửa sổ hẹp khoảng390px; ghi kích thước thực tế, focus/tab,
   lỗi/trạng thái loading/access denied. Không lấy build/HTTP làm PASS_BROWSER.

**G2b — sửa tên và assignment (PASS_MANUAL 2026-10-10)**

Dùng admin trong browser thường; chọn uat005_ui_1010_02 tại /Admin/Users.
1. Quản lý tài khoản → đổi tên hiển thị thành UAT Viewer 1010-02, nhấn Lưu.
   Expected thông báo cập nhật thành công và tên mới.
2. Chọn Viewer, nhập OU-DEMO-A / LOC-DEMO-A / CAT-DEMO-EQUIPMENT theo đúng
   ba ô scope, tích Bao gồm cấp con, nhấn Thay assignment, chờ response xong.
   Expected assignment hiện tại Viewer/Active, ba ID trên và cấp con Có.
3. Quay về /Admin/Users rồi mở lại Quản lý cùng user bằng GET (không resubmit
   POST). Expected tên mới, form chọn Viewer, đúng ba scope/checkbox và phần
   assignment đã lưu vẫn đúng. Báo G2b PASS hoặc bước lỗi; không gửi secret.

Đăng nhập user giả, validation đổi password và Viewer access denied sẽ kiểm
ở G2c và nhóm kế tiếp; G2b đã được xác nhận. Không lấy HTTP làm browser pass.

**G2c — validation đổi mật khẩu (PASS_MANUAL 2026-10-10, sau reset)**

Người dùng đã xác nhận Reset/Login PASS cho uat005_ui_1010_02.
Giữ cửa sổ admin và cửa sổ riêng user đang ở /Account/ChangePassword.
Mở /Admin/Users trước khi đổi: expected vẫn về ChangePassword, không lộ quản trị
(đã PASS G2c). Dùng password tạm mới sau reset làm Current.

Thử riêng ba case, mỗi lần nhập lại đủ ba ô:
- Current sai; New và Confirm đúng cùng một password tự chọn đủ8: lỗi current.
- Current đúng; New/Confirm cùng7 ký tự: lỗi ít nhất8 ký tự.
- Current đúng; New đủ8, Confirm khác: lỗi xác nhận không khớp.

Expected không đổi password trong cả ba case. Sau đó Current đúng, New/Confirm
cùng password tự chọn8 chữ thường: thành công và về login. Thử password tạm cũ
một lần: lỗi generic; dùng password mới: login thành công, không bị bắt đổi lại.
Giữ cửa sổ user để nhóm Viewer/revoke sau. Báo từng case G2c hoặc lỗi, không
gửi secret. Người dùng đã xác nhận G2c PASS cả bước1/2/3; Viewer access denied
kiểm tiếp ở G2d.

**G2d — Viewer bị chặn quản trị (PASS_MANUAL 2026-10-10)**

Dùng cửa sổ riêng đã login bằng password mới của uat005_ui_1010_02 (Viewer).
Lần lượt nhập trực tiếp các URL sau, chờ response từng lần:
- https://localhost:7008/Admin/Users
- https://localhost:7008/Admin/Authorization
- https://localhost:7008/Admin/SecurityEvents

Expected mỗi URL chuyển /Account/AccessDenied, hiện Không có quyền truy cập,
không hiển thị danh sách user/catalog/audit. Phiên user còn hợp lệ, không tự
quay về login. Sau đó mở /Account/ChangePassword: expected form đổi password
truy cập được, không yêu cầu bắt buộc; không submit. Giữ cửa sổ user để kiểm
revoke ở nhóm sau. Báo từng URL PASS hoặc lỗi, không gửi secret.

**G2e — đổi role/scope thu hồi cookie, editor bị chặn (chưa chạy browser)**

Giữ hai cửa sổ: browser thường admin, cửa sổ riêng uat005_ui_1010_02.
1. User mở /Account/ChangePassword trước mutation: expected form còn truy cập
   được để xác minh cookie đang hợp lệ; giữ cửa sổ này.
2. Admin /Admin/Users → Quản lý đúng user → đổi role AssetEditor, scope
   OU-DEMO-A / LOC-DEMO-A / CAT-DEMO-VEHICLE, cấp con có; Thay assignment.
   Expected thành công. Về danh sách/mở lại GET: AssetEditor/Active và ba scope
   mới đúng, tên đã lưu không đổi.
3. Không logout user; ở cửa sổ user tải lại /Account/ChangePassword. Expected
   chuyển /Account/Login ngay vì cookie cũ đã revoke.
4. Login user bằng password mới đã chọn ở G2c. Expected thành công, không bắt
   đổi lại; own ChangePassword form vẫn mở được. Mở ba URL /Admin/Users,
   /Admin/Authorization, /Admin/SecurityEvents: tất cả AccessDenied vì
   AssetEditor không có quyền quản trị. Giữ cửa sổ user cho lock/reset sau.

Báo G2e từng bước PASS hoặc lỗi; không gửi password/cookie/token. API revoke
đã có suite173, nhóm này chỉ lấy bằng chứng browser, không tạo endpoint TASK-006.

**G3 — trường hợp có admin dự phòng và timeout dài**
W10 và W13 ở bảng checklist vẫn cần bằng chứng riêng nếu chưa quan sát. Trước W10
phải giữ được credential của cả hai tài khoản admin để phục hồi quyền; chỉ fake
data, không để hệ thống về0 admin. Không thay timeout để giả bằng chứng thời gian.
W11 protocol cookie/anti-forgery được runner HTTP kiểm riêng; browser form submit
và trạng thái/lỗi vẫn ghi nguồn manual.

## 1. Checklist bootstrap / bằng chứng lịch sử

Tại PowerShell ở `D:\Dev\QuanLyTaiSan_DuyTien`:

```powershell
# Không tạo spreadsheet nữa; dùng DEV System đang cấu hình.
.\scripts\Test-Task005GoogleDev.ps1

# Prompt ẩn, mật khẩu do người vận hành nhập, chỉ tối thiểu 8 ký tự.
.\scripts\Set-AuthOperatorSecret.ps1 -Purpose Bootstrap

dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- auth-bootstrap-admin
$LASTEXITCODE
```

Trước CLI phải dừng web và mọi auth CLI khác; cùng server project dùng cùng
process lease. File lock local không bảo vệ hai host; nếu chuyển máy sau này phải
xác nhận backend cũ đã dừng. Flow provisioning/init đã chạy, không chạy lại để
thay spreadsheet hoặc dữ liệu chưa xác định.

| ID | Thao tác | Expected | Actual / nguồn hiện tại |
|---|---|---|---|
| B01 | Bootstrap lần đầu | Tạo admin + Admin assignment + ALL ba chiều/MustChangePassword; báo commit, xóa khóa bootstrap local | PASS_GOOGLE — CLI exit 0; AI trực tiếp |
| B02 | Chạy script nhập bootstrap secret lại rồi CLI bootstrap lần hai, web vẫn dừng | Báo no-op, không reset password/role/scope/SecurityVersion; xóa khóa local | PASS_GOOGLE — CLI exit 0/no-op; snapshot không đổi; AI trực tiếp |
| B03 | Chạy Google probe sau bootstrap | AdminExists=True, không có admin trùng; schema/catalog/ACL giữ đúng | PASS_GOOGLE — read probe sau bootstrap exit 0; Users 1/AdminExists True; ACL/schema/catalog đúng |
| B04 | Start web theo lệnh dưới; mở login, đăng nhập admin bằng mật khẩu đã nhập | Chuyển bắt buộc ChangePassword; không vào Admin/Users trước khi đổi; remember-me không persistent trong phiên hạn chế | PASS_MANUAL valid forced-change/login flow; hạn chế trước đổi chưa quan sát riêng |
| B05 | Trên form đổi password thử sai current, 7 ký tự, confirm khác; sau đó mật khẩu tự chọn đơn giản 8 ký tự | Validation rõ; chỉ min 8; không complexity/max/blocklist; đổi hợp lệ thu hồi phiên và yêu cầu login lại | PASS_MANUAL đổi hợp lệ/login lại; G2c user giả sau reset current/7/confirm và8 chữ thường/relogin PASS_MANUAL; không suy diễn admin đã thử các invalid case |
| B06 | Login lại; dừng web/restart đúng một instance; login bằng password mới và kiểm quyền/account/audit | Password mới dùng được, password bootstrap không dùng được; account/role/ALL và audit còn đúng | PASS_GOOGLE restart identity/role/cookie/password hiện tại; bootstrap-password login chưa thử riêng; browser audit/restart còn thiếu |

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https
```

URL dev: `https://localhost:7008/Account/Login`. Server đã báo dev certificate
chưa trusted; người dùng tự kiểm tra/trust chứng thư dev hoặc xử lý cảnh báo
browser nếu cần. AI không vượt cảnh báo bảo mật. Chỉ ghi kết quả, không ghi
password hay giá trị cookie. Mọi thay đổi mật khẩu trong browser do người dùng
nhập, xác nhận và submit.

## 2. Checklist Razor / bằng chứng lịch sử — current gate hoãn ở đầu tài liệu

Dùng profile/browser thường cho admin và cửa sổ riêng cho user giả. Chuẩn bị
`viewer_demo`, `asset_editor_demo`, `multi_role_demo`, `locked_demo`,
`admin_second_demo`; chỉ tạo nếu chưa có, không reset dữ liệu chưa xác định.
Admin cuối cùng được thử trước khi tạo admin thứ hai. Username dành riêng admin
chỉ do bootstrap tạo. Password tạm hiển thị một lần: giữ qua kênh nội bộ an toàn,
không sao chép vào report/chat hoặc chụp ảnh. Response này phải `no-store`.

| ID | Thao tác | Expected | Actual / nguồn hiện tại |
|---|---|---|---|
| W01 | Login/logout, invalid/unknown username, submit rỗng, refresh/back sau logout | Lỗi login generic; validation rõ; logout POST thu hồi phiên; trang admin cũ yêu cầu login | PASS_MANUAL G2c login password mới/old temporary rejected generic; logout/unknown/empty/back-refresh browser còn chờ; HTTP có evidence riêng |
| W02 | Mở `/Admin/Users`, tạo fake user, thử username sai/trùng/thiếu tên | List/create/detail đúng; validation hoặc conflict không tạo thêm record; password tạm chỉ một response/no-store | PASS_MANUAL G2a: rỗng/thiếu tên/tạo hợp lệ/trùng không bản sao; source người dùng 2026-10-10; sai định dạng/one-response chưa xác nhận riêng |
| W03 | Mở detail, sửa display name; gán Viewer/scopes A rồi role editor; mở Authorization/SecurityEvents | Page gọi policy chung; role catalog đúng 5; user/name/assignment đúng; audit có action/actor/target/outcome tương ứng | PASS_MANUAL G1a catalog/audit và G2b tên/Viewer/scope/GET reopen; editor và audit riêng actor/target của mutation chờ |
| W04 | Gán scope `OU-DEMO-A`, `LOC-DEMO-A`, `CAT-DEMO-EQUIPMENT`, includeDescendants; mở lại sau restart | Scope/assignment và cờ còn đúng trong Google; không dùng tên/prefix làm ID | PASS_HTTP_GOOGLE scope/cấpcon Admin/Viewer và2assignment; G2b browser gán Viewer/cấp con/GET reopen PASS_MANUAL; restart scope fake còn NOT_RUN |
| W05 | Viewer login/đổi password lần đầu; truy cập URL admin trực tiếp, rồi editor tương tự | Bị chặn server, access denied; không lộ user/audit; API cùng user phải 403 | PASS_HTTP_GOOGLE Viewer redirect AccessDenied/API403, editorAPI403; G2c forced-change/validation PASS_MANUAL sau reset; G2d Viewer ba URL admin AccessDenied và own password form PASS_MANUAL; editor browser còn chờ G2e |
| W06 | User đang login ở cửa sổ riêng; admin đổi role/scope user | Phiên cookie/access/refresh cũ mất hiệu lực ngay; user phải login lại; quyền mới đúng | PASS_HTTP_GOOGLE cookie/access/refresh revoke; browser NOT_RUN |
| W07 | Admin khóa fake user đang login, rồi mở khóa | Cookie/access/refresh cũ invalid; locked user không login; mở khóa không hồi sinh phiên cũ | PASS_HTTP_GOOGLE lock/revoke/unlock; browser NOT_RUN |
| W08 | Reset fake user đang login | Old cookie/access/refresh invalid; password tạm/no-store; first login bắt buộc đổi; audit reset/change | PASS_HTTP_GOOGLE reset/no-store/mustchange/revoke; PASS_MANUAL reset/loginmustchange sau mất password; revoke old cookie/old password/audit riêng browser còn chờ |
| W09 | Khi chỉ còn bootstrap admin đủ điều kiện, thử khóa, thay Admin thành Viewer, bỏ một ALL | Tất cả bị từ chối; vẫn login/admin được, ba ALL không đổi; Razor hiển thị lỗi, API nếu thử trả 409 | PASS_GOOGLE API409 ba mutation/snapshot không đổi; PASS_MANUAL G1b đủ bước 2026-10-10; người dùng xác nhận |
| W10 | Tạo admin_second_demo + Admin/ALL ba chiều, đang hiệu lực; lặp thao tác hợp lệ trên admin thứ nhất rồi phục hồi bằng admin thứ hai | Cho phép khi vẫn còn một admin đủ điều kiện; không để hệ thống về 0 admin | PASS_HTTP_GOOGLE — backup login/API/Razor GET/Google assignment; primary lock/downgrade/dropALL204 rồi phục hồi; AI trực tiếp 2026-10-10 |
| W11 | DevTools chỉ quan sát flags/expiry cookie, không giá trị: session mặc định, remember-me; gửi state-changing form thiếu/sai anti-forgery | dt_session HttpOnly/Secure/SameSite=Lax; mặc định không Expires, remember có Expires và timeout đúng; thiếu/sai token 400 không mutation | PASS_HTTP_GOOGLE flags/default/remember7day issuance,missing/invalidCSRF400; browser NOT_RUN |
| W12 | Kiểm loading/chờ Google, validation/empty/access denied; desktop và viewport hẹp; thử tab/label/focus | Thông báo dễ hiểu, form/bảng đọc và dùng được; không lộ secret/stack trace; ghi kích thước browser/actual | NOT_RUN_BROWSER |
| W13 | Kiểm idle/absolute/remember expiry theo timeout đã duyệt, không thay timeout production để giả bằng chứng | Default 30 phút/8 giờ; remember 24 giờ/7 ngày; expiry chặn server | PASS_PARTIAL_REAL_ELAPSED: default idle30ph HTTP302 login lúc14:44:57; 8h/24h/7ngày pending. 14 test boundary giả lập PASS_LOCAL exit0, không thay elapsed; xem deadline bên dưới |

Form hiện tại thay toàn bộ assignment bằng một assignment; API hỗ trợ nhiều
assignment. Để kiểm nhiều role dùng runner/API, không cố gán nhiều lần trên form
rồi suy diễn thành hai assignment. Authorization core exact/descendant/sibling,
OR trong chiều/AND giữa chiều, assignment không cross-combine và move nguồn/đích
đã test local; không tạo endpoint nghiệp vụ TASK-006 trong lượt này.

## 3. Mobile token API — không MAUI UI

Sau B01–B06, chạy Google read probe, start đúng một backend, tạm ngừng UAT khác để
tránh dồn quota rồi chạy trong terminal tương tác:

```powershell
python -B .\scripts\Test-Task005MobileLive.py
$LASTEXITCODE
```

Runner hỏi current admin password qua `getpass` ẩn; không nhận password từ args,
stdin pipe hoặc chat. Runner giữ credential/token trong RAM, tự tạo một fake user
`uat005_*`, password fake được sinh trong runtime (chỉ 8 chữ thường) và giữ lại
account/audit, không xóa. Chạy khoảng 15–25 phút do giãn request; có refresh admin
khi gần hết hạn, không sửa timeout auth. Output chỉ case/expected/actual/pass và
username giả. Runner lần đầu exit 1 ở checkpoint công cụ sau 9 assertion; khóa tạm đã xóa, chờ nhập lại; nếu fail ghi output sạch,
không dump body/token hoặc chạy lại ghi đè tài khoản.

| ID | Case runner hoặc bổ sung | Expected | Actual / nguồn hiện tại |
|---|---|---|---|
| M01 | Mobile admin login, create fake user/no-store, Viewer assignment/scopes | 200/201/204; create/reset response no-store; không trả hash | PASS_GOOGLE — create201/no-store/assign204; AI trực tiếp |
| M02 | Fake user first login, 7/8 ký tự/change/revoke | Must-change; hạn chế trước đổi; 7 rejected/8 đơn giản accepted; old access/refresh 401 | PASS_GOOGLE — mustchange/403/7→400/8lowercase→204/revoke401; AI trực tiếp |
| M03 | Viewer/editor gọi API admin JSON và HTML | 403/no redirect; anonymous 401; không bypass bằng cookie | PASS_GOOGLE — Viewer/editor403/no redirect JSON+HTML, cookie403; AI trực tiếp |
| M04 | Access expiry response, rotate refresh; old access và old refresh reuse | Expiry được phát hành 15 phút; rotate đổi cả token; old access 401; reuse revoke family/new access/new refresh 401 | PASS_GOOGLE — rotation/reuse/family401; actual access15 phút/refresh sau expiry đạt; AI trực tiếp |
| M05 | Đổi role/scope, khóa/mở/reset user | Old access/refresh 401; locked login 401; reset/new login must-change; audit đúng | PASS_GOOGLE — role/lock/unlock/reset/revoke, audit; AI trực tiếp |
| M06 | Logout/logout-all và nhiều session | Logout current invalid; logout-all thu hồi cả hai session; không hồi sinh refresh | PASS_GOOGLE — logout/logout-all/max5; AI trực tiếp |
| M07 | Audit và hai role assignment scope A/B | Có USER_CREATE/AUTHORIZATION_REPLACE/REFRESH_REUSE/LOCK/UNLOCK/RESET/LOGOUT actions; /api/me có hai role | PASS_GOOGLE — đủ audit action,2role/group scopes; core data-scope vẫn local; AI trực tiếp |
| M08 | Bổ sung thực nghiệm access sau 15 phút, max 5 phiên; lockout 5 lần/15 phút và rate limit10/15 phút | Access hết hạn 401; login thứ6 thu hồi oldest; lần5 khóa15 phút; unlock/expiry đúng; vượt limit429 | PASS_GOOGLE — access15 phút, max5, lock15 phút/rate10 window và expiry thật; AI trực tiếp |

## 4. Lưu bằng chứng và quyết định

Với mỗi case ghi ngày, case ID, expected, actual, PASS/FAIL/NOT_RUN, nguồn
**AI chạy/quan sát trực tiếp**, **người dùng chạy**, **người dùng xác nhận thủ
công**, hoặc **chưa chạy/bị chặn**. Chỉ ghi exit code khi thật sự quan sát.
Lệnh server đang chạy không có exit 0; chủ động ngắt phải ghi riêng. Không ghi
password/token/hash/resource ID/email thật. Nếu không kiểm tra một trạng thái
empty/loading/layout, ghi NOT_RUN thay vì suy diễn từ build/source review.

Đồng bộ TASK-005/Test Report/INDEX/PROJECT-CONTEXT sau từng gate. Chỉ DONE khi
các AC/gate bắt buộc đầy đủ, không lỗi trong phạm vi; phân biệt core local với
Google/UI live. TASK-003 giữ IN_REVIEW, TASK-004 DONE. Chưa mở TASK-006; bước tiếp
theo theo PLAN-001 là TASK-006 sau khi TASK-005 DONE.

## Tiếp tục AI UAT sau đổi mật khẩu

Người dùng đã lưu mật khẩu admin hiện tại bằng prompt ẩn:

```powershell
.\scripts\Set-Task005UatSecret.ps1
python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret --restart-checkpoint
```

Khóa `UatTask005:AdminPassword` ngoài repo chỉ dùng runner và bị xóa ở `finally`.
Không gửi giá trị vào chat/args/stdin/log. Runner kiểm restart mobile/cookie,
last-admin (guard đúng một bootstrap user), cookie/remember/anti-forgery,
Viewer/revoke và max5; chỉ là HTTP, layout/browser vẫn manual. Không chạy UAT
browser song song để tránh quota. Bằng chứng bootstrap driver phiên bản đầu
exit 1 sau assertion bootstrap được giữ riêng trong report; hai CLI đều exit 0.

Bản sửa scope Detail đã local build/test 61/61, chưa live UI pass. AI xác minh backend/port 0 và credential tạm không còn. Chạy prompt nhập lại rồi AI start một backend và runner; browser UAT vẫn cần xác nhận theo từng case, không gửi screenshot chứa mật khẩu tạm.


Checkpoint restart mới: mobile/cookie/identity/login sau restart và last-admin
API409 ba thao tác đã đạt. Runner20pass/1fail(exit1) do sai URL Razor, đã sửa
/Detail và probe anonymous route; chưa full suite PASS. Credential/marker đã
xóa/xác minh; web một instance đang chạy. Nhập lại prompt UAT rồi rerun; không
lặp provisioning/init/bootstrap. Viewer/revoke/token/audit và browser còn thiếu.
Marker precondition phải đếm unique ports2/owner1, localhost có4 rows IPv4+IPv6.


## Checkpoint sửa lỗi quota — 2026-10-09

Runner sau sửa URL exit1: 9pass/1fail GETusers500; mẫu log có45 lần xuất hiện mã429. Người dùng
báo G1 login được nhưng mở Tài khoản/Phân quyền lại yêu cầu login, tải chậm; G1 chưa
đạt. Sửa local: một batch đọc sáu bảng/fail-closed; no-op không ghi; lỗi Google trả
503 giữ phiên; validation field tạo user. Build cuối exit0/0warning/0error, tests
78/78 exit0. Credential runner đã xóa/xác minh, backend đã dừng. Chờ nhập lại prompt
UAT để live retest rồi browser; giữ TASK-005 IN_REVIEW. Không rerun setup/bootstrap.
Nguồn/expected/actual/failure lịch sử xem Test Report và artifact sạch.


## Kết quả live sau sửa quota — 2026-10-09

Build sạch/78 tests local exit0; HTTP preflight12/12 exit0; runner
`python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret` exit0,
173/173 assertion. Viewer/revoke/last-admin/mobile rotation-reuse/logout/audit/
max5/access15 phút thật/lockout15 phút thật/rate-limit đạt. Cookie/anti-forgery và
Razor scopes đã PASS_HTTP_GOOGLE, không là PASS_BROWSER. Cleanup khóa UAT/
bootstrap đều booleanFalse; read probe Google exit0 vẫn đúng DEV/owner-only/
drive.file/schema1/13tab/5role, Users4/AdminExistsTrue. Một backend đang chạy
HTTPS7008 để browser. G1 trước fix FAIL_MANUAL, retest đang chờ; G2/tương tác/
layout, W10 và W13 elapsed dài chưa đầy đủ. TASK-005 IN_REVIEW; không lặp setup.
TASK-003 IN_REVIEW, TASK-004 DONE, Task sau chỉ bắt đầu sau gate TASK-005.

### W10 hoàn tất / W13 đang đo thật — 2026-10-10

Nguồn AI chạy/quan sát trực tiếp HTTP/Google, không là browser UAT. Runner riêng
`python -u -B .\scripts\Test-Task005RemainingLive.py --start-web-timeouts`
được launch bằng Start-Process -WindowStyle Hidden -PassThru, stdout/stderr đến
file tạm ngoài repo; launch exit0, PID18964. Runner vẫn sống và chưa có exit code.
65 assertion đã quan sát PASS, 0 FAIL; không gộp với suite173 đã đạt trước đó.

- admin_second_demo mới được tạo vì absent; Active, assignment Admin đang hiệu
  lực trong Google, cùng assignment đủ ba ALL/cấp con. Đổi password bắt buộc,
  login lại, /api/me Admin/không must-change, Razor protected GET200 đều đạt.
- Khi còn backup đủ điều kiện, lock/downgrade/drop-one-ALL của admin chính đều
 204; admin chính được phục hồi Active/assignment gốc hiệu lực đủ ba ALL,
  login lại200/Admin. Old primary access sau lock401. Không có bước về0 admin.
- Logout-all backup204 (cả phiên Razor); logout phiên runner admin chính204.
  Password backup không lưu; muốn dùng trong browser, admin chính reset qua UI
  rồi người vận hành giữ password tạm/đổi password, không gửi chat.
- Credential UAT xóa ngay sau setup; runner tự đọc lại xác minh absent. Probe
  độc lập exit0: TemporaryUatSecretPresent=False, BootstrapSecretPresent=False,
  RunnerAlive=True. Cookie fixture chỉ RAM, không nằm trong artifact/log.
- Bốn phiên độc lập đã GET protected200 ban đầu, đối chiếu timestamps session
  Google riêng trong RAM: default1800/28800 giây, remember86400/604800 giây.
  Đây là bằng chứng issuance, chưa là PASS_ELAPSED. Không đổi policy/đồng hồ.

| W13 thực | Deadline kiểm (Asia/Saigon) | Hoạt động giữ idle | Actual |
|---|---|---|---|
| Default idle30ph | 10/10/2026 14:44:54 | Không request tới cookie này | PASS_REAL_ELAPSED 14:44:57 — HTTP302 login, AI |
| Default absolute8h | 10/10/2026 22:15:24 | GET protected mỗi20ph | PENDING_ELAPSED |
| Remember idle24h | 11/10/2026 14:15:54 | Không request tới cookie này | PENDING_ELAPSED |
| Remember absolute7ngày | 17/10/2026 14:16:24 | GET protected mỗi12h | PENDING_ELAPSED |

Expected deadline: gửi cookie gốc từ RAM tới /Account/ChangePassword phải302
/Account/Login; các request giữ idle trước absolute phải200. Mặc dù browser có
thể tự bỏ cookie expired, runner gửi cookie gốc để chứng minh gate phía server.
Cần giữ máy/server và runner hoạt động tới deadline; process/server gián đoạn
không là PASS. Xem artifact sạch [remaining evidence](TASK-005-REMAINING-LIVE-EVIDENCE.json),
UTC timestamp chính xác trong đó. Không rerun script khi artifact/process còn có;
nếu gián đoạn phải giữ lịch sử, kiểm checkpoint trước khi tạo fixture lại.

G2a đã được người dùng xác nhận bốn bước PASS thủ công; các mục chưa xác nhận
được giữ riêng. G2b/G2c PASS_MANUAL, G2d đã PASS_MANUAL, G2e đang chờ. Các gate browser
còn mở: W01/W02 validation/create/login/logout, phần fake assignment W03/W04
và scope sau restart, W05 Viewer/editor, W06–W08 revoke/lock/reset, W11 flags,
W12 layout/focus/loading; invalid password B05 sẽ kiểm bằng user giả trong G2.
B06 old bootstrap password negative chưa quan sát riêng; không lặp bootstrap,
không suy diễn PASS. G1a/G1b đạt, W10 đạt HTTP/Google; W13 idle30ph đạt thật, ba mốc còn pending.

Verify format riêng AuthSessionTimeoutTests exit0; syntax runner exit0.
Git diff --check ban đầu exit1 do một dòng trống EOF context, đã sửa và rerun
exit0. Không đổi code product hoặc chính sách phiên; không rerun suite173/78.
TASK-005 IN_REVIEW; TASK-003 IN_REVIEW, TASK-004 DONE; TASK-006 chưa triển khai.
