# TASK-007 — CRUD tài sản trên web

- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-006; required fields từng loại và rule code demo
- AC liên quan: AC-05, AC-06, AC-08

## Ngoại lệ phụ thuộc TASK-007 — 2026-10-13

Người dùng cho phép triển khai đầy đủ phần local TASK-007 dù TASK-006 chưa DONE. TASK-005 giữ IN_REVIEW và DEFERRED_BY_USER; TASK-006 giữ IN_REVIEW, workbook gốc/Google live/browser chưa xác minh. Ngoại lệ chỉ cho bắt đầu local, không đóng gate và không cho phép ghi dữ liệu thật, commit/push/deploy hoặc làm TASK-008. Rule mã/trường nghiệp vụ công ty chưa chốt: dùng fixture giả có nhãn cho phần độc lập, không tự phê duyệt rule hay DRAFT.

## Phạm vi
Tạo/xem/sửa/search tài sản, cấp mã test server, aliases, delete/restore mềm, Version conflict. Lát đầu tiên là một loại tài sản + API+web+tests đầy đủ.

## Acceptance criteria của Task
- [ ] Tạo một tài sản, tìm lại, sửa/đổi vị trí giữ ID
- [ ] Delete/restore giữ mã, mã không tái cấp
- [ ] Hai editor bản cũ có 409; test fields/permissions và UAT web

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra

AI local 2026-10-13: build server exit0, warnings/errors0; 54/54 test task và9/9 hồi quy liên quan exit0, failed/skipped0; format riêng4project exit0; node syntax và diff check exit0. Không chạy lại toàn bộ UAT TASK-005/006. [Test Report](../07-testing/TEST-REPORT-TASK-007.md), [artifact](../07-testing/TASK-007-LOCAL-EVIDENCE.json), [thiết kế lát giả](../02-architecture/ASSET-DEMO-DESIGN-TASK-007.md).

## Checkpoint / Review

Phần local đã thực hiện: application CRUD/alias/search/scope/counter/idempotency/version; Google adapter atomic/reconcile; API bearer và Razor create/detail/edit/list/filter/paging/soft delete/restore, CSRF/error/loading/empty. Self-review với ai-debt-detector đã đọc: sửa binding Reason và trường tùy chọn rỗng/null, giữ input/key khi lỗi, schema/malformed JSON/journal và cell bound. Không có independent reviewer hoặc browser UAT; test HTTP render không là browser.

| AC Task | Local actual | Gate còn mở |
|---|---|---|
| Create/find/edit/location giữ ID | PASS_LOCAL giả / API / HTTP Razor | Rule/fields nghiệp vụ và reference source, Google live |
| Delete/restore giữ code, không tái cấp | PASS_LOCAL + Google HTTP stub/reconstruction | Persistence/restart backend Google thật |
| Stale409, fields/permission/web | PASS_LOCAL_API_HTTP_RENDERED | NOT_RUN_BROWSER/UAT AC-08 |

Giữ các checkbox nghiệm thu tổng thể chưa đạt: TASK-007 IN_REVIEW, không DONE. TASK-005/006 IN_REVIEW; TASK-005 hoãn giữ nguyên; chưa xác minh nguồn workbook/Google/browser TASK-006. TASK-008–014 TODO. Không commit/push/deploy/ghi dữ liệu thật; không yêu cầu secret hay thao tác thủ công.

## Lát local / giới hạn nghiệp vụ

- Một loại MACHINE (máy giả), trạng thái DRAFT_DEMO; Category DRAFT vẫn hiển thị nháp.
- Fixture test dùng TEST-TST-000001, không phải mã công ty; prefix/rule phải có ApprovedForDemo và Status DEMO mới được allocator dùng. Runtime không tự seed rule, reference hoặc tài sản.
- Trường kỹ thuật cần để kiểm scope: tên, loại, CategoryId, OrgUnitId, LocationId; serial/model/hãng/người quản lý/ngày ghi nhận/tình trạng/ghi chú/alias tùy chọn ở fixture. Không áp dụng required nghiệp vụ công ty chưa được duyệt.
- Dùng shared writer + process lease hiện có. Counter/entity/alias/operation/audit atomic trong business spreadsheet; không đổi auth, không init lại Google hoặc thêm SQL.
- UI lỗi/empty/loading/409 + API authorization/validation/CSRF kiểm tự động local; browser/live giữ NOT_RUN nếu không chạy.
- Skill thực tế đã đọc: dotnet-contribution:dotnet-backend-patterns (async/DI/DTO/tests theo kiến trúc hiện có; không dùng EF/SQL); ui-design:interaction-design (form loading/error/feedback Razor, không thêm React).

Skill self-review thực tế: skill-forge-essentials:ai-debt-detector; audit targeted failure/orphan/edge/dependency/architecture, không chứng nhận security review toàn sản phẩm.
