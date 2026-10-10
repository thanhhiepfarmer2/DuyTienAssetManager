# TASK-005 — Đăng nhập, admin và phân quyền server/web

## Ngoại lệ đã được người dùng cho phép — 2026-10-10

Người dùng dừng nghiệm thu TASK-005 và cho phép triển khai local TASK-006 dù TASK-005 chưa DONE (2026-10-10).
TASK-005 giữ `IN_REVIEW`: G2a/G2b PASS_MANUAL; G2c và browser còn mở DEFERRED_BY_USER; idle 30 phút thực PASS; W13 8h/24h/7d STOPPED_BY_USER. PID 18964 đã dừng đúng runner, credential UAT được AI xác minh không còn; giữ tài khoản/audit/bằng chứng lịch sử. Không chờ 17/10, không tiếp tục giao UAT hoặc yêu cầu secret. TASK-003 IN_REVIEW, TASK-004 DONE.


- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Design: [AUTH-DESIGN-PROPOSED](../02-architecture/AUTH-DESIGN-PROPOSED.md)
- Created: 2026-10-06; design accepted/implementation started: 2026-10-07
- Phụ thuộc/gate: TASK-004 `DONE`; auth/permission/scope đã được người dùng duyệt;
  ngày 2026-10-09 đã provisioning/ACL/init schema DEV; bootstrap, phiên/quyền
  live Google đã đạt; browser UI/UAT và timeout thực còn là gate trước `DONE`
- AC liên quan: AC-03, AC-04

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

## Mục tiêu và phạm vi đã duyệt

Triển khai một lát đầy đủ backend/API + Razor Pages:

- system spreadsheet riêng cho Users/Roles/Permissions/Assignments/Scopes/
  Sessions/SecurityEvents;
- bootstrap/recovery admin bằng lệnh vận hành riêng, secret ngoài repo;
- login/logout/logout-all/change-password, web cookie và mobile token API;
- Admin tạo/sửa/khóa/mở/reset user, gán năm role cố định và scope;
- authorization server-side thống nhất cho list/detail/report/export/file/write;
- last-admin protection và revoke tức thời bằng `SecurityVersion`;
- Razor Pages login, đổi mật khẩu, access denied và quản trị user/quyền;
- test bằng adapter giả/dữ liệu scope giả, sau đó live evidence trên system
  spreadsheet dev trước khi xét `DONE`.

Ngoài phạm vi TASK-005: UI MAUI, SSO/MFA/email forgot-password, custom role,
permission trực tiếp, cơ cấu tổ chức thật và code nghiệp vụ TASK-006 trở đi.

## Đầu vào, phụ thuộc và giả định

- Các quyết định `ACCEPTED` tại AGENTS/REQ-001/ADR-001 được giữ nguyên.
- Ma trận role/permission và scope nằm tại
  [PERMISSIONS](../01-requirements/PERMISSIONS.md), đã `ACCEPTED`.
- Schema auth nằm tại [SHEETS-SCHEMA](../03-database/SHEETS-SCHEMA.md), phần
  TASK-005 đã `ACCEPTED`; DEV System đã được tạo/init ngày 2026-10-09,
  owner-only ACL được xác minh trực tiếp; đã bootstrap admin; no-op không đổi dữ liệu và khóa bootstrap đã xóa.
- Tất cả OrgUnit/Location/Category/User dùng cho test thiết kế là fixture giả,
  không mô tả tổ chức Công ty Duy Tiến.
- Không yêu cầu người dùng cung cấp mật khẩu trong chat; secret chỉ được nhập vào
  user-secrets/secret store khi bắt đầu implementation/live setup được cho phép.

## Acceptance criteria

- [x] AC-03a local: bootstrap bằng lệnh riêng tạo `admin` đúng một lần; chạy lại
  không reset hash, role, scope hoặc mật khẩu.
- [x] AC-03b local: plaintext password/token không xuất hiện trong repo, log, response
  thường, Sheet hay build artifact; bootstrap/reset buộc đổi mật khẩu lần đầu.
- [x] AC-03c local: login/logout/change-password/lockout/rate limit/web cookie/mobile
  refresh hoạt động theo timeout đã duyệt; marker token rotate phát hiện reuse;
  reset/khóa/đổi quyền revoke tức thời.
- [x] AC-03d local: không thể khóa, hạ role hoặc bỏ ALL của admin hoạt động cuối cùng;
  recovery chỉ chạy khi không còn admin hoạt động và ở maintenance mode.
- [x] AC-04a local: Viewer bị chặn API admin; AssetEditor không có
  `USERS_MANAGE`/`AUTHZ_ASSIGN`; server là gate. Endpoint tài sản chưa tồn tại và
  không được tạo sớm trong TASK-005.
- [x] AC-04b local core: `Location.ParentId` được validate; exact/descendant/
  sibling, thiếu scope và source+destination move dùng cùng authorization service.
