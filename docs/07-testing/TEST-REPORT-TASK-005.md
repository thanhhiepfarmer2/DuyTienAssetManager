# TEST-REPORT — TASK-005

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

## Kết quả lịch sử 2026-10-09 — live HTTP/Google

2026-10-09; TASK-005 **IN_REVIEW**. Nguồn AI chạy/quan sát trực tiếp cho các lệnh
và HTTP/Google; user confirmation chỉ áp dụng các bước được ghi rõ.

| Gate / lệnh thực tế | Expected | Actual / nguồn |
|---|---|---|
| Build server theo COMMANDS | Sạch | exit0; 0warning/0error; AI |
| Test project theo COMMANDS | Không failed/skipped | exit0; 78passed/0failed/0skipped; AI local fake/stub |
| python -B .\scripts\Test-Task005WebPreflight.py | Anonymous/validation/API boundary | exit0;12/12; AI HTTP |
| python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret | Các ca Google/API/Razor protocol | exit0;173/173 assertion; AI HTTP/Google |
| .\scripts\Test-Task005GoogleDev.ps1 sau run | Đúng DEV/owner-only ACL/drive.file/schema | exit0; exactdrive.file/owner-only1permission;13tabs/schema1/5roles;Users4/AdminExistsTrue; AI read-only |
| Cleanup runner + kiểm riêng | Xóa khóa UAT, không in secret | finally báo xóa; TemporaryUatSecretPresent=False, BootstrapSecretPresent=False; AI boolean |
| Backend cho browser | Một instance/đúng lease | BackendCount1/2ports; HTTPS7008/HTTP5211 listening; chưa có exit code cho server đang chạy |
| Browser G1 sau fix | Admin pages không bounce login; last-admin/error/catalog/audit đúng | Đang chờ người dùng thao tác; G1 trước fix FAIL_MANUAL giữ riêng |

Các assertion live đã quan sát đầy đủ tại [artifact](TASK-005-LIVE-EVIDENCE.json):
- Admin/Viewer/scopes và multi-assignment hiển thị đúng trong HTML; Viewer/editor
  API403 không redirect, Viewer Razor access-denied; core descendant/move vẫn
  local vì endpoint nghiệp vụ thuộc Task sau.
- Last-admin ba mutation409, user không đổi; cookie flags/default/remember7-day
  issuance; anti-forgery thiếu/sai400; cookie bị chặn API bearer403.
- Fake user first-login must-change;7 ký tự400,8 chữ thường204; password/role/lock/
  reset revoke cookie/access/refresh. Locked login401; reset bắt đổi password.
- Rotation đổi cả token; old access401; old refresh reuse401 và revoke family;
  logout/logout-all thu hồi; phiên thứ6 thu hồi oldest, newest còn dùng được.
- Access hết hạn sau15 phút thật401; refresh sau expiry200/newaccess200/logout204.
  Lần sai thứ5 khóa15 phút, correctpassword trong lock401, attempt11 rate429;
  sau deadline thật login200.429 này là rate-limit của ứng dụng, không là lỗi Google.
- Audit200 và đủ USER_CREATE/AUTHORIZATION_REPLACE/PASSWORD_CHANGE/REFRESH_REUSE/
  USER_LOCK/USER_UNLOCK/PASSWORD_RESET/LOGOUT/LOGOUT_ALL; admin web/mobile logout đạt.

Không chạy provisioning/init/bootstrap/PoC lại. Ba fake account mới được giữ trong
DEV để audit; password/token chỉ RAM, không xuất artifact. Browser/layout/validation
qua tương tác thực, W10 admin dự phòng và W13 timeout web/remember/refresh dài
chưa đủ evidence. Giữ IN_REVIEW; không suy diễn HTTP/build thành browser PASS.
Bootstrap/no-op/restart đạt từ checkpoint trước, giữ bằng chứng lịch sử.


## Checkpoint trước live retest — sửa lỗi quota Google (2026-10-09)

TASK-005 IN_REVIEW. Nguồn: AI chạy/quan sát trực tiếp; G1 do người dùng xác nhận
thủ công. Các checkpoint ở dưới là lịch sử, không thay kết quả hiện tại.

Runner `python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret`
sau sửa URL: exit 1; 9 assertion PASS, 1 FAIL ở GET /api/admin/users (500 thay
vì 200). Log backend được đọc riêng, chỉ lưu loại lỗi: ExternalStorageTransientException
và 45 lần xuất hiện mã Google429 trong mẫu log; không lưu raw log/header/body. finally
xóa credential, AI kiểm UatCredentialPresent=False, BackendCount=0 và ListenerCount=0
sau Ctrl+C (server exit 1). Không suy diễn toàn bộ suite PASS.

Người dùng báo G1: login admin được, nhưng mở Tài khoản hoặc Phân quyền bị đưa
về login, tải chậm. G1 FAIL_MANUAL/chưa hoàn tất các bước; chưa coi last-admin
HTTP hoặc build là browser pass.

