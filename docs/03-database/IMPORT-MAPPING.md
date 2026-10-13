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

## Mapping local TASK-006 — 2026-10-10

CATEGORY_F1_F3_V1: cấu trúc header A=STT/B=Danh mục/C=Mã; metadata header STT và Định nghĩa/Mô tả/Ghi chú/Definition, join exact STT đúng level, bỏ header lặp (F1)/(F2)/(F3). Header khác không được tự đoán; thiếu sheet metadata có warning. JSON staging nhận `definition` và `category_id` tùy chọn; status do server luôn DRAFT, draft_full_code từ client không dùng làm ID/path authoritative.

SourceKey namespace mapping + SegmentCode duy nhất là **source mapping đã lưu**, không CategoryId; thay STT/tên không đổi ID. Đổi code cần explicit CategoryId đã biết (JSON), không match theo tên/STT. Code trùng hoặc remap key đã gắn ID khác bị chặn; lịch sử version giữ mã/path cũ. Full snapshot thay cây hiệu lực và preview các nhánh thiếu là REMOVE_FROM_VERSION, không xóa lịch sử. Tất cả nút cũ/mới phải trong scope IMPORT; move kiểm nguồn/đích cùng assignment. Import đầu cây mới cần Category ALL. Không hỗ trợ merge một file thiếu cha vào nhánh ngoài file, không tạo endpoint sửa/chuyển cây TASK-008.

Đối với subset bounded của TASK-006, bước chunk tổng quát trong plan gốc được thay bằng một atomic batch cả snapshot+journal+pointer; timeout reconcile hoặc explicit retry cùng key/file, không PARTIAL. Giới hạn được ghi ở SHEETS-SCHEMA/Test Report, vượt giới hạn dừng không cắt dữ liệu. XLSX gốc chưa có trong repo; staging khớp baseline nhưng definition gốc chưa đối chiếu.

## TASK-010 local — 2026-10-13

ITEMS_VTHH20_V1: exact20header+ordinal theo bảng trên; XLSX sheet Vat_tu__hang_hoa__dich_vu, header2/dữ liệu>=3 hoặc JSON ItemDocument20cells. Hai Mô tả5/12 giữ riêng; raw20cells giữ mã, commercial/conversion/BOM, mã numeric chỉ General/text/zero-mask hỗ trợ, format chưa biết bị từ chối. SourceHash SHA256 bytes khi reader nhận file và canonical DocumentHash/RequestHash khi vào job; hash client khai báo không là xác minh workbook gốc. Không đọc XLS/formula/đoán header khác. Nguồn gốc20744/20743/20464-276-4/CĐKEM839-840 vẫn NOT_VERIFIED_SOURCE_ORIGINAL; test synthetic20744 không chứng nhận baseline công ty.

Group mapping CategoryId + stockTracked nullable tường minh; chưa map QUARANTINED chỉ Admin ALL phục hồi, không global. Code trùng cần MERGE toàn20cột đồng nhất hoặc SPLIT identities khác nhau có lý do; decisions/hash/mapping/provenance audit bền. Identity không lấy row/STT làm ID; reimport upsert ADD/UPDATE/SKIP, không xóa Item vắng nguồn. Không tự cấp ObjectCode/Asset/nhân sự/tồn.

Staging plan theo chunk có byte budget, tối đa100row; STAGING chưa ghi Items, retry cùng file/mapping/key khôi phục prefix sau restart. VALIDATED rồi commit từng chunk nguyên tử Items+progress+journal+audit; PARTIAL cho tiến độ chưa đủ; COMPLETE khi processed==planned. Không báo thành công toàn job từ chunk cuối nếu totals không khớp. [Thiết kế](../02-architecture/INVENTORY-DESIGN-TASK-010.md), [report](../07-testing/TEST-REPORT-TASK-010.md).
