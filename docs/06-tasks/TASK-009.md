# TASK-009 — Drive ảnh và biểu mẫu DOCX/PDF

## Ngoại lệ phụ thuộc TASK-009 — 2026-10-13

Người dùng cho phép hoàn tất phần local TASK-009 dù TASK-005/006/007/008 IN_REVIEW; không đóng gate trước, giữ DEFERRED_BY_USER. Không Google live/UAT thủ công/dữ liệu thật/commit/push/deploy/TASK-010. Guard metadata TASK-008 giữ nguyên, không bypass; tab Files/Documents làm tree mutation fail-closed cho đến khi có thiết kế đối soát module được giao riêng. Rule/workbook/Org-Location OPEN/TBD; file mẫu thật và Drive target chưa xác minh, fixture giả chỉ cho local.


- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-007; file mẫu và Drive target
- AC liên quan: AC-10, AC-13

## Phạm vi
Upload ảnh web, Files states, doc registry/versions, cặp DOCX+PDF chuẩn cùng version, gateway kiểm quyền, failure/reconcile/retention.

## Acceptance criteria của Task

| AC | Expected / Actual local | Gate còn mở |
|---|---|---|
| Download đúng file/version, thiếu counterpart báo rõ | PASS_LOCAL: exact bytes/slot/version; thiếu cặp409, old version giữ và download theo version | Google/browser/mẫu nguồn chưa xác minh |
| User ngoài scope bị chặn dù biết file ID | PASS_LOCAL_HTTP/STUB: current owner/slot/read/action/scopes, outside404 trước Drive IO, Viewer write403, CSRF/parent binding | Google/browser chưa chạy |
| Drive thành công+Sheet lỗi có operation; giữ file gốc | PASS_LOCAL_STUB: pending durable trước create, same DriveID retry/reconcile/lostresponse/restart reconstruction một file; soft-delete/retention | Google thật/quota/restart chưa chạy |

## Phần local hoàn tất

- [x] Registry metadata DRAFT_DEMO, version DRAFT/PUBLISHED_DEMO, same-version DOCX/PDF, explicit confirm/effective UTC date, immutable published/old files.
- [x] Ảnh PNG/JPEG web/preview/description/primary/soft-delete; cancel/reconcile PENDING/FAILED, type/byte/ZIP/XML validation.
- [x] Sheets registry atomic/journal/audit/integrity; Drive pre-generatedID, small multipart/large resumable/status/range, bounded checksum gateway, failure/retry.
- [x] API bearer và cookie Razor gateway, scope server/dynamic owner/CSRF/version/idempotency; UI per-form keys và input/version retention.
- [x] Build sạch;56/56 tests TASK-009 +67/67 hồi quy exit0, failed/skipped0; JS/diff0; schema/API/UI/config/context đồng bộ.
- [ ] Google live/browser/file mẫu và Drive target: NOT_RUN/NOT_VERIFIED; rule/workbook/Org-Location và retention policy OPEN/TBD. Không DONE.

## Routing / thiết kế

[Thiết kế](../02-architecture/MEDIA-DESIGN-TASK-009.md), [Test Report](../07-testing/TEST-REPORT-TASK-009.md), [artifact sạch](../07-testing/TASK-009-LOCAL-EVIDENCE.json), lệnh tại COMMANDS. Tái sử dụng skill .NET/backend/UI đã đọc và error-handling-patterns cho failure-state, ai-debt-detector/session guard cho self-review; không orchestration/subagent mới. Dữ liệu/file fixture tổng hợp, không mẫu công ty.

## Bằng chứng kiểm tra — 2026-10-13

AI chạy/quan sát trực tiếp tại D:\Dev\QuanLyTaiSan_DuyTien: build server exit0/0warnings/0errors; filter ManagedMedia56passed/0failed/0skipped exit0; hồi quy67/67 exit0; node syntax và diff exit0; format từng project exit0. Report giữ compiler/startup failures và checkpoint8/37/44/48/51/55/56, không suy diễn Google/browser. Không user-run/manual confirmation mới.

## Checkpoint / Review

TASK-009 IN_REVIEW, local hoàn tất, chưa Google/browser/source. Không phê duyệt DRAFT/rule công ty hoặc chuyển task trước DONE. Đã sửa binding DELETE, resumable>5MiB, per-form key và stale-version retry; tự review theo AC/scope/failure/retention, không independent review.

Guard metadata TASK-008 giữ nguyên ở read và commit. Sau registry mới, tree mutation còn fail-closed vì chưa đối soát module mới; không rename tab/resource hoặc bypass để tiếp tục. Không init/bootstrap/provisioning/live UAT, secret UAT, SQL/SQLite, data thật, deploy/commit/push/TASK-010. TASK-005 hoãn giữ DEFERRED_BY_USER; TASK-005/006/007/008 IN_REVIEW, TASK-003 IN_REVIEW, TASK-004 DONE. Chỉ đóng gate nguồn/Drive/live/browser khi được phép, không giao thêm UAT thủ công.
