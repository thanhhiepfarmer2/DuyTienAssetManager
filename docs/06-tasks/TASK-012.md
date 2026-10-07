# TASK-012 — App Android: nhập liệu và chụp ảnh

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-005,007,009; Android device/API endpoint
- AC liên quan: AC-04, AC-08, AC-09

## Phạm vi
Ứng dụng Android riêng theo kiến trúc được chốt tại TASK-003, auth secure storage, list/detail/create/edit, chụp/chọn ảnh, revoke/network/permission fallback.

## Acceptance criteria của Task
- [ ] Build/cài chạy Android trên thiết bị đã ghi
- [ ] Chụp thật/từ chối quyền/retry được kiểm thử, file có Drive metadata
- [ ] Form validation và version conflicts cùng rules web

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