Sửa trong TASK-005:
- Sáu bảng auth đọc qua một values:batchGet, kiểm đích/count/range/header đầy đủ;
  thiếu hoặc sai dữ liệu fail-closed, không ghi đè store. [Google API contract](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets.values/batchGet).
- Mutation không đổi dữ liệu không fetch metadata hoặc persist. Không cache quyền;
  SecurityVersion/revoke/last-seen thực sự đổi vẫn persist một batch nguyên tử.
- Lỗi external storage khi kiểm phiên hoặc xử lý request trả 503/no-store/thông báo
  chung, giữ cookie; không chuyển login hoặc lộ exception/credential. Sau phục hồi
  cùng phiên vẫn dùng được. Invalid/revoked/Viewer vẫn giữ 401/403 tương ứng.
- Bổ sung lỗi bắt buộc theo từng field khi tạo user; trước đây ModelOnly không
  hiển thị lỗi Required. Browser validation/layout vẫn phải nghiệm thu.
- Runner giãn request 5 giây sau đọc batch, giữ timeout đã duyệt và deadline thật.

| Lệnh thực tế / kiểm tra | Actual | Nguồn / giới hạn |
|---|---|---|
| Build server --no-restore --nologo -v:minimal, lần đầu | exit 0; 1 warning CS8602; 0 error | AI; đã sửa null guard |
| Test project --no-restore --nologo -v:minimal, lần đầu | exit 1; thiếu namespace Results | AI; lỗi test mới, đã sửa |
| Test cùng lệnh sau sửa | exit 0; 78 passed, 0 failed, 0 skipped | AI; fake/stub, không thay Google/browser |
| Format server rồi test project, --no-restore --include các file vừa sửa | exit 0 | AI; tuần tự |
| Build server cuối cùng | exit 0; 0 warning, 0 error | AI; tuần tự sau format |
| Kiểm File.Replace toàn scripts | Hai chỗ UAT/bootstrap helper đều NullString.Value | AI review; không rerun bootstrap |

Lệnh build/test đầy đủ nằm trong COMMANDS, không đổi project/flags. Test mới gồm:
batch đọc đủ sáu bảng; no-op không ghi; mutation từng bảng vẫn ghi; batch thiếu/sai
fail-closed; HTTP bearer/cookie JSON/HTML 503 và hồi phục cùng token, không Set-Cookie,
không Location, không lộ canary/credential. Chưa live retest bản sửa; server đã dừng,
chờ người dùng nhập lại credential UAT qua prompt ẩn. Bootstrap/init/provisioning
đã đạt và không lặp lại. Restart/last-admin trước đó giữ bằng chứng đã quan sát;
Viewer/revoke/rotation/reuse/elapsed/audit và browser vẫn còn thiếu.

## Bằng chứng local lịch sử — 2026-10-07

- Ngày/môi trường: 2026-10-07; Windows; .NET SDK 10.0.400; target `net10.0`.
- Phạm vi: implementation local đăng nhập, session, admin, RBAC/scope, Razor
  Pages, Google system adapter và lệnh vận hành theo AC-03/AC-04 của REQ-001.
- Dữ liệu test: fixture hoàn toàn giả; fake auth store chỉ nằm trong project test.
- Google: không tạo, chọn hoặc ghi system spreadsheet; không chạy lại PoC cũ.

