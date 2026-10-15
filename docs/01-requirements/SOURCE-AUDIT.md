# Rà soát nguồn — 2026-10-06

Phân tích trực tiếp ba file người dùng cung cấp; không sửa bản nguồn. Đây là kiểm tra cấu trúc/nội dung, chưa xác nhận hiệu lực ISO hoặc độ đúng của nghiệp vụ.

## Bảng mã hiện tại
- Workbook có 5 sheet: F1-F3 Mã ngang, Mã nhân viên, F1, F2, F3.
- F1-F3 Mã ngang có 665 dòng dữ liệu: 15 F1, 98 F2, 552 F3; STT không trùng, các parent path đều có.
- 665 mã đoạn khác nhau đều khớp regex `[A-Z][A-Z0-9]{2}`; có 610 tên sau trim, 39 tên xuất hiện với nhiều mã. Kiểm tra này chưa khẳng định cùng tên là cùng nghĩa.
- Các sheet F1/F2/F3 có nội dung định nghĩa/ghi chú/ví dụ; “Liệt kê” trong cell không tự trở thành các nút F4-F10.
- Sheet Mã nhân viên chứa công thức dựa vào STT/ngày; không dùng để cấp ID lâu dài hoặc tự tạo user. Không sao chép dữ liệu nhân sự vào bộ yêu cầu.

| STT F1 hiện tại | Tên nhóm | Mã Excel |
|---|---|---|
| 1 | Nhân sự | `NSU` |
| 2 | Đối tác | `DTA` |
| 3 | Tài sản cố định liên quan xây dựng | `TSC` |
| 4 | Tài sản thiết bị máy móc(Cố định) | `TST` |
| 5 | Công cụ dụng cụ, máy móc cơ động | `CC2` |
| 6 | Tài sản vô hình và tài nguyên số | `TS1` |
| 7 | Phương tiện vận tải | `PTV` |
| 8 | Hồ sơ, tài liệu | `HS1` |
| 9 | Tài liệu truyền thông | `TL2` |
| 10 | Công trình | `CTR` |
| 11 | Vật tư đầu vào | `VTD` |
| 12 | Vật tư đầu ra | `VT3` |
| 13 | Sản phẩm - thành phẩm đầu vào | `SPT` |
| 14 | Sản phẩm - thành phẩm đầu ra | `SP1` |
| 15 | Dịch vụ | `DVU` |

## Khác biệt với DOCX
DOCX là v0.1, dự thảo trình phê duyệt, mã dự kiến QD.17.02; ghi 13 F1, 705 nút sâu F1-F10, 359 dòng mã ngang/317 tên và mã. Các con số không khớp workbook đính kèm hiện tại.
Ví dụ DOCX dùng XDG/TMM/CDC/VTA/HSL/VDV..., Excel dùng TSC/TST/CC2/PTV/HS1/VTD... và bổ sung Tài sản vô hình, tài nguyên số cùng Dịch vụ.
DOCX quy định cùng khái niệm dùng cùng mã; Excel hiện có các tên nhiều mã dưới đây. Người dùng chọn giữ workbook làm nguồn demo, không tự sửa/tự hợp nhất các tên này. Cần rà nghĩa/mapping trước ban hành.
DOCX phần công thức nhân viên tham chiếu D2; workbook thực tế dùng E2 cho ngày vào. Không sao chép tham chiếu cột từ DOCX vào importer.
DOCX mô tả GM là Gmart Windows trong bảng nhân viên; ứng dụng dùng tên Công ty TNHH Duy Tiến. Tiền tố GM được giữ như mã nguồn đề xuất, tên pháp nhân trong văn bản không tự suy ra thay đổi prefix.

| Tên cần rà soát nghĩa | Các mã trong Excel |
|---|---|
| Nhà máy và xưởng sản xuất | NM1, NMX |
| Văn phòng | VP1, VPH |
| Công trình phụ trợ | CT1, CT2, CTP |
| Hệ thống kỹ thuật gắn liền | HT1, HT2, HTK |
| Kho chung | KCH, KLN |
| Kính | KI1, KI2, KI3, KIN |
| Trang trí | TR1, TTR |
| Tiêu chuẩn, quy chuẩn và catalogue kỹ thuật | TC3, TCQ |
| Khảo sát, đo đạc và yêu cầu đầu vào | KS2, KSD |
| Cơ sở thiết kế và thuyết minh kỹ thuật | CST, STK |
| Bản vẽ tổng thể và phối hợp kiến trúc | BV1, BVT |
| Shop drawing cửa, vách kính và mặt dựng | SDC, SDV |
| Chi tiết hệ, liên kết và chống thấm | CT0, CTH |
| Tính toán kết cấu, tải trọng và kính | TT6, TTK |
| Vật liệu trình duyệt, mẫu và màu sắc | VL1, VLT |
| Bóc tách khối lượng, BOM và định mức | BT1, BTK |
| Danh sách cắt, gia công và dữ liệu CNC | DS1, DSC |
| Mockup, RFI và phê duyệt kỹ thuật | MR1, MRP |
| Bản vẽ hoàn công và tài liệu vận hành bảo trì | BV2, BVH |
| Phụ kiện | PK1, PKI |
| Linh kiện | LK1, LKI |
| Nhôm | NH1, NHO |
| Cửa | CU1, CU2, CU3, CU4, CU5, CUA |
| Vách | VA1, VA2, VA3, VAC |
| Vòm | VO1, VO2, VO3, VOM |
| Tum | TU1, TUM |
| Lam | LA1, LAM |
| Ốp | OP1, OP2, OP3, OPX |
| Dạ | DA1, DA2, DA3, DAX |
| Nhựa | NH2, NHU |
| Cơ khí(Sắt - thép - inox) | CK1, CKS |
| Lan can | LC1, LCA |
| Cầu thang | CT7, CT8 |
| Khung | KH3, KHU |
| Mái | MA1, MAI |
| Cửa cường lực | CC4, CCL |
| Cửa Cabin | CC5, CCA |
| Vách kính | VK1, VKI |
| Mái kính | MK1, MKI |

