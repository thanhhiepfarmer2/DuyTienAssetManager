# Yêu cầu sản phẩm — READY

- Tên hiển thị: **Mã số hóa tài sản Công ty TNHH Duy Tiến**.
- Người phụ trách: Nguyễn Thanh Hiệp.
- Nguồn: yêu cầu người dùng ngày 2026-10-06 và gói `DuyTien_Asset_Requirements`.
- Chi tiết authoritative: [REQ-001](REQ-001-asset-digitization.md); rà soát dữ liệu nguồn: [SOURCE-AUDIT](SOURCE-AUDIT.md).

## Mục tiêu và người dùng
Số hóa việc phân loại, ghi nhận, tra cứu, quản lý hồ sơ/ảnh và theo dõi tài sản,
vật tư theo phạm vi được phân quyền. Người dùng gồm admin hệ thống và các vai trò
nghiệp vụ được gán quyền đọc/thêm/sửa/xóa hoặc quyền chuyên biệt theo chức năng,
đơn vị, địa điểm và nhánh danh mục.

## Quyết định đã xác lập
| Hạng mục | Quyết định |
|---|---|
| Cây danh mục demo | Dùng cây từ `Bảng mã(Nháp lần 5).xlsx`: 15 F1, 98 F2, 552 F3; toàn bộ vẫn là `DRAFT`, không phải bộ mã đã ban hành |
| CSDL và tệp | Google Sheets là CSDL; Drive công ty lưu ảnh, DOCX và PDF |
| Kênh sử dụng | Có web và ứng dụng Android/iOS riêng; web responsive không thay cho app |
| Tài khoản và quyền | Có đăng nhập; admin quản lý tài khoản; quyền theo hành động và phạm vi vị trí/chức năng, được kiểm tra phía server |
| Báo cáo bắt buộc | Toàn bộ tài sản, theo danh mục/cây con, vật tư hết số lượng và sắp hết số lượng |
| Ý nghĩa “đang hết” | Hết/sắp hết **số lượng**, không phải hết bảo hành |

## Phạm vi demo
- Import có preview cây danh mục nháp và dữ liệu Items; không biến Item thành từng Asset.
- Quản lý vòng đời cơ bản của Asset: tạo, xem, sửa, tìm, chuyển phân loại, xóa mềm/khôi phục và giữ ID ổn định.
- Ảnh và hồ sơ tài liệu được lưu trên Drive; metadata và liên kết được quản lý trong Sheets.
- Đăng nhập, quản trị tài khoản, vai trò/quyền/phạm vi; danh sách, chi tiết, báo cáo và file dùng cùng quy tắc quyền phía server.
- Web và app Android/iOS dùng cùng API/rules; mobile hỗ trợ nhập liệu và chụp/chọn ảnh.
- Snapshot số lượng tách khỏi dữ liệu vật tư nguồn; `UNKNOWN` không được quy đổi thành 0.

## Ngoài phạm vi hiện tại
- Không tự mở rộng thành ERP/MES, sổ nhập/xuất kho đầy đủ, kế toán/khấu hao hoặc quản lý toàn bộ nhân sự/đối tác/công trình.
- Chưa yêu cầu đồng bộ offline, phát hành App Store/Google Play hay triển khai production.
- Không coi mã trong Excel nháp là bộ mã chính thức đã phê duyệt.

## Kiến trúc đã chốt và gate còn mở
| Hạng mục | Trạng thái |
|---|---|
| ASP.NET Core .NET 10, Razor Pages, .NET MAUI Android/iOS | `ACCEPTED` tại [ADR-001](../02-architecture/ADR-001-proposed-sheets-drive-web-mobile.md) |
| Một backend/API instance, single writer cho Sheets | `ACCEPTED` cho demo; idempotency/reconciliation phải PoC tại TASK-004 |
| Google Drive | `ACCEPTED`: My Drive của tài khoản vận hành; OAuth backend tách app login, chỉ `drive.file`; resource mới provisioning, có sẵn qua Picker; folder không truyền quyền file con |
| Phạm vi truy cập | `ACCEPTED`: trong công ty và từ Internet qua HTTPS; host/domain/TLS vẫn `TBD` |
| Google Cloud project/OAuth client/spreadsheetId/folderId | `TBD` — gate live integration của TASK-004 |
| Định dạng/prefix cấp mã demo | `TBD` — phải được người có thẩm quyền chốt trước allocator |

## Tiêu chí thành công
Các acceptance criteria sản phẩm trong [REQ-001](REQ-001-asset-digitization.md)
được kiểm chứng bằng test/UAT tương ứng; riêng Google, Android và iOS cần bằng
chứng trên môi trường thật. TASK-002 chỉ xác lập tài liệu, không phải bằng chứng
ứng dụng đã hoạt động.
