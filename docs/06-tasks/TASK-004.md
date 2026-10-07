# TASK-004 — PoC Sheets/Drive, writer và idempotency

- Status: DONE
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created/updated: 2026-10-07
- Phụ thuộc/gate: TASK-003 vẫn `IN_REVIEW`; không chặn checkpoint TASK-004 theo
  quyết định tách gate ngày 2026-10-06. Live Google và kiểm tra thủ công đã đủ.
- AC liên quan: AC-05, AC-13, AC-15

## Quyết định checkpoint

Người dùng cho phép ngày 2026-10-06 bắt đầu phần local của TASK-004 độc lập với
gate Google resources, hosting và thiết bị còn mở. Quyết định này không chuyển
TASK-003 sang `DONE`.

Ngày 2026-10-07, người dùng thay thiết kế scope rộng bằng least privilege:
`drive.file` là scope duy nhất cho cả Sheets/Drive; không fallback quyền rộng.
Resource mới phải qua provisioning chủ động, resource có sẵn phải qua Picker;
nhập ID hoặc chọn folder cha không được coi là đã cấp quyền file con.

Cùng ngày, người dùng chạy các lệnh live trên Google thật và cung cấp kết quả:

- `google-live-check`: Sheets/Drive cùng `configured=True, live=True`; exit 0.
- `google-live-access-boundary-check`: file được cấp quyền HTTP 200, file control
  HTTP 404; lệnh báo least-privilege check passed. Người dùng không cung cấp exit
  code cho lệnh này.
- `google-poc-init`: initialized or verified; exit 0.
- `google-poc-run DT-DEV-002`: 2 unique writes, idempotent replay,
  stale-version rejection, Drive upload/download checksum và `AVAILABLE`; exit 0.

Đây là bằng chứng do người dùng chạy trên Google thật, không phải lệnh AI chạy
lại hay kết quả từ fake/stub. AI không chạy lại PoC có ghi trong lượt review này.

Cũng trong ngày 2026-10-07, người dùng xác nhận đã kiểm tra trực tiếp trên Google:

- tab `TASK004_POC` tồn tại và có bản ghi tương ứng run `DT-DEV-002`;
- file `task004-DT-DEV-002.txt` nằm đúng thư mục My Drive dev và có owner là tài
  khoản dự kiến;
- file control tồn tại, tài khoản vận hành mở được, chưa được chọn qua Picker và
  không do ứng dụng tạo.

Đây là xác nhận thủ công do người dùng cung cấp, không phải AI trực tiếp quan sát.

## Phạm vi

Kiểm thử đọc/ghi dev, upload/download, single writer, batch nguyên tử counter +
entity PoC + operation + audit, idempotency, version check, retry giới hạn và
reconciliation timeout/restart. Local dùng fault injection/HTTP stub; live Google
có evidence riêng. Không triển khai CRUD tài sản, auth người dùng, import, report
hoặc UI của Task sau.

## Kết quả thực hiện

- [x] Adapter Sheets/Drive dùng OAuth refresh token ở backend và gọi REST Google
  thật; readiness đọc metadata thật khi đủ cấu hình, không tự nhận live.
- [x] Desktop OAuth bootstrap dùng PKCE + loopback
  `http://127.0.0.1:53682/authorize/`; refresh token tự lưu user-secrets, không
  dùng callback web/public và không in token.
- [x] OAuth request chỉ có `drive.file`, không bật `include_granted_scopes`; code
  kiểm token response đúng duy nhất scope này và fail-closed với grant thiếu/rộng.
- [x] Có lệnh provisioning riêng cho spreadsheet/folder mới và flow Picker
  `trigger_onepick` cho resource có sẵn; không lệnh nào tự chạy khi start web.
- [x] Có live access-boundary command: resource đã cấp quyền phải đọc được và
  file control có thật chưa cấp quyền phải bị 403/404.
- [x] Google identity/credential ở backend, tách tài khoản đăng nhập ứng dụng.
- [x] Demo vẫn một backend process; mọi allocation PoC đi qua singleton
  `ISingleWriterCoordinator`.
