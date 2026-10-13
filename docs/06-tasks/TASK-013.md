# TASK-013 — App iOS: nhập liệu và chụp ảnh

- Status: TODO
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-012; Mac/Xcode/signing/device
- AC liên quan: AC-04, AC-08, AC-09

## Phạm vi
Build app iOS riêng theo kiến trúc được chốt tại TASK-003, kiểm thử cùng kịch bản, permission descriptions, secure storage, camera/library/file download; xác lập cách phân phối nội bộ.

## Acceptance criteria của Task
- [ ] Bằng chứng build/cài/chạy iOS độc lập
- [ ] Camera/file/security/permission tests trên iOS; Android pass không thay
- [ ] Thiếu Mac/signing/device thì BLOCKED/NOT_RUN, không DONE

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.

## Lịch thực hiện — 2026-10-14

Execution: **DEFERRED_BY_USER**. Người dùng thay chỉ đạo triển khai iOS bằng ưu tiên Web → Android → iOS hoãn do môi trường/thiết bị chưa sẵn sàng. Không công việc iOS đang chạy được quan sát ở đầu lượt; target/code/scaffold hiện hữu giữ, không DONE/xóa. Compile TASK-003 lịch sử không runtime iOS PASS. Chưa chốt phân phối nội bộ: PROPOSED/TBD, không ký/upload/phát hành.