| AC | Loại test | Case / lệnh | Expected | Actual | Kết quả |
|---|---|---|---|---|---|
| AC-03a | Unit | Bootstrap hai lần | Tạo admin một lần; lần hai no-op, không reset | Admin/assignment/ba ALL giữ nguyên; active-admin = 1 | PASS_LOCAL |
| AC-03b | Unit/source review | Password 7/8 ký tự, chuỗi đơn giản/username/space; framework hasher; scan logging | Chỉ min 8; hash framework; không log/response plaintext/hash | Policy đúng; hasher verify pass; API user không trả hash; log chỉ nói loại lỗi/trạng thái | PASS_LOCAL |
| AC-03c | Unit | Sai password 5 lần; 6 phiên; đổi password; mobile rotate/reuse | Lock 15 phút; giữ 5 phiên; revoke; marker rotate tồn tại và reuse revoke family | Đúng expected | PASS_LOCAL |
| AC-03d | Unit | Future/revoked/expired Admin assignment; thiếu một ALL; khóa admin cuối | Chỉ assignment hiệu lực đủ ba ALL được tính; không về 0 | Đúng expected | PASS_LOCAL |
| Writer | Unit + subprocess | Hai lease manager và auth CLI process riêng dùng cùng lock file | Writer/process thứ hai fail; sau release lấy được | Đúng expected; không dùng lock RAM làm bằng chứng liên tiến trình | PASS_LOCAL |
| AC-04a | Unit + HTTP integration | Catalog permission; Viewer gọi API admin | Role đúng catalog; Viewer 403 | Kestrel integration trả 403 | PASS_LOCAL |
| AC-04b | Unit | `Location.ParentId`, missing parent/cycle, exact/descendant/sibling, move nguồn+đích | Validate cây; fail-closed; cùng assignment phải match cả nguồn/đích | Đúng expected | PASS_LOCAL |
| AC-04c | Unit | AssetEditor scope A + DocumentManager scope B | Union permission, không cross-combine scope | Đúng expected | PASS_LOCAL |
| API | HTTP integration | Login mobile; admin/list user bằng bearer; cookie gọi API admin | Token opaque; admin 200; cookie bị chặn; không lộ PasswordHash | Đúng expected | PASS_LOCAL |
| Google adapter | HTTP stub | Init schema/catalog và persist mutation | Dùng `SystemSpreadsheetId`; có `RefreshTokens`; mutation một batch; không plaintext | Đúng expected | PASS_LOCAL_STUB |
| Razor | Build/review | Login/change-password/logout/users/authorization/events | Authorize server-side; POST anti-forgery; validation/empty/access denied | Compile; PageModel dùng cùng application service; password tạm `no-store` | PASS_LOCAL_BUILD |
| PowerShell | Parser | `Set-AuthOperatorSecret.ps1` | Prompt ẩn; syntax hợp lệ | Parser 0 lỗi | PASS_LOCAL |
| Live gate | Google + browser/manual | Provisioning/Picker, ACL, init/bootstrap/restart/UAT | Evidence trên system spreadsheet dev thật | Chưa được phép thực hiện trong lượt này | NOT_RUN |

Các endpoint CRUD/import/report/file của TASK-006 trở đi không được tạo. Scope
authorization core đã có unit test để các Task sau gọi; chưa giả vờ có integration
test trên endpoint nghiệp vụ chưa tồn tại.

## Lệnh và kết quả cuối

```text
dotnet format .\QuanLyTaiSan.DuyTien.sln --no-restore --verbosity minimal
exit 0

dotnet format .\QuanLyTaiSan.DuyTien.sln --no-restore --verify-no-changes --verbosity minimal
exit 0

dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
exit 0; warnings 0; errors 0

dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal
exit 0; total 57; passed 57; failed 0; skipped 0
```

Một lần build trước bằng chứng cuối được chạy song song với test và có warning
retry copy `apphost.exe` do hai tiến trình cùng dùng file. Build tuần tự cuối đã
pass sạch 0 warning/0 error; đây là bằng chứng authoritative.

PowerShell parser cho script auth: exit 0, `PASS: PowerShell syntax`.
`git status`: exit 1 với `not a git repository`; không init/commit/push.

## Review quyền, scope, session và logging

- API admin yêu cầu session hợp lệ + role Admin + bearer; cookie bị cấm trên API
  để Razor form/anti-forgery là kênh web thay đổi trạng thái.
- Role/scope được tính từ assignment hiệu lực trên mỗi request. Scope ba chiều
  match trong cùng assignment; dữ liệu thiếu scope fail-closed; move yêu cầu cùng
  assignment match cả nguồn và đích.
- Lock/reset/password/role-scope tăng `SecurityVersion` và revoke session. Web
  idle/absolute/remember, mobile access/refresh, max 5 và reuse family đã test.
- Refresh marker đã rotate không bị xóa/ghi đè thành active; reuse để lại dấu
  `RotatedAt`, revoke token đang active và session family.
- Runtime không có fake auth adapter. Password/token nguyên văn không được ghi
  vào store/log; Google store lưu hash. Mật khẩu tạm chỉ trả một lần với
  `Cache-Control: no-store`.
- Web và auth CLI dùng file lock liên tiến trình; semaphore hiện có chỉ tuần tự
  hóa mutation bên trong một process.

## Chưa kiểm tra tại checkpoint lịch sử 2026-10-07

- `GoogleWorkspace:SystemSpreadsheetId` chưa có; chưa provisioning/Picker, kiểm
  owner/ACL, init schema, bootstrap/recovery/restart hoặc UAT trên Google thật.
- Chưa tương tác Razor Pages trong browser với live system store. Compile và core
  HTTP integration pass, nhưng không thay cho live UI/UAT.
- Không chạy deploy, Git init/commit/push hoặc PoC TASK-004.

## Quyết định tại checkpoint lịch sử 2026-10-07

Local gate đạt; TASK-005 chuyển `IN_REVIEW`. Chưa đủ điều kiện `DONE` vì live
Google và UAT bắt buộc đang `NOT_RUN`. TASK-003 giữ `IN_REVIEW`, TASK-004 giữ
`DONE`.


## Checkpoint đầu lượt live/UAT — 2026-10-09 (trước bootstrap)