- [x] Một `spreadsheets.batchUpdate` ghi counter/entity/operation/audit; precheck
  Version và operation ledger bảo vệ stale/replay trong giả định single writer.
- [x] 429 retry hữu hạn; timeout/5xx của write là outcome chưa biết và phải tìm
  Operation/Drive `operationId`, không retry mù hoặc báo success giả.
- [x] File state `PENDING/AVAILABLE/FAILED`; upload gắn appProperties operationId,
  download lại kiểm SHA-256.
- [x] Lệnh local-only cho bootstrap/Picker/provisioning/live read/access boundary,
  init tab và live PoC. Provisioning/init/run được ghi rõ là có ghi Google.
- [x] Live read/write Google với `drive.file` có output người dùng cung cấp như
  phần trên.
- [x] Logger mặc định của hai HttpClient Google đã được thay bằng logger an toàn:
  chỉ tên client/HTTP method, status code, thời gian và loại lỗi; không log URL
  chứa ID, token, Authorization header hoặc request/response body.
- [x] Hướng dẫn dev: [GOOGLE-DEV-SETUP](../GOOGLE-DEV-SETUP.md).
- [x] Người dùng xác nhận thủ công tab `TASK004_POC`/record `DT-DEV-002`, file
  TXT đúng folder/owner dev và file control hợp lệ. AI không trực tiếp quan sát.

## Acceptance criteria của Task

- [x] Local fault tests: hai lệnh đồng thời không trùng; replay/restart không nhân
  bản; key cũ/payload khác và Version cũ bị từ chối.
- [x] Live Google: lệnh `google-poc-run DT-DEV-002` do người dùng chạy báo hai
  write không trùng, replay không nhân bản và stale version bị từ chối; exit 0.
- [x] Local fault tests: upload/download checksum, lỗi Drive → `FAILED`, timeout
  chưa xác nhận giữ `PENDING`/yêu cầu reconcile.
- [x] Live Google: cùng lệnh PoC báo upload/download checksum và state
  `AVAILABLE`; exit 0.
- [x] Local fault tests: retry 429 giới hạn; timeout trước/sau commit không success
  giả; request executor retry safe-read.
- [x] Local request-boundary tests: chỉ `drive.file`, không incremental grant;
  token thiếu/rộng bị từ chối; Picker filter đúng; provisioning chỉ POST khi gọi
  chủ động; HTTP stub mô phỏng file được cấp quyền 200 và chưa cấp quyền 403.
- [x] Live access boundary: command do người dùng chạy nhận 200 cho file granted,
  404 cho file control và báo pass; người dùng xác nhận thủ công file control có
  thật, tài khoản vận hành mở được và file chưa được Picker/app cấp quyền.
- [x] Kiểm tra thủ công resource dev: người dùng xác nhận tab `TASK004_POC` có
  record của run `DT-DEV-002`, file TXT nằm đúng folder và đúng owner dev.

## Routing đã dùng

- `dotnet-contribution:dotnet-backend-patterns`: async/DI/options, ranh giới
  Application/Infrastructure, HTTP client factory và xUnit.
- `developer-essentials:auth-implementation-patterns`: tách integration identity,
  OAuth consent/refresh token và secret backend ở checkpoint trước; không dùng
  skill này để triển khai đăng nhập user của TASK-005.
- `developer-essentials:code-review-excellence`: review theo AC, lỗi, dữ liệu
  nhạy cảm và chất lượng test.
- `skill-forge-essentials:ai-debt-detector`: rà failure path, dependency thật,
  cleanup và architectural drift của phần logging mới.
- Không chạy orchestrator/subagent: Requirement/Plan/Task đã có và phạm vi review
  đủ nhỏ.

## Bằng chứng kiểm tra

Working directory: `D:\Dev\QuanLyTaiSan_DuyTien`.

