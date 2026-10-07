# Mapping nguồn và quy trình nhập

## Cây danh mục
Nguồn cấu trúc chính: F1-F3 Mã ngang cột A(STT), B(Danh mục), C(Mã). F1/F2/F3 bổ sung definition sau join theo STT loại trừ dòng tiêu đề lặp “(F1)/(F2)”.
source_stt → provenance/display hierarchy; parent derived from prefix trong staging; name→Name; code→SegmentCode; draft_full_code→preview only.
CategoryId cấp một lần khi commit; reimport chính xác cùng file/hash/mapping không nhân đôi. Workbook khác version cần match theo ID/source mapping đã lưu và preview (STT có thể đổi), không blind upsert STT hoặc name.
Từng node là bản nháp, đủ path nhưng chưa giải quyết canonical code semantics. Định nghĩa/ghi chú không tự phân quyền hay tạo asset.

## Excel vật tư
Header dòng 2, dữ liệu từ dòng 3; mapping dùng ordinal + header fingerprint.
| Cột | Nội dung nguồn | Đích / xử lý |
|---|---|---|
| 1 | Mã | Legacy/source code dạng text, namespace MISA_VTHH |
| 2 | Tên | Item.Name; không là Asset cụ thể |
| 3 | Tính chất | Item.Nature; stock tracking xác lập theo loại |
| 4 | Nhóm VTHH | SourceGroup; mapping CategoryId cần preview, không đoán từ tên |
| 5 | Mô tả | Item description |
| 6 | ĐVT chính | PrimaryUnit |
| 7 | Mã nhà SX | ManufacturerCode; không tự là Serial |
| 8 | Kho ngầm định | DefaultWarehouseRef; không là tồn/vị trí actual |
| 9–12 | Đơn vị chuyển đổi, tỷ lệ, phép tính, Mô tả | ItemConversions; validate factor/operator |
| 13–16 | Giá bán 1/2/3, giá cố định | Commercial fields tham chiếu, không nguyên giá |
| 17–20 | Mã NVL, tên NVL, ĐVT, Số lượng | BOM/reference nếu có, chưa thuộc inventory snapshot |

Không có số tồn trong nguồn này; StockBalances lấy nguồn riêng hoặc nhập thủ công demo có nhãn giả/thời điểm rõ. Không seed quantity=0 cho 20.744 dòng.

## Quy trình
1. Chọn file có quyền IMPORT, xác nhận hash/header/mapping; input chỉ local hoặc upload endpoint đã validate.
2. Parse vào staging, kiểm tra rows/errors/duplicates; cho preview cây/Items, số sẽ thêm/sửa/skip, xung đột mã/nguồn.
3. Review lỗi CĐKEM 839/840 và 39 tên nhiều code; không tự sửa dữ liệu nguồn.
4. Commit job đã valid theo chunk; mỗi chunk operation idempotent, progress persisted, trạng thái PARTIAL rõ nếu hỏng.
5. Chỉ publish version cây sau validation tổng thể; không để người dùng nhìn cây nửa cũ nửa mới.
6. Reconcile/tiếp tục job sau restart/429/timeout; không tính thành công toàn job chỉ từ request cuối.
Excel formulas không chạy để cấp mã. Kết quả text/export phòng formula injection; string bắt đầu =,+,-,@ cần xử lý khi xuất CSV/XLSX phù hợp.