Ngày theo phiên người dùng Asia/Saigon. Môi trường vẫn là máy Windows đã setup
trước đây; người dùng xác nhận chưa chuyển máy. .NET SDK 10.0.400/runtime
10.0.11. Git worktree main, remote tên origin, HEAD b324c74 (2026-10-07);
git status ban đầu exit 0/sạch. Không đọc/in URL remote, không fetch/commit/push.
Bằng chứng chưa có Git ở checkpoint 2026-10-07 phía trên là lịch sử, không phải
trạng thái hiện tại.

Người dùng cho phép dùng OAuth dev và dữ liệu giả để provisioning/init/bootstrap;
không chạy lại PoC TASK-004. Người dùng trả lời **chưa nhập secret bootstrap**.
Không có bằng chứng người dùng chạy hay xác nhận thủ công UI/UAT trong lượt này.
AI không đọc dữ liệu doanh nghiệp; Google target là system spreadsheet DEV mới.

### Bằng chứng và gate

| ID / AC | Expected | Actual | Kết quả | Nguồn bằng chứng |
|---|---|---|---|---|
| ENV-01 | Đúng repo, giữ thay đổi có sẵn | Git status sạch ban đầu, worktree đúng; main/origin/HEAD b324c74 | PASS | AI chạy/quan sát trực tiếp, git exit 0 |
| ENV-02 | Không writer khác trước CLI | Không process server DuyTien/listener 7008/5211/53682; dotnet được phân loại build infrastructure; người dùng vẫn dùng máy đã setup | PASS cho điều kiện local đã quan sát | AI quan sát tiến trình/port; người dùng làm rõ máy hiện tại; không chứng minh lock phân tán |
| LIVE-01 | Không in secret, chỉ drive.file | Tên khóa + boolean; OAuth refresh HTTP 200/scope exact drive.file; giữ client/setup Desktop hiện có, không consent mới | PASS_SCOPE | AI chạy/quan sát API; không kiểm lại Google Console loại client |
| LIVE-02 | Dùng lại DEV System nếu có, tránh trùng | SystemSpreadsheetId chưa có; query đúng tên/mime/non-trashed trong file app được grant trả 0, không nextPageToken; provisioning tạo một file và lưu ID ngoài repo | PASS_PROVISION | AI chạy trực tiếp; discovery chỉ bao phủ file app được grant, không mọi Drive file |
| LIVE-03 | DEV riêng/owner/ACL hạn chế trước init | Tên DEV System, ownedByMe=true, canEdit=true, trashed=false, shared=false, một permission user/owner; khác spreadsheet nghiệp vụ | PASS_LIVE_READ | AI quan sát Google metadata; không in email/ID |
| LIVE-04 | Schema/tab/catalog trên Google thật | Init version 1; probe thấy đủ 13 tab hệ thống (còn tab mặc định Sheet1), schema 1/5 role đúng; Users=0, admin chưa tồn tại | PASS_LIVE_SCHEMA | AI chạy CLI và probe chỉ đọc, exit 0 |
| AC-03a live | Bootstrap tạo admin, xóa secret, chạy lại no-op | Secret chưa được nhập; chưa chạy bootstrap | NOT_RUN | Người dùng báo chưa thực hiện; AI kiểm secret presence=false |
| AC-03b/c live | First login/change password/restart còn đúng | Chưa có admin; chưa chạy | NOT_RUN | Không có bằng chứng |
| AC-03c/d/AC-04a live | Tạo user, Viewer/editor bị chặn admin; role/scope, audit, lock/reset/revoke/last-admin | Chưa chạy với Google dev | NOT_RUN | Local test không thay live |
| API regression | API giữ 401/403 kể cả Accept HTML | Trước sửa: anonymous API HTML 302. Sau sửa: anonymous HTML/JSON 401/no Location; Viewer HTTP integration JSON/HTML 403/no Location | PASS_LOCAL + PASS_LIVE_HTTP_ANONYMOUS | AI quan sát HTTP và chạy integration fake store; Viewer live chưa chạy |
| WEB-01 | Razor anonymous/validation/anti-forgery/access denied | HTTP probe 12/12: login 200, nhãn/type password, remember-me mặc định không chọn, anti-forgery hidden, POST thiếu token 400, empty validation, admin page redirect login, API 401, access denied 200 | PASS_HTTP | AI HTTP trực tiếp trên backend local dùng Google adapter; không là browser interaction/layout/cookie login |
| WEB-02 | UI tương tác login/admin/quyền/audit/cookie | CUA lần đầu lỗi kernel; sau reset inventory apps/browsers rỗng, getBrowser báo No browser is available | NOT_RUN_BROWSER | AI quan sát tool; đã chuẩn bị checklist thủ công |
| MOBILE-01 | Mobile login/change/rotate/reuse/revoke/logout thực trên Google | Runner có prompt ẩn/output đã làm sạch được tạo; mới parse syntax, chưa chạy live | NOT_RUN | Syntax pass không thay UAT |
| AC-04b/c/d core | Descendant/multiple assignments/move trong phạm vi TASK-005 | Unit core pass trong 58 tests; không tạo endpoint CRUD/report/file TASK-006 | PASS_LOCAL_CORE | AI chạy suite fake/stub; không tuyên bố integration endpoint chưa có |
| END-01 | Dừng web trước CLI tiếp theo, không thay TASK-003/004 | ServerCount=0, ListenerCount=0 sau dừng; TASK-003 IN_REVIEW/TASK-004 DONE | PASS_LOCAL_OBSERVATION | AI kiểm process/port; server bị chủ động ngắt, exit 1 được quan sát, không gọi đó là graceful exit 0 |

