# TASK-011 — Báo cáo tài sản và tồn hết/sắp hết

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-007,010
- AC liên quan: AC-04, AC-12

## Phạm vi
Triển khai R01-R04, scoped filters/tree aggregation, asOf/units, paging. Chỉ thêm report PROPOSED khi được chọn.

## Acceptance criteria của Task
- [ ] 0/low/UNKNOWN/non-stock item phân biệt
- [ ] Không cộng khác đơn vị, không lộ tổng ngoài scope
- [ ] R01/R02 đếm Assets khác Item count; UAT filter

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.
