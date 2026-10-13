# Kế hoạch nghiệm thu

## Ngoại lệ đã được người dùng cho phép — 2026-10-10

Người dùng dừng nghiệm thu TASK-005 và cho phép triển khai local TASK-006 dù TASK-005 chưa DONE (2026-10-10).
TASK-005 giữ `IN_REVIEW`: G2a/G2b PASS_MANUAL; G2c và browser còn mở DEFERRED_BY_USER; idle 30 phút thực PASS; W13 8h/24h/7d STOPPED_BY_USER. PID 18964 đã dừng đúng runner, credential UAT được AI xác minh không còn; giữ tài khoản/audit/bằng chứng lịch sử. Không chờ 17/10, không tiếp tục giao UAT hoặc yêu cầu secret. TASK-003 IN_REVIEW, TASK-004 DONE.


Không có test ứng dụng đã chạy trong phiên soạn này. Các case sau sẽ thực hiện theo Task/code/môi trường đã tạo.

## Case bắt buộc
1. Import cây → 665 nodes / 15+98+552; report 39 tên nhiều mã; lần hai không nhân đôi; changed workbook preview.
2. Admin bootstrap login đúng cấu hình, restart không reset, hash không plaintext; user-editor-viewer các chức năng thực tế.
3. Viewer gửi POST/PATCH/DELETE trực tiếp; user scoped truy cập Id/fileId/report ngoài phạm vi; bị chặn, không lộ tổng hay data.
4. 2 concurrent creates, retry cùng key, reuse key payload khác, timeout sau commit và restart; unique code và một entity.
5. Change classification/root/location/name, soft-delete/restore; ID/code không đổi hoặc tái cấp.
6. Category move cây con, cycle/F11, bulk delete có impact, scope nguồn/đích, export TXT Unicode.
7. Import MISA duplicate CĐKEM, duplicate header Mô tả, textcodes, chunk failure/resume, zero vs missing quantity.
8. R03/R04 quantities=0, below/equal/above min, missing min, missing qty, khác unit, service/nonstock, scope restricted.
9. Upload image/type spoof/oversize, Drive permission revoked, upload success/Sheet fail, retry; reconciliation không mất file hay báo success giả.
10. DOCX/PDF version pair đúng, missing counterpart, viewer unauthorized file, backend stream headers/content.
11. Android chụp thật/chọn file/từ chối camera/mất mạng/revoke/session/version conflict; iOS cùng case và evidence riêng.
12. Quota429/backoff/timeouts/cache invalidation, Google offline, administrative maintenance, no 2 writers.
13. Backup/restore trên dev khác đích; kiểm code/counter/scope/files/meta; không khôi phục ngược vào dữ liệu production để thử.

## Chi tiết TASK-005 — `ACCEPTED`, local pass; live hoàn thành một phần

Fixture dưới đây hoàn toàn giả, không mô tả cơ cấu hoặc tài khoản Công ty Duy
Tiến. Password/token được sinh trong test runtime, không ghi vào tài liệu.

| Nhóm | Fixture/case chính | Expected |
|---|---|---|
| Scope | `OU-DEMO-A/A-CHILD/B`, `LOC-DEMO-A/A-CHILD/B` có `ParentId`, `CAT-DEMO-EQUIPMENT/CHILD/VEHICLE` | Validate ParentId/no-cycle; exact, descendant và sibling được phân biệt; thiếu scope fail-closed |
| Role | Viewer, AssetEditor, CatalogManager, DocumentManager, Admin | Permission đúng [ma trận](../01-requirements/PERMISSIONS.md); Viewer không write; editor không quản lý user |
| Nhiều role | AssetEditor scope A + DocumentManager scope B | Permission union nhưng không ghép AssetEditor với scope B |
| Bootstrap | System store trống, chạy bootstrap hai lần và restart | Tạo `admin` một lần; lần sau no-op; hash/role/scope không reset |
| Password | Chuỗi 7/8 ký tự, chuỗi đơn giản/username, first login, reset, change, 5 lần sai | Chỉ min 8; không complexity/max/blocklist/username rule; must-change hạn chế quyền; reset/change revoke; lần 5 khóa 15 phút |
| Web session | Cookie mặc định/remember-me, CSRF, idle/absolute expiry | Flags đúng; state-changing request thiếu token bị chặn; expiry đúng proposal |
| Mobile session | Access 15 phút, refresh rotate, reuse token cũ | Reuse revoke family; secure token không xuất hiện log/response sai |
| Revoke | Lock user, đổi role/scope, logout/logout-all | Cookie/access/refresh cũ mất hiệu lực qua `SecurityVersion` |
| Last admin | Assignment active/revoked/chưa hiệu lực/hết hạn và ALL đủ/thiếu | Chỉ assignment Admin hiệu lực có đủ ba ALL mới được tính; chặn mutation làm admin về 0 |
| Writer lease | Web/CLI và hai CLI dùng cùng lock file | Chỉ một process giữ lease; lock RAM không được dùng làm bằng chứng liên tiến trình |
| Scope leak | Detail/file ngoài scope; list/report/export trộn A/B | Ngoài scope 404; list/count/aggregate chỉ gồm row được phép |
| Razor Pages | Login, change password, users, authorization, security events | Validation/empty/access denied; dùng cùng service với API |