- [x] AC-04c local: nhiều role cộng permission nhưng không cross-combine scope giữa hai
  assignment; permission trực tiếp và deny không tồn tại trong demo.
- [x] AC-04d contract/core: resource thiếu/ngoài scope fail-closed; contract quy
  định 404/403 và lọc trước count. Integration từng endpoint nghiệp vụ sẽ thuộc
  Task tạo endpoint đó, không triển khai CRUD/report sớm.
- [x] AC-04e local build/review: Razor Pages login/change-password/admin dùng cùng
  service với API, POST có anti-forgery mặc định, validation/empty/access-denied.
- [ ] UI/live UAT: tương tác Razor Pages với system spreadsheet dev thật.
- [x] Live Google gate: system spreadsheet dev được provisioning/Picker, ACL hạn chế,
  init/bootstrap/restart/revoke kiểm chứng trên Google thật. Fake không thay live.

## Routing/skill đã dùng

- `conductor:context-driven-development`: Context → Spec/Plan, giữ Requirement,
  Plan, Task và tài liệu liên quan đồng bộ; không tạo thư mục context song song.
- `developer-essentials:auth-implementation-patterns`: phân tách authn/authz,
  cookie/token, password hash, rate limit, RBAC và revoke/session.
- `dotnet-contribution:dotnet-backend-patterns`: DI, API, Razor và framework
  password hasher; `ui-design:accessibility-compliance` cho label, validation,
  status/access-denied và form có keyboard semantics.
- Không dùng subagent vì người dùng/skill không yêu cầu.

## Kế hoạch thực hiện local

1. Đồng bộ quyết định đã duyệt, gồm password min 8, `Location.ParentId`, active
   admin theo assignment hiệu lực và process lease liên tiến trình.
2. Tạo abstraction/domain auth và fake store; test password/session/RBAC/scope
   trước khi nối Google.
3. Triển khai system Sheets adapter, command provisioning/Picker/init/bootstrap/
   recovery; không tự chạy command ghi khi start web.
4. Triển khai API/cookie/mobile token và authorization service.
5. Triển khai Razor Pages login/change-password/admin; dùng chung service.
6. Chạy build/test/format, review secret/log/API/scope và cập nhật report.
7. Chỉ khi được phép mới provisioning/Picker system spreadsheet dev và chạy live
   evidence; không đụng production hoặc resource TASK-004 đã hoàn tất.

## Bằng chứng implementation local lịch sử — 2026-10-07

| Kiểm tra | Kết quả |
|---|---|
| Format | PASS — `dotnet format` và `--verify-no-changes`, exit 0 |
| Build server | PASS — exit 0; 0 warnings; 0 errors |
| Test | PASS — 57 passed; 0 failed; 0 skipped |
| Google provisioning/Picker/init/bootstrap | NOT_RUN — không được thực hiện trong lượt local |
| Git/deploy/commit/push | NOT_RUN — thư mục không phải Git worktree và không được yêu cầu |
| Chi tiết | [TEST-REPORT-TASK-005](../07-testing/TEST-REPORT-TASK-005.md) |

## Checkpoint / Review

- TASK-004 giữ `DONE`; TASK-003 giữ `IN_REVIEW`; không mở lại test Google cũ.
- TASK-005 chuyển `IN_REVIEW` vì local build/test/review đạt; chưa `DONE` trước
  live Google và UI/UAT.
- Ba điểm nghiệp vụ ở mục 9 và phương án demo đã được người dùng duyệt ngày
  2026-10-07; các điều chỉnh cùng ngày là authoritative.

## Checkpoint đầu lượt live/UAT — 2026-10-09 (trước bootstrap)

- Người dùng cho phép tài khoản dev và dữ liệu hoàn toàn giả, provisioning system
  spreadsheet riêng/init/bootstrap. Người dùng làm rõ vẫn dùng máy đã setup,
  không có bước chuyển máy. Không chạy lại PoC TASK-004.
- Git ban đầu sạch, main/origin/HEAD b324c74; không commit/push/deploy.
- OAuth exact drive.file, discovery không có DEV System cùng tên trong các file
  app được cấp quyền; flow provisioning và init đều exit 0. AI xác minh trực tiếp
  DEV riêng, owner-only ACL, 13 tab/schema 1/5 role; user count 0.
- Đã sửa lỗi API trả redirect với Accept HTML: API giữ 401/403, Razor còn redirect.
  Test hồi quy cả JSON/HTML, local build sạch/test 58/58 và verify format exit 0.
- HTTP Razor chưa đăng nhập: 12/12 assertion, exit 0; không thay tương tác browser.
- Browser runtime sau reset hoạt động nhưng inventory rỗng/getBrowser báo không
  có browser. Checklist thủ công và runner mobile API được chuẩn bị; runner
  mobile mới kiểm syntax, chưa chạy live.
