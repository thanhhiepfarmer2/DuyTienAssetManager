# Bối cảnh dự án

## Checkpoint hiện hành TASK-009 — 2026-10-13

TASK-009 IN_REVIEW: phần local ảnh web/metadata/primary/soft-delete, registry và version DOCX/PDF, same-version publish PUBLISHED_DEMO có confirm/UTC effective date, scoped gateway, durable PENDING/FAILED/AVAILABLE/retry/reconcile/retention trên Sheets/Drive đã triển khai API/Razor/Google adapters. Build0warnings/errors exit0;56/56 task +67/67 hồi quy exit0,0failed/skipped; fixture tổng hợp/fake Google/Kestrel HTTP, không Google live/browser. [Report](07-testing/TEST-REPORT-TASK-009.md), [thiết kế](02-architecture/MEDIA-DESIGN-TASK-009.md).

DRAFT/DEMO không tự chính thức, mẫu nguồn/Drive target/workbook/rule/Org-Location/retention còn NOT_RUN/OPEN/TBD. Guard TASK-008 nguyên vẹn, khi Files/Documents/Versions có mặt tree mutation fail-closed, chưa đối soát impact module mới; không bypass/rename để mở. TASK-003 IN_REVIEW, TASK-004 DONE, TASK-005/006/007/008 IN_REVIEW, hoãn TASK-005 DEFERRED_BY_USER giữ nguyên; TASK-010–014 TODO. Ngoại lệ TASK-009 mới thay hạn chế không làm TASK-009 ở checkpoint trước, giữ bằng chứng lịch sử.

Worktree TASK-007/008 đầu lượt giữ; HEADc643fcb không đổi, không commit/push/deploy/real writes, secret UAT hoặc init/bootstrap/provision/live UAT. Không yêu cầu thao tác thủ công hoặc tự triển khai TASK-010. Các đoạn bên dưới là checkpoint lịch sử, INDEX/đoạn hiện hành này là trạng thái hiện tại.

## Ngoại lệ phụ thuộc TASK-009 — 2026-10-13

Người dùng cho phép hoàn tất phần local TASK-009 dù TASK-005/006/007/008 IN_REVIEW; không đóng gate trước, giữ DEFERRED_BY_USER. Không Google live/UAT thủ công/dữ liệu thật/commit/push/deploy/TASK-010. Guard metadata TASK-008 giữ nguyên, không bypass; tab Files/Documents làm tree mutation fail-closed cho đến khi có thiết kế đối soát module được giao riêng. Rule/workbook/Org-Location OPEN/TBD; file mẫu thật và Drive target chưa xác minh, fixture giả chỉ cho local.


## Checkpoint TASK-008 trước TASK-009 — 2026-10-13

TASK-008 IN_REVIEW: phần local sửa tên/mã nháp/definition, move cây con tới F10, archive với preview/plan/confirm, bulk Asset scope nguồn/đích, history/mapping và TXT theo snapshot mới đã triển khai backend/API/Razor/Google joint adapter. Publish kỹ thuật giữ DRAFT, node ngừng DRAFT_INACTIVE, Asset DRAFT_DEMO; ID/code/counter/alias giữ nguyên. Build cuối0warnings/errors exit0;42/42 task +91/91 hồi quy liên quan exit0, failed/skipped0; fixture giả/Google stub/Kestrel compiled Razor HTTP, không Google live/browser.

[Report TASK-008](07-testing/TEST-REPORT-TASK-008.md), [thiết kế](02-architecture/TREE-MUTATION-DESIGN-TASK-008.md). Google live/browser/source workbook/Org-Location/rule/fields công ty còn NOT_RUN/OPEN/TBD; không phê duyệt DRAFT hoặc gate trước. TASK-003 IN_REVIEW, TASK-004 DONE, TASK-005/006/007 IN_REVIEW, hoãn TASK-005 DEFERRED_BY_USER giữ nguyên; TASK-009–014 TODO. Ngoại lệ mới cho TASK-008 thay giới hạn không làm TASK-008 ở checkpoint TASK-007, không sửa bằng chứng lịch sử.

Git HEAD c643fcb không đổi, worktree giữ33 thay đổi đầu lượt TASK-007 và thêm TASK-008 chưa commit; không commit/push/deploy. Probe backend0/W13 runner0 exit0, không stop/start process tồn tại hoặc dùng credential UAT, không ghi Google/dữ liệu thật. Các checkpoint bên dưới là lịch sử; trạng thái hiện hành theo đoạn này/INDEX. Không giao UAT thủ công hoặc tự tiếp tục TASK-009.