### Lệnh thực tế và kết quả

Working directory: `D:\Dev\QuanLyTaiSan_DuyTien`.

```powershell
git status --short
git rev-parse --show-toplevel
git log -1 --format='%h %cs'
git branch --show-current
git remote
dotnet --info
```

AI quan sát Git status ban đầu sạch; root đúng; HEAD b324c74 2026-10-07,
branch main, remote name origin. Các nhóm lệnh inspection kết thúc exit 0;
không gán exit riêng cho từng subcommand khi runtime chỉ cung cấp exit nhóm.

AI chạy PowerShell ad hoc bằng HttpClient, đọc user-secrets trong process và chỉ
xuất boolean/trạng thái: POST `https://oauth2.googleapis.com/token` (refresh),
GET Drive files với query `trashed = false and mimeType =
'application/vnd.google-apps.spreadsheet' and name =
'DuyTien Asset Management - DEV System'`, fields nextPageToken/id/name/ownedByMe/
trashed. Exit nhóm 0; OAuthExactDriveFile=True, accessible count=0,
DiscoveryComplete=True. Metadata spreadsheet nghiệp vụ cho tên DuyTien Assets
DEV/ownedByMe=true/non-trashed. Không xuất token/ID/email/body. Script ad hoc này
không có file lệnh riêng; thông tin trên mô tả thực thi, không giả tên command.

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- google-provision system-spreadsheet
# exit 0; tạo một DEV System, lưu ID ngoài repo, không in ID
```

Trước init, một PowerShell HttpClient probe thứ hai (exit nhóm 0) kiểm metadata
Drive theo system ID trong RAM: owner/ACL/shared/canEdit/mime/title và khác ID
nghiệp vụ; tất cả điều kiện đạt. Sheets metadata trước init có một tab Sheet1.

```powershell
dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https -- auth-system-init
# exit 0; Auth system schema version 1 initialized or verified

.\scripts\Test-Task005GoogleDev.ps1
# lần đầu exit 1 do probe chưa xử lý values null khi Users trống
# sau sửa probe: exit 0; scope/ACL/13 tab/schema 1/5 role pass; Users 0/AdminExists False

dotnet format .\QuanLyTaiSan.DuyTien.sln --no-restore --verify-no-changes --verbosity minimal
# exit 0

dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
# baseline và bản sửa đều exit 0; 0 warnings; 0 errors

dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal
# baseline: exit 0; 57 passed/0 failed/0 skipped
# sau sửa API: exit 0; 58 passed/0 failed/0 skipped

dotnet dev-certs https --check
# exit 0; valid dev certificate; check không chứng minh trusted

dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https
# backend giữ lease, lắng nghe HTTPS 7008/HTTP 5211; cảnh báo dev cert chưa trusted
# không có exit 0 cho server đang chạy; cuối mỗi probe AI chủ động ngắt, exit 1 được quan sát