- Secret bootstrap còn thiếu: người dùng báo chưa nhập; AI kiểm có/không thấy
  false. Bootstrap/no-op/đổi mật khẩu/restart/revoke/quyền/cookie còn NOT_RUN.
  Web cuối lượt đã dừng. Không tạo admin bằng password tự đặt hoặc từ chat.
- Skill áp dụng lượt này: developer-essentials:auth-implementation-patterns;
  chỉ dùng authn/authz/cookie/token/revoke. Policy mật khẩu chỉ min 8 theo yêu
  cầu authoritative, không áp dụng khuyến nghị complexity/MFA ngoài phạm vi.
- Self-review diff và lệnh đã chạy, không gọi subagent hoặc báo review độc lập.
- Trạng thái giữ IN_REVIEW; checklist
  [UAT-CHECKLIST-TASK-005](../07-testing/UAT-CHECKLIST-TASK-005.md), actual/source
  và lệnh tại [Test Report](../07-testing/TEST-REPORT-TASK-005.md).
- TASK-003 giữ IN_REVIEW, TASK-004 giữ DONE; TASK-006 chưa triển khai. Theo PLAN-001,
  chỉ chuyển sang TASK-006 sau khi TASK-005 đạt toàn bộ gate và DONE.

## Tiếp tục sau nhập secret — 2026-10-09

- AI chạy bootstrap và no-op bằng secret khác: hai CLI exit 0; đúng một admin,
  MustChangePassword/Admin/ba ALL đúng; snapshot Users/assignment/scope (gồm
  hash/version trong RAM) không đổi ở lần hai; khóa bootstrap đã xóa.
- Driver đầu exit 1 sau 10 assertion bootstrap, trước nhánh web: không coi toàn
  driver PASS. Helper hiện thu hẹp bootstrap-only/parser 0 lỗi.
- Người dùng xác nhận đổi mật khẩu bắt buộc và đăng nhập lại thành công. Đây là
  manual valid-flow; chưa suy diễn validation/cookie/quyền/layout đã pass.
- Người dùng sửa prompt `File.Replace` bằng `NullString.Value`, lưu credential
  UAT tạm thành công; AI sửa cùng lỗi trong bootstrap helper, temp probe pass.
- Mobile/Razor HTTP runner lần đầu đạt 9 assertion rồi exit 1 do stdin EOF tại
  checkpoint công cụ; credential tạm đã xóa. Đã sửa checkpoint marker, đang chờ
  người dùng nhập lại credential qua prompt ẩn. Web đã dừng để build bản sửa UI.
- TASK-003 IN_REVIEW, TASK-004 DONE; không commit/push/deploy/TASK-006.

- Review phát hiện Detail không hiển thị/nạp scope đã lưu; đã sửa read model,
  hiển thị từng assignment và nạp form khi biểu diễn được; validation giữ input.
  Thêm 3 test hành vi: local format verify/build exit 0, build 0 warning/error,
  tests 61/61 exit 0. Bản UI mới chưa có live browser proof.
- Checkpoint cuối: ServerCount/ListenerCount=0; credential UAT tạm không còn.
  Bootstrap/no-op/valid admin flow đạt; restart/quyền/revoke/token/audit live và
  manual UI còn thiếu. Giữ IN_REVIEW; nguồn/expected/actual tại report/checklist.

## Checkpoint restart đã nghiệm thu

- AI xác minh Google read probe exit0/DEV owner-only/drive.file/Users1; không lặp
  provisioning/init/bootstrap. Credential tạm được dùng trong RAM.
- Restart mobile/cookie/identity và login password mới đạt trên Google; marker
  tự xóa. Một backend hiện running, không stdin checkpoint.
- Last-admin API lock/downgrade/dropALL đều409, user snapshot giữ nguyên, pass live.
- Runner exit1 sau20pass/1fail vì URL Razor thiếu /Detail; đã sửa/probe anonymous
  route, chưa rerun; credential đã xóa và AI boolean False.
- TASK-005 vẫn IN_REVIEW chờ Viewer/revoke/rotation/reuse/audit và browser UAT.


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

### Kết thúc lượt theo chỉ đạo mới 2026-10-10

TASK-005 IN_REVIEW; UAT còn lại DEFERRED_BY_USER/W13 STOPPED_BY_USER. TASK-006 local đã triển khai và IN_REVIEW theo ngoại lệ đã cho phép, không tiếp tục chờ17/10 hoặc giao G2c. Kiểm cuối AI: đúng runner18964 không còn, UAT/bootstrap secret absent, backend workspace đã dừng để build. Xác nhận lịch sử G2a/G2b/manual và timeout30ph giữ nguyên; không xóa user/audit. Xem TASK-006/Test Report mới; các đoạn phía trên là lịch sử khi gate chưa được hoãn.