## Ngoại lệ phụ thuộc TASK-008 — 2026-10-13

Người dùng cho phép hoàn tất phần local TASK-008 dù TASK-005/006/007 còn IN_REVIEW. Các gate hoãn TASK-005 và nguồn/workbook/Google/browser TASK-006/007 giữ nguyên; rule mã/fields/Org-Location công ty OPEN/TBD. Ngoại lệ không phê duyệt DRAFT, không cho ghi dữ liệu thật/commit/push/deploy/TASK-009. Publish kỹ thuật là đổi pointer nguyên tử sang snapshot DRAFT đầy đủ, không công bố mã chính thức.


## Checkpoint TASK-007 trước TASK-008 — 2026-10-13

TASK-007 IN_REVIEW: phần local CRUD một loại MACHINE giả đã thực hiện, state DRAFT_DEMO, code test có rule riêng, alias/soft-delete/restore/search/filter/paging/Version409, scope server TASK-005 và Categories DRAFT TASK-006. Backend/API/Razor và Google atomic/reconcile adapter đã build sạch exit0; 54/54 test task +9/9 hồi quy local pass exit0, failed/skipped0. [Báo cáo](07-testing/TEST-REPORT-TASK-007.md), [thiết kế](02-architecture/ASSET-DEMO-DESIGN-TASK-007.md).
Rule/trường nghiệp vụ công ty và source Org/Location chưa xác minh/phê duyệt; Google live/browser NOT_RUN, không lấy HTTP stub/render thay evidence thật. TASK-005 IN_REVIEW, hoãn giữ DEFERRED_BY_USER; TASK-006 IN_REVIEW, workbook gốc/Google/browser vẫn chưa xác minh. TASK-003 IN_REVIEW, TASK-004 DONE; TASK-008–014 TODO.
Git đã có checkpoint c643fcb lưu/push ở lượt10/10; lượt13/10 này không commit/push/deploy, worktree chứa thay đổi TASK-007. Probe cuối backend0 và W13 runner0 exit0; không stop/start process hay nhập secret UAT, không ghi Google/dữ liệu thật. Các đoạn bên dưới là checkpoint lịch sử, không thay trạng thái hiện hành này.


## Ngoại lệ phụ thuộc TASK-007 — 2026-10-13

Người dùng cho phép triển khai đầy đủ phần local TASK-007 dù TASK-006 chưa DONE. TASK-005 giữ IN_REVIEW và DEFERRED_BY_USER; TASK-006 giữ IN_REVIEW, workbook gốc/Google live/browser chưa xác minh. Ngoại lệ chỉ cho bắt đầu local, không đóng gate và không cho phép ghi dữ liệu thật, commit/push/deploy hoặc làm TASK-008. Rule mã/trường nghiệp vụ công ty chưa chốt: dùng fixture giả có nhãn cho phần độc lập, không tự phê duyệt rule hay DRAFT.


## Kết quả local TASK-006 — 2026-10-10

TASK-006 `IN_REVIEW`: preview/validation/idempotent import/version diff DRAFT, tree/detail/tìm/TXT và permission/scope server, Google business adapter/API/Razor đã triển khai. Build sạch exit 0; 46/46 test task + 12/12 hồi quy liên quan pass, failed/skipped 0, exit 0. Staging đúng 665/15/98/552/39. Workbook XLSX gốc không có trong repository; metadata gốc/hash chưa tái xác minh. Google live/browser TASK-006 NOT_RUN, không chứng nhận từ fake/render HTML. [Báo cáo](07-testing/TEST-REPORT-TASK-006.md).
Backend workspace PID18664 đã dừng đúng executable trước build, chưa restart UAT. Không tự nhập staging vào Google. TASK-007–014 vẫn TODO; chưa commit/push/deploy.


## Ngoại lệ đã được người dùng cho phép — 2026-10-10

Người dùng dừng nghiệm thu TASK-005 và cho phép triển khai local TASK-006 dù TASK-005 chưa DONE (2026-10-10).
TASK-005 giữ `IN_REVIEW`: G2a/G2b PASS_MANUAL; G2c và browser còn mở DEFERRED_BY_USER; idle 30 phút thực PASS; W13 8h/24h/7d STOPPED_BY_USER. PID 18964 đã dừng đúng runner, credential UAT được AI xác minh không còn; giữ tài khoản/audit/bằng chứng lịch sử. Không chờ 17/10, không tiếp tục giao UAT hoặc yêu cầu secret. TASK-003 IN_REVIEW, TASK-004 DONE.


