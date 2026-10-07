# PLAN-001 — Demo quản lý tài sản dựa trên Google Sheets/Drive

- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Status: READY
- Workflow chính / skill thực tế: `conductor:context-driven-development` cho Context → Spec/Plan và đồng bộ nguồn chuẩn; thực thi từng Task theo skill kỹ thuật phù hợp sau này.

## Thiết kế tối thiểu
- Quyết định sản phẩm: Google Sheets là CSDL, Drive lưu file; có web và app Android/iOS riêng; cây Excel hiện tại là `DRAFT`.
- [ADR-001](ADR-001-proposed-sheets-drive-web-mobile.md) đã `ACCEPTED`: .NET 10/Razor Pages/MAUI, API chung và một backend instance/single writer cho demo.
- Web và mobile dùng chung API, rules, authorization và dữ liệu; backend kiểm tra quyền cho list/detail/report/file, không dựa vào việc ẩn nút.
- Lát đầu tiên sau PoC/auth: dùng một nhánh máy móc TST với dữ liệu giả, import nhánh, admin tạo một máy, viewer tìm/xem, editor sửa vị trí và kiểm tra ID không đổi.

## Thứ tự thực hiện
| Thứ tự | Task | Trạng thái | Đầu ra | Phụ thuộc / gate |
|---|---|---|---|---|
| 1 | [TASK-002](../06-tasks/TASK-002.md) | DONE | Requirement/Plan/Task authoritative và câu hỏi cho bước setup | Không phụ thuộc code |
| 2 | [TASK-003](../06-tasks/TASK-003.md) | IN_REVIEW | ADR accepted, scaffold/framework và môi trường local; checkpoint external gates | Google/host/device |
| 3 | [TASK-004](../06-tasks/TASK-004.md) | DONE | Local 31 tests pass; live Google và manual resource/control-file có user-provided evidence | TASK-003 vẫn IN_REVIEW nhưng không chặn checkpoint TASK-004 |
| 4 | [TASK-005](../06-tasks/TASK-005.md) | IN_REVIEW | [Auth design](AUTH-DESIGN-PROPOSED.md), backend/API + Razor login/admin/roles/scope | Local format/build/57 tests pass; live Google + UI/UAT là gate |
| 5 | [TASK-006](../06-tasks/TASK-006.md) | TODO | Import/xem/tìm cây 665 nút nháp và TXT | TASK-005 |
| 6 | [TASK-007](../06-tasks/TASK-007.md) | TODO | Lát web + API CRUD tài sản có test | TASK-006; fields + rule mã demo |
| 7 | [TASK-008](../06-tasks/TASK-008.md) | TODO | Move/delete cây và đối tượng an toàn | TASK-007 |
| 8 | [TASK-009](../06-tasks/TASK-009.md) | TODO | Ảnh và DOCX/PDF trên Drive | TASK-007; Drive/file mẫu |
| 9 | [TASK-010](../06-tasks/TASK-010.md) | TODO | Items và snapshot số lượng | TASK-006; nguồn stock |
| 10 | [TASK-011](../06-tasks/TASK-011.md) | TODO | R01–R04 có scope/asOf/unit | TASK-007, TASK-010 |
| 11 | [TASK-012](../06-tasks/TASK-012.md) | TODO | App Android nhập liệu/chụp ảnh | TASK-005/007/009; thiết bị |
| 12 | [TASK-013](../06-tasks/TASK-013.md) | TODO | App iOS và bằng chứng riêng | TASK-012; Mac/Xcode/signing/device |
| 13 | [TASK-014](../06-tasks/TASK-014.md) | TODO | UAT, backup/restore, hướng dẫn | TASK-008/009/011/012/013 |

Chi tiết và trạng thái authoritative nằm tại [INDEX](../06-tasks/INDEX.md). Không
bắt mọi Task chờ toàn bộ sản phẩm, nhưng không bỏ qua gate trực tiếp; không dựng
tất cả bảng/module/code trong một lượt.

Checkpoint 2026-10-06: người dùng cho phép triển khai phần local của TASK-004 độc
lập với các gate Google/hosting/thiết bị còn mở. Ngày 2026-10-07, người dùng cung
cấp kết quả chạy readiness, access-boundary, init và PoC trên Google thật;
TASK-004 chuyển `DONE` sau khi người dùng xác nhận thủ công tab/TXT và tính hợp lệ
file control; AI không trực tiếp quan sát. TASK-003 vẫn `IN_REVIEW` vì host/device
còn mở.

## Rủi ro và phương án xử lý
- Mã nháp xung đột DOCX: giữ state `DRAFT`, source audit và chốt rule demo trước allocator.
- Sheets cạnh tranh/timeout: PoC single writer/idempotency/atomic batch; không scale-out khi chưa có cơ chế phối hợp được duyệt.
- Drive ownership/file ACL: My Drive đã chốt; chỉ dùng `drive.file`. Resource mới
  phải do app provisioning, resource có sẵn qua Picker; folder không truyền quyền
  cho file con. Live test phải có cả resource được phép và file control bị chặn.
- Không có tồn: snapshot riêng, `UNKNOWN` không biến thành 0.
- iOS: Mac/Xcode/signing/device là gate thật; thiếu thì `BLOCKED`/`NOT_RUN`, không lấy Android thay bằng chứng.
- Auth scope: dùng cùng authorization service cho API/file/report/list/detail và có test trực tiếp.
- Auth design TASK-005 đã `ACCEPTED`: scope gắn từng role assignment để nhiều
  role không khuếch đại quyền; system spreadsheet riêng và bootstrap bằng command.
  Lượt local không tự tạo/chọn/ghi resource Google.

## Kiểm thử và nghiệm thu
[Kế hoạch nghiệm thu](../07-testing/ACCEPTANCE-PLAN.md) ánh xạ các case mức sản
phẩm. Mỗi Task phải ghi build/test count, môi trường, thiết bị và bằng chứng thật;
fake adapter không thay live Google, Android không thay iOS. Tài liệu READY không
có nghĩa feature đã hoàn thành.