python -B .\scripts\Test-Task005WebPreflight.py
# lần đầu script lưu: exit 1, 9/12 (API sửa đã đúng; 3 check chữ lỗi encoding probe)
# cuối: exit 0; 12/12 assertion; HTTP evidence, không browser UAT
```

Probe ad hoc HTTP đầu tiên exit 1, 5/9: có lỗi sản phẩm API HTML 302 và lỗi
expected text chưa decode entity/chuỗi tiếng Việt mất encoding khi đi qua
Windows PowerShell stdin. Đã sửa probe decode HTML và Unicode literal; không sửa
nhãn sản phẩm để làm test pass. Test hồi quy JSON/HTML cho API đã tăng một case
(thay Fact bằng Theory hai input), không skip hoặc làm yếu test.

Parser PowerShell `Test-Task005GoogleDev.ps1`: 0 lỗi. Hai Python probe/runner
được kiểm bằng `ast.parse`: exit nhóm 0, syntax pass; không chạy mobile runner.
`git diff --check` trước cập nhật docs: exit 0; kiểm tra cuối được ghi dưới đây.

### Lỗi đã sửa và giới hạn

- API challenge/forbidden dùng Accept HTML để redirect như Razor, trái hợp đồng
  401/403. Authentication handler chỉ redirect HTML nếu path ngoài `/api`.
  Integration JSON/HTML kiểm anonymous/admin/Viewer/cookie/hash boundary; HTTP
  anonymous live đã kiểm lại. Viewer live/cookie authenticated còn NOT_RUN.
- Probe Google xử lý Users trống, probe HTTP xử lý entity/Unicode; đây là lỗi
  công cụ kiểm tra, không là lỗi dữ liệu/auth sản phẩm.
- Chưa bootstrap/recovery/no-op/first password change/restart. Chưa có manual UI
  evidence. Cookie flags/remember/idle/absolute, last-admin và mobile rotation/
  reuse trên Google dev chưa được nghiệm thu. Core local pass được giữ đúng loại.
- Không tạo endpoint TASK-006, UI MAUI, không đổi scope, sharing/ACL, hosting hoặc
  password policy; không commit/push/deploy/PoC TASK-004.

### Quyết định hiện tại

TASK-005 giữ **IN_REVIEW**. Gate schema/resource đã đạt một phần; chưa đủ DONE
khi bootstrap/phiên/quyền live và browser UI/UAT còn NOT_RUN. Tất cả thao tác
manual có expected/actual/source tại [UAT-CHECKLIST-TASK-005](UAT-CHECKLIST-TASK-005.md).
Bước kế tiếp: người dùng nhập bootstrap secret qua prompt ẩn, sau đó bootstrap/
no-op/đổi mật khẩu/restart và UAT. TASK-003 IN_REVIEW, TASK-004 DONE giữ nguyên;
TASK-006 chỉ tiếp tục sau khi TASK-005 DONE theo PLAN-001.

### Kiểm tra tài liệu/artifact cuối

AI chạy riêng `git diff --check`: exit 0, không output. Python `ast.parse` cho
hai script và kiểm UTF-8 artifact mới: exit nhóm 0; PowerShell parser 0 lỗi.
TASK-003 vẫn IN_REVIEW/TASK-004 DONE trong file và INDEX. Requirement/Plan đã
sửa mô tả gate Google/role đã được xác lập, không đổi nghiệp vụ hay mở module.
Không chạy lại build/test sau chỉ chỉnh tài liệu; kết quả .NET cuối vẫn là
build sạch/58 test pass của bản sửa API. Mobile runner chưa chạy live.

## Tiếp tục bootstrap và UAT — 2026-10-09

Nguồn AI trực tiếp trừ những dòng ghi người dùng. Không in password/token/hash,
resource ID hoặc dữ liệu nhạy cảm; so sánh snapshot chỉ trong RAM.

| Case | Expected | Actual | Kết quả / nguồn |
|---|---|---|---|
| B01 bootstrap | Admin một lần, must-change, Admin + ALL ba chiều | CLI `auth-bootstrap-admin` exit 0; đúng một admin, MustChangePassword=True, một Admin assignment, ba ALL; khóa bootstrap bị xóa | PASS_GOOGLE; AI trực tiếp |
| B02 no-op | Secret khác không reset user/hash/version/role/scope | AI đặt secret một lần khác ngoài repo, CLI thứ hai exit 0/no-op; snapshot các hàng Users/RoleAssignments/RoleAssignmentScopes trước/sau giống hệt; khóa bị xóa | PASS_GOOGLE; AI trực tiếp |
| Driver bootstrap phiên bản đầu | Bootstrap + thử web hạn chế | 10 assertion bootstrap đạt; driver exit 1 với MethodInvocationException trước nhánh web/listening | PARTIAL/FAIL_DRIVER; không gán PASS cho cả driver |
| B04/B05 valid flow | Admin bắt buộc đổi mật khẩu rồi login lại | Người dùng: “Đã đổi mật khẩu và đăng nhập lại thành công” | PASS_MANUAL_VALID_FLOW; người dùng xác nhận thủ công; chưa chứng minh invalid current/7 ký tự/confirm trong form |
| UAT credential | Prompt ẩn, ngoài repo | Người dùng sửa `File.Replace` sang `NullString.Value`, xác nhận lưu credential UAT thành công | USER_RUN_CONFIRMATION; không có exit code do người dùng cung cấp |
| Script tương tự | Backup null đúng trên Windows PowerShell | AI tìm trong scripts, sửa bootstrap helper cùng pattern; parser 0 lỗi; probe file giả `FileReplaceNullStringProbe=True`, exit nhóm 0 | PASS_TOOL_FIX; không bootstrap lại dữ liệu |
| Live API/cookie/restart | Theo checklist | Runner bắt đầu: mobile admin login HTTP 200; còn đang chạy | IN_PROGRESS; chưa gán suite PASS/exit |

Lệnh thực tế:

```powershell
.\scripts\Test-Task005GoogleDev.ps1
# trước bootstrap: exit 0; scope/ACL/schema/catalog pass, Users 0

