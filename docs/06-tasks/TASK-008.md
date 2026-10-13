# TASK-008 — Sửa/chuyển/ngừng cây và di chuyển đối tượng

## Ngoại lệ phụ thuộc TASK-008 — 2026-10-13

Người dùng cho phép hoàn tất phần local TASK-008 dù TASK-005/006/007 còn IN_REVIEW. Các gate hoãn TASK-005 và nguồn/workbook/Google/browser TASK-006/007 giữ nguyên; rule mã/fields/Org-Location công ty OPEN/TBD. Ngoại lệ không phê duyệt DRAFT, không cho ghi dữ liệu thật/commit/push/deploy/TASK-009. Publish kỹ thuật là đổi pointer nguyên tử sang snapshot DRAFT đầy đủ, không công bố mã chính thức.


- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-007; ngoại lệ người dùng 2026-10-13 cho local khi TASK-005/006/007 IN_REVIEW
- AC liên quan: AC-06, AC-07

## Phạm vi
Preview bulk, chuyển cây con, cập nhật full path/tree version, scopes hai phía, chặn cycle/F10; xóa rỗng hoặc relocate/archive có xác nhận tác động.

## Acceptance criteria của Task

| AC | Local expected / actual | Gate còn mở |
|---|---|---|
| ID node/assets không đổi khi move | PASS_LOCAL: edit/move/bulk/relocate/soft-delete, alias/mapping/counter/code giữ; scoped history | Google live/browser/source/rule chưa đủ |
| Cycle/F11 và xóa nhánh đang dùng không plan bị chặn | PASS_LOCAL: F10 hợp lệ; cycle/F11/no-op/plan/confirm/permission hai phía bị kiểm ở server | Google/browser chưa chạy |
| Publish complete tree, TXT/report theo tree mới | PASS_LOCAL_STUB/HTTP: full snapshot+pointer+Asset batch atomic, failures/retry, Unicode/indent/new path và scoped impact/report | Google thật/browser chưa chạy; không PASS từ stub |

## Phần local đã thực hiện

- [x] Backend preview/commit stateless cho sửa tên/mã nháp/definition, move subtree, archive EMPTY_ONLY/RELOCATE/SOFT_DELETE và bulk MOVE_ASSETS.
- [x] Validation, current auth/scope nguồn/đích cùng assignment, count/path/history scoped, stale409 và durable retry ledger.
- [x] Google joint adapter một atomic batch, schema guard/digest, per-row diff/audit, shared single-writer readers và import bypass guard.
- [x] Razor /Categories/Manage/{id?}, /Assets/Move/{id?}, history và TXT theo snapshot; CSRF/error/input retention/preview/confirm/DRAFT.
- [x] Build sạch;42/42 task,91/91 hồi quy pass; JS syntax và diff kiểm thực tế.
- [ ] Google live, browser thật và source/rule công ty: NOT_RUN/OPEN/TBD, không chuyển DONE.

## Routing và thiết kế

Tái sử dụng .NET backend/UI interaction, self-review ai-debt-detector và session-guard đã đọc; không subagent/orchestrator mới. [Thiết kế](../02-architecture/TREE-MUTATION-DESIGN-TASK-008.md), [report](../07-testing/TEST-REPORT-TASK-008.md), [artifact](../07-testing/TASK-008-LOCAL-EVIDENCE.json), lệnh tại COMMANDS.

## Bằng chứng kiểm tra — 2026-10-13

AI chạy/quan sát trực tiếp ở D:\Dev\QuanLyTaiSan_DuyTien: build server exit0/0warnings/0errors; filter TreeMutation42passed/0failed/0skipped exit0; hồi quy Category/Asset/scope91/91 exit0; node syntax/diff check exit0; format file liên quan từng project exit0. Fake persistence/Google HTTP stub và Kestrel compiled Razor HTTP không chứng minh Google live/browser UAT. Report giữ cả lần28test có2failure assertion và các checkpoint17/34/35/37/40/42, không suy diễn PASS.

## Checkpoint / Review

Phần local đầy đủ, TASK-008 IN_REVIEW. Self-review theo AC, scope/data state, snapshot atomic/retry và failure retention; không có independent review mới. Đã sửa import bỏ qua move, diff vượt cell và mất ID form khi read lỗi. Giữ DRAFT/DRAFT_INACTIVE/DRAFT_DEMO, rule/fields/source Org-Location OPEN/TBD. Không sửa password min8/session policy, init/bootstrap/provision, ghi Google/dữ liệu thật hoặc làm TASK-009; không commit/push/deploy.

TASK-003 IN_REVIEW, TASK-004 DONE, TASK-005/006/007 IN_REVIEW; hoãn cũ DEFERRED_BY_USER và bằng chứng lịch sử nguyên vẹn. Bước tiếp theo trong phạm vi task là giải quyết Google/browser và source/rule gates khi được cho phép; không tự giao UAT thủ công hoặc bắt đầu TASK-009.

## Tích hợp chéo pha Web14/10/2026

Status IN_REVIEW/Google/browser/sourcegates vàlịch sửgiữ. Guardđãđược mởrộngbằng đốisoátfullregistry trongTASK-014Web, khôngbypasshoặcgiảempty: [thiết kế](../02-architecture/WEB-COMPLETION-DESIGN-TASK-014.md), [evidence](../07-testing/TEST-REPORT-TASK-014-WEB.md). KhôngchuyểnTasknàyDONEtừlocalchecks mới.