Local unit/integration dùng fake system store và HTTP stub; 57/57 test pass tại
[TEST-REPORT-TASK-005](TEST-REPORT-TASK-005.md). Trước khi TASK-005 `DONE`, phải
có test live trên system spreadsheet dev được cấp quyền qua provisioning/Picker;
lượt local này không tạo hoặc ghi Google.

## Fixture và evidence
Unit tests dùng dữ liệu giả; integration fake adapter có fault injection không thay live Google test. Dữ liệu nguồn đã cung cấp dùng kiểm import trong phạm vi task, không upload lên dịch vụ khác ngoài đích đã xác lập.
Report Task ghi build/test counts và platform/device/sdk/schema/source hash. Mã hoạt động mà chưa có evidence cần IN_REVIEW, không DONE.
UAT bản demo bởi người dùng: login, tree, asset lifecycle, ảnh, template, reports và phạm vi thực. Từng bước có expected/actual và lỗi còn lại.

## Tiến độ TASK-005 ngày 2026-10-09

Google DEV provisioning/owner-only ACL/init schema đã được AI chạy/quan sát;
local build/58 tests/HTTP preflight 12/12 pass. HTTP chỉ kiểm tra Razor chưa
đăng nhập, không chứng minh tương tác/layout hay cookie login. Browser runtime
không có browser khả dụng. Bootstrap/no-op đã đạt trên Google (hai CLI exit 0,
snapshot giữ nguyên); người dùng xác nhận đổi mật khẩu bắt buộc và login lại.

Restart, tạo/gán quyền tài khoản giả, Viewer/revoke/last-admin và mobile rotation/
reuse đang tiếp tục. Runner lần đầu đạt 9 assertion admin API/Razor login/cookie
flags/default persistence rồi exit 1 do stdin EOF ở checkpoint công cụ; credential
tạm đã xóa. Checkpoint đổi sang marker temp. Không coi toàn suite PASS. [Checklist chi tiết](UAT-CHECKLIST-TASK-005.md)
có expected/actual/source cho từng case. UI manual mới chứng minh valid admin
forced-change/login flow; validation/layout/quản trị còn cần nghiệm thu. Scope descendant/nhiều assignment/move giữ bằng chứng core local;
không tạo endpoint nghiệp vụ TASK-006 để thử. TASK-005 giữ IN_REVIEW.

Checkpoint lịch sử trước restart: local sửa Razor scope, build sạch/61 tests pass; Google bootstrap/no-op và manual forced-change/login đạt. Runner checkpoint đã sửa, credential tạm đã xóa, chờ nhập lại; backend đã dừng. Các live/quyền/restart/token/audit/manual UI gate chưa đầy đủ, TASK-005 IN_REVIEW.


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

## Checkpoint UAT hiện tại — 2026-10-10

G1a/G1b PASS do người dùng xác nhận thủ công. W10 PASS_HTTP_GOOGLE: fake backup
Active/Admin cùng assignment ba ALL hiệu lực, login API/Razor HTTP và phục hồi
primary đã quan sát. Baseline78/live173 không chạy lại. Thêm14 test boundary
TimeProvider PASS_LOCAL exit0, không đổi timeout; không thay bằng elapsed live.

