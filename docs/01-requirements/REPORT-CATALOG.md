# Danh sách báo cáo

Nhóm REQUIRED thuộc yêu cầu đã nêu. Nhóm PROPOSED là gợi ý theo dõi, cần chọn trước khi đưa vào Task triển khai.

| ID | Báo cáo | Trạng thái | Nguồn/trường cần có |
|---|---|---|---|
| R01 | Toàn bộ tài sản trong phạm vi người dùng | REQUIRED | Assets không deleted; ID, tên, loại, tình trạng, vị trí |
| R02 | Tài sản theo danh mục và cây con | REQUIRED | CategoryId/ParentId; hỗ trợ tổng hợp cấp F |
| R03 | Vật tư hết số lượng | REQUIRED | Snapshot quantity = 0; không gồm UNKNOWN |
| R04 | Vật tư sắp hết | REQUIRED | 0 < quantity <= minimum; ngưỡng theo item/kho và cùng đơn vị |
| R05 | Tài sản theo phòng ban/địa điểm/người quản lý | PROPOSED | OrgUnit/Location/Custodian |
| R06 | Theo tình trạng: đang dùng, dự phòng, hỏng, đang sửa, ngừng dùng | PROPOSED | Bộ trạng thái được chốt |
| R07 | Tài sản thiếu ảnh/hồ sơ/serial theo loại | PROPOSED | Quy tắc required theo loại |
| R08 | Tài sản chưa có người/đơn vị quản lý | PROPOSED | Các trường quản lý và quyền scope |
| R09 | Danh mục rỗng, danh mục ngừng dùng còn liên kết | PROPOSED | Categories và liên kết |
| R10 | Biến động tạo/sửa/chuyển/xóa theo thời gian | PROPOSED | AuditEvents có actor/operation/time |
| R11 | Hồ sơ/biểu mẫu thiếu DOCX hoặc PDF cùng phiên bản | PROPOSED | Documents/DocumentVersions |
| R12 | Tồn UNKNOWN hoặc snapshot đã lâu chưa cập nhật | PROPOSED | Snapshot status/asOf; ngưỡng stale cấu hình |
| R13 | Mã/tên nháp cần rà soát, import trùng/chưa mapping | PROPOSED | ImportRows/CodeReview/Aliases |
| R14 | Sắp hết bảo hành/kiểm định/giấy phép | FUTURE_PROPOSED | Dates thực tế; không thay R03/R04 |
| R15 | Giá trị/khấu hao tài sản | FUTURE_PROPOSED | Nguyên giá, tiền tệ, phương pháp và quy tắc kế toán |

## Chuẩn hiển thị
Mọi report có filter, số bản ghi, thời điểm/asOf, phạm vi quyền và cách tính. Danh sách mặc định không gồm xóa mềm; xem archived/deleted cần quyền và filter riêng.
R01/R02 đếm từng Asset, không đếm Items hoặc nhân sự như tài sản; phân loại loại đối tượng rõ ràng.
R03/R04 chỉ cho stock-tracked items, gộp theo Item+Location+Unit; không cộng khác đơn vị. UNKNOWN/không có ngưỡng được liệt kê riêng, không tự =0.
Đề xuất xuất CSV/XLSX và in/PDF theo filter; user hiện mới yêu cầu xuất cây TXT nên định dạng report export phải chọn trong Task, không tự mở rộng tất cả.
