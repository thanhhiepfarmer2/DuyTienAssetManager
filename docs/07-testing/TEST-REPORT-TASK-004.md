# TEST-REPORT — TASK-004

- Ngày / môi trường: 2026-10-07; local Windows/.NET SDK 10.0.400; Google thật
  theo kết quả lệnh do người dùng chạy.
- Phạm vi: fault/request-boundary tests local cho AC-05, AC-13, AC-15; live
  read/write/access-boundary Google; kiểm tra chống lộ dữ liệu trong HttpClient log.
- Dữ liệu local: chuỗi canary giả trong memory/HTTP stub; không dùng credential
  hoặc dữ liệu doanh nghiệp.
- Dữ liệu live: không ghi ID/token trong report. AI không chạy lại PoC có ghi.
- Kiểm tra Google UI: người dùng xác nhận thủ công; AI không trực tiếp quan sát.

| AC | Loại test | Case / lệnh | Expected | Actual | Kết quả | Evidence |
|---|---|---|---|---|---|---|
| AC-05 | Local integration/fault | Hai command đồng thời qua single writer | Hai code khác nhau; đủ entity/operation/audit | Đạt | PASS_LOCAL | `PocAssetWriteServiceTests.ConcurrentCommands_AreSerializedAndReceiveUniqueCodes` |
| AC-05 | Local integration/fault | Replay cùng key sau service restart; key cũ/payload khác | Không duplicate; payload khác bị từ chối | Đạt | PASS_LOCAL | 2 test idempotency |
| AC-05 | Local integration/fault | Expected counter version cũ | Từ chối trước commit | Đạt | PASS_LOCAL | `StaleExpectedVersion_IsRejectedBeforeCommit` |
| AC-13 | Local fault | 429 tương đương trước commit | Retry hữu hạn; lần 3 vẫn lỗi thì không ghi | Đạt | PASS_LOCAL | 2 retry tests + HTTP executor 429 test |
| AC-13 | Local fault | Timeout sau commit / trước commit | Sau commit đối soát; chưa commit không báo success | Đạt | PASS_LOCAL | 2 reconciliation tests |
| AC-13 | Local HTTP stub | Adapter gọi endpoint Sheets/Drive có Bearer token | Chỉ báo live khi metadata thật từ API hợp lệ | Đạt với stub | PASS_LOCAL_ONLY | `GoogleAdapterHttpTests` — không phải live Google |
| AC-13 | Local integration/fault | Upload/download, lỗi rõ, timeout Drive | `AVAILABLE` + checksum; lỗi → `FAILED`; unknown → `PENDING`/reconcile | Đạt | PASS_LOCAL | 4 file tests |
| AC-15 | Local request boundary | Build authorization URI và parse token response | Chỉ `drive.file`, không incremental grant; thiếu/rộng bị từ chối | Đạt | PASS_LOCAL_ONLY | `GoogleOAuthAuthorizationRequestTests`, `GoogleOAuthTokenProviderTests` |
| AC-15 | Local HTTP stub | Truy cập `granted-file` và `ungranted-file` | File granted 200; file ungranted 403 | Đạt với stub | PASS_LOCAL_ONLY | `DriveFileAccess_GrantedFileSucceedsAndUngrantedFileIsDenied` |
| AC-15 | Local HTTP stub | Gọi provisioning spreadsheet/folder | Chỉ POST create khi lệnh chủ động gọi; nhận ID tạo mới | Đạt với stub | PASS_LOCAL_ONLY | `GoogleWorkspaceProvisionerTests` |
| Task review | Local logging canary | Named HttpClient ở log level Trace; URL có ID/query, bearer token, hai body và exception message giả | Chỉ client/method, status, duration, failure type; không lộ canary | Đạt | PASS_LOCAL | `SafeGoogleHttpClientLoggingTests` (2 tests) |
| AC-13/15 | Google live, người dùng chạy | `google-live-check` | Sheets/Drive configured/live True | Đạt; exit 0 | PASS_LIVE_USER_RUN | Output người dùng cung cấp; AI không chạy lại |
| AC-15 | Google live, người dùng chạy | `google-live-access-boundary-check` | File granted 200; file control có thật chưa cấp quyền bị 403/404 | Granted 200, control 404; command báo pass; người dùng xác nhận control file hợp lệ | PASS_LIVE_USER_RUN_AND_MANUAL | Không ghi exit code vì người dùng không cung cấp; AI không trực tiếp quan sát |
| AC-13 | Google live, người dùng chạy | `google-poc-init` | Tạo hoặc xác minh tab PoC | Initialized or verified; exit 0 | PASS_LIVE_USER_RUN | Output người dùng cung cấp; chưa quan sát Google UI |
| AC-05/13 | Google live, người dùng chạy | `google-poc-run DT-DEV-002` | Hai write unique, replay idempotent, stale reject, file checksum/AVAILABLE | Đạt; exit 0 | PASS_LIVE_USER_RUN | Output người dùng cung cấp; chưa quan sát Google UI |
| AC-13/15 | Google UI thủ công | Tab/records, TXT đúng folder/owner; control file tồn tại, mở được, chưa qua Picker và không do app tạo | Xác nhận trực tiếp | Người dùng xác nhận đạt đủ ba nội dung | PASS_MANUAL_USER_CONFIRMATION | Xác nhận thủ công của người dùng; AI không trực tiếp quan sát |

