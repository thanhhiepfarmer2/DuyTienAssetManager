# TASK-014 — Nghiệm thu, backup/restore và hướng dẫn

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-008,009,011,012,013
- AC liên quan: AC-01..AC-14

## Phạm vi
Chạy acceptance toàn bộ, test tải/import quota theo nguồn thực, kiểm tra bảo mật scope/file, backup/restore dev, hướng dẫn user và vận hành.

## Acceptance criteria của Task
- [ ] AC có evidence thật; UAT người dùng được lưu
- [ ] Restore dev giữ IDs/mapping/quyền và Drive links
- [ ] Hạn chế iOS/Google/live tests nêu rõ; không tự deploy/publish stores

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
