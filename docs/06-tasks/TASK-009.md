# TASK-009 — Drive ảnh và biểu mẫu DOCX/PDF

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-007; file mẫu và Drive target
- AC liên quan: AC-10, AC-13

## Phạm vi
Upload ảnh web, Files states, doc registry/versions, cặp DOCX+PDF chuẩn cùng version, gateway kiểm quyền, failure/reconcile/retention.

## Acceptance criteria của Task
- [ ] Download đúng file và version; thiếu counterpart báo rõ
- [ ] User ngoài scope bị chặn dù biết file API id
- [ ] Drive thành công+Sheet lỗi có operation để đối soát; không mất file gốc

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