Ngày khởi tạo bộ mẫu: 2026-10-05. Ngày cập nhật: 2026-10-10.
Trạng thái: TASK-005 IN_REVIEW; baseline local78/78/live173/173 exit0 được giữ.
G1a/G1b người dùng xác nhận PASS thủ công ngày2026-10-10. Thêm14 test boundary
TimeProvider PASS_LOCAL exit0; không đổi policy/product, không rerun suite đã đạt.
G2a bốn bước PASS_MANUAL (rỗng/thiếu tên/tạo hợp lệ/trùng không bản sao);
G2b tên/Viewer/scope/GET mở lại PASS_MANUAL;
G2c validation/password8/login sau reset PASS_MANUAL;
G2d Viewer admin URLs bị chặn/own form truy cập PASS_MANUAL;
G2e trở đi/editor/revoke/layout/flags còn mở; W10 đã PASS_HTTP_GOOGLE; W13 thời gian thực
default idle30ph đã PASS thực lúc14:44:57; ba mốc8h/24h/7ngày
còn mở. Reset/Login sau mất password tạm PASS_MANUAL; G2c validation đã PASS_MANUAL, G2d Viewer đã PASS_MANUAL, chờ G2e đổi quyền. Runner PID18964 giữ cookie chỉ RAM; credential tạm
đã xóa/xác minh absent độc lập sau setup.
Một backend HTTPS7008/HTTP5211, owner1; TASK-003 IN_REVIEW, TASK-004 DONE.

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
ở test. Ngày 2026-10-09 đã provisioning/init system spreadsheet DEV riêng và
kiểm tra chỉ đọc trực tiếp trên Google; admin đã bootstrap; no-op không đổi user/hash/version/role/ALL, khóa bootstrap xóa.

Code TASK-004 gọi Google qua scope duy nhất `drive.file`, có provisioning/Picker
và fail-closed với token scope rộng. Người dùng đã chạy readiness,
access-boundary, init và PoC trên Google thật; kết quả chi tiết nằm tại
[TEST-REPORT-TASK-004](07-testing/TEST-REPORT-TASK-004.md). Người dùng đã xác nhận
thủ công tab/record, TXT đúng folder/owner và tính hợp lệ file control; AI không
trực tiếp quan sát. TASK-005 đã có implementation local nhưng chưa đủ live/UAT;
TASK-006 local đã triển khai (xem kết quả hiện hành ở đầu tài liệu); CRUD tài sản/báo cáo và TASK-007–TASK-014 chưa triển khai.
`LiveConnectionVerified=false` khi thiếu cấu hình vẫn là trạng thái runtime đúng
và không tự thay thế evidence môi trường.

## Đã kiểm tra trên máy local

- Hiện tại (2026-10-09): Git worktree tại thư mục dự án, branch `main`, remote
  có tên `origin`, HEAD `b324c74` (2026-10-07). Git status ban đầu sạch, exit 0.
  Không xem/in URL remote, không fetch/commit/push. Checkpoint lịch sử TASK-003
  chưa có Git và không khởi tạo Git được giữ nguyên trong report cũ.
- SDK 10.0.400, runtime 10.0.11, MSBuild 18.9.6; MAUI workload 10.0.20/10.0.100 đã cài.
- Restore solution: exit 0.
- Build server: exit 0, 0 warnings, 0 errors.
- Architecture tests sau sửa logging: exit 0; passed 31, failed 0, skipped 0;
  gồm request scope, token fail-closed, Picker/provisioning, HTTP stub allow/deny
  và 2 test canary bảo đảm HttpClient log không lộ URL/resource ID, token/header
  hay body nhạy cảm.
- Checkpoint TASK-005 local lịch sử 2026-10-07: `dotnet format` và verify exit 0; build server exit 0,
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
| Git/remote | Đã có Git: `main`, remote `origin`, HEAD `b324c74` | Không commit/push/deploy trong lượt TASK-005 live/UAT |

## Công việc hiện tại

