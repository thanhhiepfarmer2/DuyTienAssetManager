# TASK-008 — Sửa/chuyển/ngừng cây và di chuyển đối tượng

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-007
- AC liên quan: AC-06, AC-07

## Phạm vi
Preview bulk, chuyển cây con, cập nhật full path/tree version, scopes hai phía, chặn cycle/F10; xóa rỗng hoặc relocate/archive có xác nhận tác động.

## Acceptance criteria của Task
- [ ] ID node/assets không đổi khi move
- [ ] Cycle/F11 và xóa nhánh đang dùng không có phương án bị chặn
- [ ] Publish tree version đầy đủ, không thấy tree partial; TXT/report theo tree mới

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
