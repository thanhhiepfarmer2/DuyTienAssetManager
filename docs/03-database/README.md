# Dữ liệu

Google Sheets đã được chọn làm CSDL; Google Drive lưu binary. TASK-004 đã có
resource dev/PoC và bằng chứng người dùng cung cấp, nhưng không có nghĩa toàn bộ
schema nghiệp vụ đã được tạo. Phần nghiệp vụ của
[SHEETS-SCHEMA](SHEETS-SCHEMA.md) vẫn là schema logic `PROPOSED`; riêng phần auth
TASK-005 đã `ACCEPTED` và có Google system adapter local. System spreadsheet thật
chưa được tạo/chọn/ghi; live init/bootstrap vẫn là gate. Mỗi trường nghiệp vụ khi
triển khai vẫn cần type, required, default, unique, quan hệ và nguồn quy tắc.

Schema/versioning phải thống nhất với code; dữ liệu test dùng mẫu giả/đã làm
sạch. [IMPORT-MAPPING](IMPORT-MAPPING.md) và `category-candidates.json` chỉ là
mapping/staging của nguồn nháp, không phải ID hay cây đã ban hành. Rule mã tài
sản vẫn phải được chốt trước khi triển khai allocator.
