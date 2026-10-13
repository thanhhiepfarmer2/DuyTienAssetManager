# TASK-012 — App Android: nhập liệu và chụp ảnh

- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created:2026-10-06; local checkpoint2026-10-13
- Phụ thuộc: TASK-005/007/009 còn IN_REVIEW; người dùng cho phép local bắt đầu, không đóng gate trước.
- AC liên quan: AC-04, AC-08, AC-09

## Phạm vi / kết quả local

Project MAUI hiện có, Android .NET10: login/đổi mật khẩu bắt buộc/min8/SecureStorage/refresh rotation/logout/revoke; Asset search/filter/paging/total/asOf/detail/create/edit DRAFT_DEMO; camera/pick/preview/upload trạng thái/retry/reconcile và lỗi quyền/mạng/service/cancel. Form/version/tree/key và ảnh/metadata giữ trong RAM khi chưa xác nhận, retry cùng operation không duplicate. 409 nạp version mới rõ ràng, không auto overwrite. HTTPS backend cấu hình theo môi trường, bearer header/no cookie/no redirect/no certificate bypass; không Google credential/direct calls.

Tái sử dụng DTO/validation TASK-007 và API TASK-005/009. Scope/permission/version/idempotency/single-writer/metadata guard server giữ; không rule/approval/offline DB/task ngoài phạm vi. Rule/nguồn/OrgLocation vẫn OPEN/TBD, fixtures GIẢ. [Thiết kế](../02-architecture/MOBILE-ANDROID-DESIGN-TASK-012.md).

## Acceptance criteria / gate

- [x] Build Android local net10.0-android: exit0,0warning/error; SDK10.0.400, MAUI10.0.20/10.0.100, Android36/JDK21.
- [x] Local client/contract/biên:42/42pass,0failed/skipped,exit0; validation shared/backend và409, scope, rotation/revoke, retry idempotent không duplicate qua backend thật/store giả.
- [ ] Cài/chạy trên Android ghi rõ device/OS/UI tương tác: NOT_RUN, adb devices rỗng.
- [ ] Chụp ảnh thật, picker/cancel/từ chối quyền OS, network/retry/SecureStorage trên Android thực: NOT_RUN; fake workflow không thay runtime.
- [ ] File ảnh có Drive metadata/đúng Google resources/ACL/drive.file thật: NOT_RUN_GOOGLE.

## Ngoại lệ phụ thuộc / bằng chứng

Người dùng cho phép local dù TASK-005/007/009 IN_REVIEW; TASK-003 IN_REVIEW, TASK-004 DONE, TASK-005–011 IN_REVIEW/gates hoãn/DEFERRED_BY_USER/STOPPED_BY_USER giữ. Giữ56file TASK-010/011 chưa commit đầu lượt, mainHEAD463896d không đổi; không stage/commit/push/deploy/Google live/real writes/secret/thiết bị/UAT mới/TASK-013.

AI chạy/quan sát trực tiếp; không user-run/manual evidence mới. [Test Report](../07-testing/TEST-REPORT-TASK-012.md), [evidence](../07-testing/TASK-012-LOCAL-EVIDENCE.json), [commands](../COMMANDS.md). Build/test/format tuần tự. Lỗi restore TFM/test JSON/test method đã sửa, lịch sử failure giữ trong report. Self-review dùng .NET patterns/interaction-design/ai-debt-detector; không subagent/independent audit.

## Review / bước tiếp theo

Hoàn tất local, IN_REVIEW vì acceptance device/camera/Google chưa đủ. Draft/photo RAM không offline/crash-resume; logout/process chết mất RAM, backend giữ operation để đối soát, app không tự gửi bản mới. HTTP compiled workflow không chứng minh Android UI/TLS/OS camera/storage. Khi có phạm vi runtime, bổ sung bằng chứng vào Task này; không yêu cầu UAT/thiết bị ngay hoặc triển khai TASK-013.

## Lịch thực hiện — 2026-10-14

Execution: **DEFERRED_BY_USER** cho phát triển và runtime UAT Android tới sau pha Web. Status IN_REVIEW/local42tests/build và bằng chứng13/10 nguyên vẹn; không build/code/mobile UAT mới. Gate mobile không chặn Web.