- [TASK-001](06-tasks/TASK-001-bootstrap.md): `DONE`.
- [TASK-002](06-tasks/TASK-002.md): `DONE`.
- [TASK-003](06-tasks/TASK-003.md): `IN_REVIEW` — giữ nguyên; Google đã có
  evidence, hosting/domain và thiết bị/signing còn mở.
- [TASK-004](06-tasks/TASK-004.md): `DONE` — local build/31 tests, live Google và
  manual tab/TXT/control-file có bằng chứng do người dùng cung cấp.
- [TASK-005](06-tasks/TASK-005.md): `IN_REVIEW` — local78/78/build sạch;
  Google live173/173 exit0 và cleanup verified. Browser G1 trước fix lỗi quota;
  G1a/G1b, G2a/G2b PASS_MANUAL; W10 PASS_HTTP_GOOGLE; nghiệm thu còn lại DEFERRED_BY_USER, W13 dài STOPPED_BY_USER; xem
  [checklist](07-testing/UAT-CHECKLIST-TASK-005.md).
- [TASK-006](06-tasks/TASK-006.md): `IN_REVIEW` — local build/46 tests/12 hồi quy pass; Google/browser/workbook gốc chưa kiểm.
- TASK-007–TASK-014: `TODO`; chưa triển khai.

TASK-005 có [thiết kế auth `ACCEPTED`](02-architecture/AUTH-DESIGN-PROPOSED.md):
năm role cố định, scope gắn role assignment, password chỉ min 8, session/refresh
rotation, bootstrap/recovery command, system spreadsheet riêng, API + Razor Pages
và test bằng dữ liệu giả. `Location.ParentId`, active admin theo assignment hiệu
lực/đủ ALL và process lease liên tiến trình đã được code/test local. Lượt local
2026-10-07 chưa ghi system spreadsheet; lượt 2026-10-09 đã tạo/init DEV riêng.
Người dùng xác nhận vẫn dùng máy đã setup, chưa chuyển máy; process/port kiểm
trước CLI. AI stop/build/start đúng một backend, restart marker đã kiểm chứng;
không lặp provisioning/init/bootstrap đã đạt. Bootstrap secret và credential UAT
đều xóa/xác minh. Backend hiện chạy để browser UAT, Task chưa đủ gate DONE.

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

- 2026-10-09: xác minh Git hiện có; dùng lại OAuth Desktop/drive.file đã cấu
  hình, không mở rộng scope hoặc chạy lại PoC TASK-004. Provisioning DEV System,
  xác minh owner-only ACL và schema version 1 bằng AI trực tiếp; chưa bootstrap.
  Sửa API giữ 401/403 với Accept HTML; build sạch/58 test pass/HTTP preflight
  12/12. TASK-005 vẫn IN_REVIEW vì thiếu secret, phiên/quyền live và browser UAT.

- 2026-10-09 tiếp tục: bootstrap/no-op Google pass (hai CLI exit 0, snapshot giữ nguyên); người dùng xác nhận đổi mật khẩu/login lại; sửa NullString trong UAT helper. Runner live đang chạy, TASK-005 giữ IN_REVIEW chờ đầy đủ gate.

- Checkpoint tiếp tục: sửa Razor Detail để hiển thị/nạp scope đã lưu; format/build sạch và 61/61 test pass. Runner live lần đầu có 9 assertion đạt rồi exit 1 ở checkpoint công cụ, đã cleanup credential; đang chờ prompt nhập lại để chạy restart/phiên/quyền. Web đã dừng, TASK-005 vẫn IN_REVIEW.

- Checkpoint restart: marker flow đã chạy, mobile/cookie/identity/password sau restart pass và last-admin API409 ba thao tác/user không đổi. Runner20pass/1fail do URL công cụ, đã sửa; credential/marker xóa, backend chạy một instance. TASK-005 IN_REVIEW chờ live còn lại/browser.

- 2026-10-09 sửa lỗi quota: runner9pass/1fail exit1, G1 lỗi quay lại login; batch reads/no-op writes/503 đã local78tests+build sạch. Live/browser chờ retest, credential xóa/xác minh; TASK-005 IN_REVIEW.

- 2026-10-09 sau sửa quota: live runner173/173 exit0; access/lockout15 phút thật, Viewer/revoke/rotation/reuse/audit/max5 đạt; cleanup verified, read probe owner-only/drive.file/schema/catalog đúng, Users4. Web1instance; browser G1 retest còn chờ.

