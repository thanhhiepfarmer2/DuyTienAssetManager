# TASK-006 — Import cây và xem/tìm danh mục

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-005; mapping và state nháp
- AC liên quan: AC-01, AC-02, AC-07

## Phạm vi
Preview cây 665 nút, metadata từ F1/F2/F3, import idempotent, state DRAFT và report 39 tên nhiều code. Xuất TXT theo quyền.

## Acceptance criteria của Task
- [ ] Đúng 15/98/552 nodes, không thiếu parent, không tạo F4 từ text ghi chú
- [ ] Reimport cùng file không tạo bản sao; khác version có diff
- [ ] TXT Unicode/indent/path đúng và không lộ nhánh ngoài scope

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