## Tóm tắt thực tế

- Restore solution sau thay đổi test: exit 0.
- Build Server: exit 0; 0 warnings; 0 errors.
- ArchitectureTests: exit 0; total 31, passed 31, failed 0, skipped 0.
- `dotnet format --verify-no-changes`: exit 0 cho Server và ArchitectureTests.
  Chạy toàn solution ở checkpoint trước exit 1 do whitespace trong các file MAUI
  ngoài phạm vi; không tự sửa mobile.
- Kiểm tra tài liệu sau đồng bộ `DONE`: 52 local links trong 10 file tài liệu bị
  ảnh hưởng, 0 hỏng; 14/14 Task khớp trạng thái với INDEX.
- Hai named client `GoogleOAuth` và `GoogleApi` bỏ default HttpClient logger rồi
  gắn logger chỉ ghi client/method/status/duration/failure type. Test Trace-level xác
  nhận không có URL/resource ID, query, token/Authorization, request body,
  response body hoặc exception message giả.
- Live Google là bằng chứng người dùng chạy trên Google thật: readiness exit 0,
  init exit 0 và PoC run exit 0. Access-boundary command báo pass với 200/404;
  exit code của riêng lệnh này không được cung cấp nên không được suy diễn.
- Người dùng xác nhận trực tiếp tab `TASK004_POC` có record `DT-DEV-002`, file
  `task004-DT-DEV-002.txt` đúng folder/owner dev và file control hợp lệ. Đây là
  xác nhận thủ công của người dùng, không phải AI quan sát qua browser/tab.
- Không chạy lại lệnh PoC có ghi, không đổi sharing, deploy, commit hoặc push.

## Giới hạn bằng chứng

- AI không trực tiếp quan sát Google UI; ba kiểm tra thủ công được chấp nhận theo
  xác nhận rõ ràng của người dùng. Không lưu ID, token hay ảnh chứa dữ liệu thật.
- Exit code của riêng lệnh access-boundary không được cung cấp; report chỉ ghi
  command báo pass, HTTP 200/404 và xác nhận thủ công về file control.
- Không chủ động tạo 429/timeout trên Google thật; failure injection local đã pass.
- Lock chỉ trong một process. Không có bằng chứng an toàn nhiều process/replica và
  không được scale-out.
- Version check ngăn stale write trong single-writer flow, nhưng Sheets không có
  CAS; chỉnh tay/second writer giữa read và batch vẫn ngoài giả định demo.

## Quyết định

TASK-004 đủ điều kiện `DONE`: local test/build pass, live Google do người dùng
chạy pass và các gate UI thủ công đã được người dùng xác nhận. TASK-003 vẫn
`IN_REVIEW`; hosting/domain và thiết bị/signing còn mở.