| Kiểm tra | Kết quả |
|---|---|
| Restore solution sau thay đổi test | exit 0 |
| Build Server | exit 0; 0 warnings; 0 errors |
| ArchitectureTests | exit 0; passed 31; failed 0; skipped 0 |
| Format affected projects | exit 0 cho Server và ArchitectureTests |
| Tài liệu sau đồng bộ `DONE` | 52 local links trong 10 file tài liệu bị ảnh hưởng, 0 hỏng; 14/14 Task khớp status với INDEX |
| Logging canary giả | PASS_LOCAL; client/method/status/duration còn lại, không có resource ID/query/token/Authorization/request body/response body/exception message |
| Concurrency/idempotency/version | PASS_LOCAL; in-memory fault store, không phải Google |
| Retry/reconciliation/file states | PASS_LOCAL; fault injection/HTTP stub, không phải Google |
| Scope/Picker/provisioning/access boundary | PASS_LOCAL; URI/token policy và HTTP stub, không phải Google |
| `google-live-check` | Người dùng chạy trên Google thật: configured/live đều True; exit 0 |
| `google-live-access-boundary-check` | Người dùng chạy trên Google thật: granted 200, control 404, command báo pass; không ghi exit code vì người dùng không cung cấp |
| `google-poc-init` | Người dùng chạy trên Google thật: initialized or verified; exit 0 |
| `google-poc-run DT-DEV-002` | Người dùng chạy trên Google thật: unique/replay/stale/checksum/AVAILABLE pass; exit 0 |
| Kiểm tra thủ công Google UI | PASS_MANUAL_USER_CONFIRMATION; người dùng kiểm tra trực tiếp và xác nhận tab/record, TXT folder/owner và tính hợp lệ file control; AI không trực tiếp quan sát |
| Git | `NOT_A_GIT_WORKTREE`; không init/commit/push |

Chi tiết case: [TEST-REPORT-TASK-004](../07-testing/TEST-REPORT-TASK-004.md).

## Review và giới hạn

- HttpClient factory gọi `.RemoveAllLoggers()` trước khi gắn logger an toàn cho
  cả `GoogleOAuth` và `GoogleApi`. Test chạy ở log level Trace để canary URL,
  resource ID, query, bearer token, hai body và exception message sẽ làm test
  fail nếu logger mặc định hoặc logger mới làm lộ dữ liệu.
- Logger vẫn giữ tên client, method, HTTP status, elapsed milliseconds và failure
  type để chẩn đoán; không truyền exception object vào log.
- Bằng chứng live và xác nhận Google UI ở trên đều do người dùng cung cấp; AI
  không có transcript/tab Google và không tự gán exit code cho access-boundary.
- HTTP 404 được dùng cùng xác nhận thủ công rằng file control có thật, tài khoản
  vận hành mở được, chưa qua Picker và không do app tạo; không suy diễn từ 404
  đứng riêng.
- Lock RAM chỉ đúng một process; không có distributed lock. Sheets không có CAS;
  second writer/chỉnh tay giữa read và batch nằm ngoài giả định và phải dừng writer.
- Batch Sheets giữ atomicity trong một spreadsheet. Drive + Sheets không có
  transaction chung nên file state/reconciliation vẫn bắt buộc.
- Self-review theo AC và AI-debt checklist; không có independent reviewer/subagent.
  Build, test và format phần bị ảnh hưởng đều pass.
- `dotnet format` toàn solution ở checkpoint trước exit 1 vì whitespace trong
  các file MAUI ngoài phạm vi; không tự sửa mobile.

## Checkpoint để tiếp tục

TASK-004 chuyển `IN_REVIEW` → `DONE`: local build/31 tests, live Google do người
dùng chạy và ba kiểm tra Google UI thủ công đều đạt. Xác nhận UI là báo cáo của
người dùng, không phải AI trực tiếp quan sát. Không chạy lại PoC có ghi.

TASK-003 tiếp tục `IN_REVIEW`; hosting/domain và thiết bị/signing vẫn mở.
Tại checkpoint đóng TASK-004, TASK-005 còn `TODO`. Trạng thái hiện tại của
TASK-005 xem [INDEX](INDEX.md); đoạn này chỉ giữ lịch sử thời điểm TASK-004.