.\scripts\Test-Task005BootstrapLive.ps1
# phiên bản đầu: driver exit 1 sau 10 assertion bootstrap
# hai lệnh dotnet auth-bootstrap-admin bên trong: từng CLI exit 0 được quan sát
# snapshot/hash chỉ so sánh trong RAM, không in dữ liệu

dotnet run --project .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-build --launch-profile https
# backend listening HTTPS 7008/HTTP 5211, giữ lease; chưa có exit code khi đang chạy

.\scripts\Set-Task005UatSecret.ps1
# người dùng chạy, sửa NullString và xác nhận thành công; không cung cấp exit

python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret --restart-checkpoint
# AI chạy live, đang chờ các gate; sẽ ghi actual/exit sau khi hoàn tất
```

Driver đã thu hẹp về bootstrap/no-op sau lỗi nhánh startup; bản hiện tại parser
0 lỗi nhưng không chạy lại toàn driver với bootstrap secret đã xóa. Không gán
nguyên nhân cụ thể cho MethodInvocationException cũ khi chưa có stack sạch.
Sửa `NullString.Value` trong helper là cùng lỗi binding được người dùng phát
hiện ở prompt mới; probe temp xác minh pattern mà không đụng credential thật.
Runner dùng `os.replace` và `finally` để xóa khóa UAT tạm, giữ cấu hình khác.
Không chạy lại build/test vì chỉ thêm/sửa công cụ UAT/tài liệu; .NET cuối vẫn
build sạch/58 tests của bản sửa API. TASK-005 giữ IN_REVIEW; TASK-003 IN_REVIEW,
TASK-004 DONE. Chưa deploy/commit/push hoặc triển khai TASK-006.

### Runner live lần đầu — lỗi checkpoint công cụ

AI quan sát 9 assertion đạt: mobile admin login/me/role, Razor GET/POST login,
anti-forgery field, cookie tồn tại/flags đúng và cookie default không persistent.
Sau `RESTART_CHECKPOINT_READY`, stdin pipe của runtime trả EOF nên driver
RuntimeError/exit 1 trước restart; không là bằng chứng lỗi backend. `finally`
báo đã xóa khóa UAT tạm. Chưa suy diễn toàn suite PASS. Runner đã đổi checkpoint
sang marker file temp không chứa secret, timeout 10 phút và tự xóa marker;
syntax pass. Người dùng cần nhập lại credential qua prompt ẩn vì cleanup đúng.

AI kiểm lại cleanup key bằng boolean: `TemporaryUatSecretPresent=False`, exit nhóm 0; Google read probe sau bootstrap exit 0, Users 1/AdminExists True, ACL/schema/catalog giữ đúng. `git diff --check` exit nhóm 0. Không in store.

## Checkpoint cuối hiện tại — chờ credential UAT nhập lại

- TASK-005 IN_REVIEW. Bootstrap/no-op Google đạt; người dùng xác nhận admin
  forced-change và login lại. Google read probe sau bootstrap exit 0, Users 1,
  AdminExists True, owner-only ACL/schema/catalog không đổi.
- AI tìm lỗi UI bằng source review: Detail không tải/hiển thị các scope đã lưu,
  form GET về Viewer/scopes rỗng. Đã thêm read model assignment + scopes từ cùng
  snapshot; Razor hiển thị từng nhóm scope/cờ descendant. GET chỉ nạp form khi
  biểu diễn được một assignment với đúng ba scope/cờ chung; POST validation giữ
  input vừa nhập. Không thêm endpoint TASK-006 hay thay cách tính authorization.
- Ba test hành vi mới: GET nạp role/scope, nhiều assignment giữ scope riêng,
  validation không ghi đè input/không đổi quyền đã lưu. Fake chỉ là local proof.
- Format verify exit 0; build server bản cuối exit 0/0 warning/0 error; test
  bản cuối exit 0/61 passed/0 failed/0 skipped. Build/test chạy tuần tự, web dừng.
- Three PowerShell parser 0 lỗi và hai Python AST pass, exit nhóm 0.
  `git diff --check` exit 0 trước cập nhật cuối. Không gán syntax PASS thành live.
- AI Ctrl+C backend để build, tool quan sát exit 1; kiểm ServerCount=0,
  ListenerCount=0. Không để writer backend khác chạy khi tiếp tục.
- Credential UAT tạm đã cleanup sau runner fail; AI kiểm boolean False.
  Question nhập lại prompt ẩn còn chờ. Runner đã sửa marker checkpoint và thêm
  live HTTP cho role/scope, nhưng chưa chạy lại; không claim UI fix live PASS.

```powershell
dotnet format .\QuanLyTaiSan.DuyTien.sln --no-restore --verify-no-changes --verbosity minimal
# exit 0

dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
# exit 0; warnings 0; errors 0

dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal
# exit 0; total 61; passed 61; failed 0; skipped 0
```

Còn thiếu: restart, tạo/gán role/scope fake, Viewer và các quyền bị từ chối,
revoke cookie/access/refresh khi đổi quyền/khóa/reset, last-admin, refresh
rotation/reuse/logout/max5 và audit Google live; manual browser ngoài valid
forced-change/login flow (validation/quản trị/layout). Timeout elapsed live
vẫn NOT_RUN, local TimeProvider được giữ riêng. Không nâng TASK-005 lên DONE.
Bước gần nhất: người dùng chạy lại `Set-Task005UatSecret.ps1`, chỉ báo trạng thái;
AI start một backend rồi chạy runner với checkpoint marker, tiếp tục manual UAT.
TASK-003 IN_REVIEW, TASK-004 DONE. TASK-006 chờ TASK-005 DONE theo PLAN-001;
không deploy/commit/push trong lượt này.

## Tiếp tục checkpoint restart — 2026-10-09

Người dùng xác nhận nhập lại credential tạm/web đã dừng. AI kiểm boolean:
credential Present=True; ServerCount/ListenerCount=0; Google read probe exit0
vẫn đúng DEV/owner-only ACL/drive.file/schema1/5 role/Users1/AdminExists True.
Không chạy provisioning/init/bootstrap hoặc PoC TASK-004 lại. Browser inventory
apps/browsers rỗng, chưa có automation UI.

AI start backend bằng command Run trong COMMANDS (listening7008/5211, giữ lease).
Runner cùng lệnh --operator-secret --restart-checkpoint, không phụ thuộc stdin.
Tại marker checkpoint, AI Ctrl+C backend: exit1 được quan sát; probe process/port
0 exit0; start lại command Run và quan sát listening. Marker lần đầu chưa tạo:
ad hoc precondition đếm đúng2 listener rows fail/exit1 vì localhost có IPv4+IPv6.
AI sửa probe đếm unique ports/owner: BackendCount1/ListenerRows4/UniquePorts2/
ListenerOwners1, exit0 rồi tạo marker. Runner xóa marker (AI boolean False).

| Case | Expected | Actual | Kết quả / nguồn |
|---|---|---|---|
| Restart mobile | Token cũ dùng được, user/role/must-change còn đúng | /api/me200, cùng identity/roles, MustChangePassword=False | PASS_GOOGLE; AI trực tiếp |
| Restart Razor cookie | Cookie trước restart còn dùng được | /Admin/Users200 | PASS_HTTP_GOOGLE; không thay browser |
| Login sau restart | Mật khẩu hiện tại vẫn dùng được | Login200, logout phiên bổ sung204 | PASS_GOOGLE; AI trực tiếp |
| Last admin lock/downgrade/dropALL | Cả ba409, user giữ nguyên | Đúng một bootstrap user; lock409, Viewer409, bỏ một ALL409; list200/snapshot không đổi | PASS_GOOGLE; AI trực tiếp |
| Razor scope detail | URL đúng/200 | Runner gọi sai /Admin/Users/{id} ->404, abort | FAIL_TOOL_ROUTE; chưa là lỗi backend |
| Cleanup | Xóa khóa UAT và marker | finally báo xóa; AI kiểm TemporaryUatSecretPresent=False/RestartMarkerPresent=False | PASS_CLEANUP; AI trực tiếp |

Runner exit1, 20 assertion PASS và1 FAIL do URL công cụ, không full-suite PASS.
Đã sửa cả ba chỗ sang /Admin/Users/Detail/{id}, đối chiếu @page và asp-page link.
Probe HTTP anonymous exit0: route mới302/login, route cũ404; chỉ chứng minh route,
không authenticated scope UI pass. Chưa rerun; credential đã cleanup đúng flow,
cần prompt nhập lại. [Artifact sạch](TASK-005-LIVE-EVIDENCE.json) lưu từng case
expected/actual/source, không chứa secret/token/hash/body.

Backend hiện chạy một instance; TASK-005 IN_REVIEW. Restart và last-admin API đã
đạt; còn Viewer/revoke/rotation/reuse/fixture elapsed/audit live và browser UAT.
Code sản phẩm chưa đổi lượt này; .NET proof cuối vẫn build sạch/61 tests trước.

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

### Kết thúc lượt theo chỉ đạo mới 2026-10-10

TASK-005 IN_REVIEW; UAT còn lại DEFERRED_BY_USER/W13 STOPPED_BY_USER. TASK-006 local đã triển khai và IN_REVIEW theo ngoại lệ đã cho phép, không tiếp tục chờ17/10 hoặc giao G2c. Kiểm cuối AI: đúng runner18964 không còn, UAT/bootstrap secret absent, backend workspace đã dừng để build. Xác nhận lịch sử G2a/G2b/manual và timeout30ph giữ nguyên; không xóa user/audit. Xem TASK-006/Test Report mới; các đoạn phía trên là lịch sử khi gate chưa được hoãn.