## File vật tư, hàng hóa, dịch vụ
- Sheet Vat_tu__hang_hoa__dich_vu, dòng 1 tiêu đề, dòng 2 header, dữ liệu dòng 3–20746; 20.744 dòng có mã, 20.743 mã khác nhau.
- 20.464 dòng Vật tư hàng hóa, 276 Thành phẩm, 4 Dịch vụ.
- CĐKEM trùng tại dòng 839/840 với cùng tên Cửa đi sắt kẽm STĐ; importer phải đưa vào hàng đợi review, không tự chọn một dòng.
- 20 cột; cột “Mô tả” xuất hiện ở vị trí 5 và 12 với ngữ cảnh khác nhau. Mapping phải dùng header + ordinal, không dictionary chỉ theo tên header.
- Các cột 17–20: Mã nguyên vật liệu, Tên nguyên vật liệu, ĐVT, Số lượng. Cột 20 không có giá trị trong file; không có nguồn tồn thực tế.
- “Kho ngầm định” và giá bán mặc định không phải vị trí tài sản hoặc nguyên giá kế toán.

## Dữ liệu chuẩn bị
[category-candidates.json](../03-database/category-candidates.json) là 665 nút staging theo nguồn. STT/row chỉ dùng truy xuất nguồn; chưa có CategoryId authoritative. `draft_full_code` ghép theo parent trong Excel; mọi trạng thái là DRAFT.
Importer phải đọc lại file nguồn và đối chiếu hash, không dùng JSON như chứng nhận cây đã ban hành. Không upsert một bản workbook mới bằng STT đơn thuần vì STT có thể đổi khi sắp xếp.

## Dấu vết phiên bản nguồn
- `04_Quy định nguyên tắc đặt mã chung Duy Tiến.docx`: SHA-256 `ff79ba5c5949104d6b985963fe8f64e3c2a22b5f90a841d995e67174e88a9f21`
- `Bảng mã(Nháp lần 5).xlsx`: SHA-256 `0f9c0ceb78ed321175de769a3a49f22a5fdf45fe7c0a69b25d00cad6902c1fbb`
- `Vat_tu__hang_hoa__dich_vu1.2.xlsx`: SHA-256 `fca3b33c2f2ecc7811df964dd01e816678768b936f9f38821882994f7574d0d3`

## Checkpoint TASK-006 — 2026-10-10

AI đọc lại staging trong repository: 665/15/98/552, 39 tên nhiều code, không lệch baseline. Không tìm thấy workbook XLSX gốc kể cả hidden/ignored (ngoài bin/obj/.git); chưa tái xác minh hash/header/definition gốc. Nguồn authoritative vẫn workbook Nháp lần5 đã xác lập; không thay bằng DOCX 13F1. Parser metadata đã test với workbook giả, không là bằng chứng nguồn original.

## Reference tối ưu/offline bổ sung — 2026-10-14

Nguồn bằng chứng: phân tích tĩnh **người dùng cung cấp**, không phải AI đọc lại file hoặc benchmark. [QuanLyVatTu_DuyTien_v4_Offline.html](https://drive.google.com/file/d/1ilMyTYJnKNbsvhie919cAElKZxBB9hh_/view): khoảng7,19MB tự chứa JS/CSS, không script src/stylesheet ngoài; dataset20744x12; IndexedDB duy-tien-material-manager/application/state-v1; snapshot RAM/Map/searchIndex chuẩn hóa/memoization/deferred search, render60tăng dần; autosave debounce900ms; XLSX/file người dùng chọn. V4 vẫn gọi Apps Script cho login/resume/mutations, offline cached user chỉ đọc và lần đầu cần Internet.

Dùng làm UX/performance reference của REQ-002, không nguồn nghiệp vụ authoritative/benchmark/đề xuất rule mã theo name/path. Không tải hoặc commit HTML/dataset, không seed tự động. Không ép12cột vào contract VTHH20cột hoặc bỏ mô tả; workbook gốc/provenance/rule vẫn OPEN/TBD. LocalOffline mới cần đọc/ghi thật qua backend/JSON durable, không sao chép giới hạn offline-read-only hoặc báo đã lưu theo debounce.


## Nguồn giao diện yêu cầu15/10/2026 — chưa có file gốc

`QuanLyVatTu_DuyTien_v4_Offline.html`: AI tìm filename (không phân biệt hoa/thường) trong workspace kể cả hidden/ignored, loại .git, và thư mục attachments hiện có: **0/0 file**. Chỉ đọc file yêu cầu Pasted text.txt; không đọc HTML/dataset, không tải từ Drive hoặc nhập vào Git. Trạng thái **MISSING_REFERENCE / NOT_REVIEWED_HTML**. Phân tích tĩnh trên vẫn là thông tin do người dùng cung cấp. [REQ-003](REQ-003-web-experience.md), [thiết kế DRAFT](../02-architecture/WEB-UI-DESIGN.md) và TASK-016 ghi rõ phần đối chiếu còn thiếu; màu/định hướng là chỉ đạo người dùng, không suy là đã quan sát tool.
