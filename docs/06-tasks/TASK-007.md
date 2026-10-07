# TASK-007 — CRUD tài sản trên web

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-006; required fields từng loại và rule code demo
- AC liên quan: AC-05, AC-06, AC-08

## Phạm vi
Tạo/xem/sửa/search tài sản, cấp mã test server, aliases, delete/restore mềm, Version conflict. Lát đầu tiên là một loại tài sản + API+web+tests đầy đủ.

## Acceptance criteria của Task
- [ ] Tạo một tài sản, tìm lại, sửa/đổi vị trí giữ ID
- [ ] Delete/restore giữ mã, mã không tái cấp
- [ ] Hai editor bản cũ có 409; test fields/permissions và UAT web

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