## Checkpoint lịch sử trước quyết định hoãn — 2026-10-10

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
  qua prompt ẩn, đã xóa sau setup/xác minh absent; finally vẫn kiểm cleanup.
- TASK-005 IN_REVIEW; TASK-003 IN_REVIEW, TASK-004 DONE. Không lặp setup/bootstrap/
  suite173, không commit/push/deploy/TASK-006.

- 2026-10-10 W10 PASS_HTTP_GOOGLE: backup Active/Admin ba ALL, login và phục hồi primary đạt; runner65 assertion đã quan sát PASS (chưa exit), bốn W13 real-time pending tới10/10 14:44/22:15,11/10 14:15,17/10 14:16 giờVN. Credential UAT/bootstrap absent kiểm độc lập; browser G2a chờ xác nhận.

- G2a (2026-10-10): PASS_MANUAL cho submit rỗng, thiếu tên hiển thị, tạo user
  hợp lệ và username trùng bị chặn/không thêm bản sao. Username người dùng báo:
  uat005_ui_1010, uat005_ui_1010_02. Nguồn người dùng xác nhận thủ công; không
  có exit code. Username sai định dạng và password tạm chỉ một response chưa
  được người dùng xác nhận riêng, không suy diễn PASS. G2b sửa tên/gán Viewer/
  scope/mở lại đang chờ; W13 default idle30ph đạt thật, ba mốc khác pending theo artifact hiện tại.

- G2b (2026-10-10): PASS_MANUAL theo xác nhận người dùng cho
  uat005_ui_1010_02: đổi tên UAT Viewer 1010-02, gán Viewer/Active với
  OU-DEMO-A, LOC-DEMO-A, CAT-DEMO-EQUIPMENT/cấp con; GET mở lại giữ đúng
  tên, role, scope và checkbox. Không có exit code; chưa chứng minh restart
  scope fake. G2c đăng nhập lần đầu/validation đổi password đang chờ.

- Lịch sử G2c: người dùng quên password tạm, đã hướng dẫn reset đúng user;
  bước Reset/Login nay PASS_MANUAL. Không suy diễn PASS cho validation hoặc
  thu hồi phiên cũ trước khi có kết quả riêng.
- W13 default idle30ph PASS_REAL_ELAPSED: runner gửi cookie gốc từ RAM tới
  /Account/ChangePassword lúc 2026-10-10 07:44:57 UTC (14:44:57 giờVN), nhận302
  về /Account/Login; session tạo07:14:50 UTC, idle deadline07:44:50 UTC. Trước
  đó protected GET200, không request giữ idle của fixture này trong30ph. Nguồn
  AI chạy/quan sát HTTP thực, không là browser. Ba mốc8h/24h/7ngày vẫn pending;
  runner PID18964 còn sống, chưa có exit code. Credential tạm đã cleanup.

- Reset/Login (2026-10-10): PASS_MANUAL theo người dùng, đúng
  uat005_ui_1010_02: admin reset, user login bằng password tạm mới và được
  yêu cầu đổi password. Không có exit code. Đã giải quyết việc mất password
  tạm; validation đổi password và login sau đổi vẫn chờ G2c. Chưa có browser
  evidence thu hồi cookie đang hoạt động/reset-old-password/audit riêng W08.

- G2c (2026-10-10): PASS_MANUAL theo người dùng cho cả bước1/2/3, đúng
  uat005_ui_1010_02 sau reset: /Admin/Users trước đổi quay về ChangePassword;
  current sai, new7, confirm khác đều validation/không đổi password; password
  tự chọn8 chữ thường đổi thành công/về login; password tạm trước đổi bị từ
  chối, password mới login được/không bắt đổi lại. Không có exit code. Đây là
  flow sau reset, không phải login bằng password tạo ban đầu đã mất. G2d Viewer
  access denied và các gate revoke/lock/reset/logout/layout còn chờ.

- G2d (2026-10-10): PASS_MANUAL theo người dùng cho uat005_ui_1010_02
  (Viewer): /Admin/Users, /Admin/Authorization, /Admin/SecurityEvents đều
  chuyển AccessDenied/Không có quyền, không lộ dữ liệu quản trị/không về login.
  /Account/ChangePassword vẫn mở được, không cảnh báo bắt buộc đổi. Không có
  exit code. Editor/browser revoke/lock/reset/logout/layout còn chờ G2e trở đi.