Runner W10/W13 riêng PID18964:65 assertion quan sát PASS, chưa exit; credential
UAT xóa ngay sau setup và probe độc lập booleanFalse (bootstrap cũngFalse).
Bốn W13 thời gian thật PENDING đến10/10 14:44/22:15,11/10 14:15 và17/10 14:16
giờVN; cookie chỉ RAM, không rút ngắn policy. G2 browser còn chờ, W13 chưa PASS.
Xem UAT-CHECKLIST-TASK-005 và TASK-005-REMAINING-LIVE-EVIDENCE.json. Giữ TASK-005
IN_REVIEW, TASK-003 IN_REVIEW, TASK-004 DONE; không deploy/commit/push/TASK-006.

Checkpoint bổ sung2026-10-10: W13 defaultidle30ph PASS_REAL_ELAPSED HTTP302 login
lúc14:44:57 giờVN; 8h/24h/7ngày vẫn pending. G2a bốn bước và G2b PASS_MANUAL;
G2c chưa chạy vì mất password tạm, hướng dẫn reset đúng user giả qua browser.
Chưa xác nhận reset thành công; TASK-005 vẫn IN_REVIEW.

## AC-01/AC-02/AC-07 — checkpoint TASK-006 local 2026-10-10

46/46 test task +12/12 hồi quy liên quan pass, AI local exit0; source staging đúng665/15/98/552/39, metadata/parser/validation/diff/idempotency/atomic/scope/TXT/API/rendered Razor/antiforgery có test. Fake Google và render HTML không thay live/browser. Workbook gốc metadata/hash NOT_RUN_SOURCE_ORIGINAL; Google schema/import/restart NOT_RUN_GOOGLE; browser tương tác/layout/loading NOT_RUN_BROWSER. TASK-006 IN_REVIEW; chi tiết TEST-REPORT-TASK-006. Không giao thao tác thủ công trong lượt theo quyết định người dùng.

## AC-05/AC-06/AC-08 — TASK-007 local 2026-10-13

Ngoại lệ người dùng cho phép local khi TASK-006 IN_REVIEW; TASK-005 hoãn giữ nguyên. 54/54 task +9/9 regression local exit0, build sạch0warnings/errors, fake persistence/Google HTTP stub và Kestrel/Razor HTTP. AC-05 concurrency/key/reconcile, AC-06 ID/code/lifecycle/alias và AC-08 fields/version/permission/form HTTP có local evidence tại TEST-REPORT-TASK-007. Actual browser UAT/layout/loading, Google resource/schema/CRUD/restart/quota và rule/fields/Org-Location source chưa đạt; không đánh dấu PASS môi trường thật. TASK-007 IN_REVIEW; không giao manual checklist/secret.

## AC-06/AC-07 — TASK-008 local 2026-10-13

Ngoại lệ người dùng cho local dù TASK-005/006/007 IN_REVIEW; hoãn cũ giữ nguyên.42/42 task và91/91 regression local exit0, build sạch0warning/error: IDs/code/mapping, subtree cycle/F10/F11, preview/plan/confirm, same-assignment source/target, partial-tree prevention, import bypass guard, atomic joint fake Google/reconciliation/digest, TXT Unicode và Kestrel compiled Razor HTTP. Synthetic665-node không thay workbook/baseline công ty. Google real resource/schema/write/restart/quota NOT_RUN_GOOGLE; browser interaction/layout/loading NOT_RUN_BROWSER; rule/fields/source Org-Location OPEN/TBD, workbook gốc chưa xác minh. [Report](TEST-REPORT-TASK-008.md) và [artifact](TASK-008-LOCAL-EVIDENCE.json) ghi expected/actual/source/lệnh/exit và lịch sử failures. TASK-008 IN_REVIEW, không DONE và không giao UAT thủ công trong lượt.

## AC-10/AC-13 — TASK-009 local 2026-10-13

Ngoại lệ người dùng cho local dù TASK-005–008 IN_REVIEW, giữ nghiệm thu hoãn.56/56 task+67/67 regression exit0; build0warning/error. Exact DOCX/PDF same-version gateway/pair missing/published demo/future date/current owner scope, type/byte/CSRF/413/409/503, pending before Drive/same-ID retry/reconcile, resumable large/lost/partial/foreign URI and guard metadata có PASS_LOCAL/STUB/HTTP ở TEST-REPORT-TASK-009. Compiled Razor/JS syntax không browser UAT; fake transport không Google live. Source mẫu/Drive target/actual ACL/quota/restart/retention/source rules còn NOT_RUN/OPEN_TBD; không đóng AC môi trường thật. TASK-009 IN_REVIEW, task trước/DEFERRED_BY_USER giữ nguyên, không giao manual UAT mới.
