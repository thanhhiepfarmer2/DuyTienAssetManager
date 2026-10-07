# TASK-010 — Import Items và snapshot số lượng

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-006; nguồn stock snapshot và ngưỡng
- AC liên quan: AC-11, AC-12

## Phạm vi
Mapping 20.744 rows thành Items qua chunk/preview; resolve CĐKEM có decision; số lượng snapshot riêng UNKNOWN. Không tạo một Asset cho mỗi Item.

## Acceptance criteria của Task
- [ ] Hai cột Mô tả không ghi đè nhau; mã cũ đủ số 0/format
- [ ] Không coi BOM/kho ngầm định là tồn; snapshot input có asOf/unit/source
- [ ] Partial import/retry không silent duplicate; default stock UNKNOWN

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
