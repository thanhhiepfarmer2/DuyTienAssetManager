# TASK-006 — Import cây và xem/tìm danh mục

- Status: `IN_REVIEW`
- Type: FEATURE; Requirement: REQ-001 FR-02/FR-03, AC-01/AC-02/AC-07.
- Plan: PLAN-001; bắt đầu local 2026-10-10.
- Ngoại lệ phụ thuộc: người dùng cho phép bắt đầu dù TASK-005 `IN_REVIEW`, chấp nhận hoãn UAT TASK-005. Không thay chính sách auth, không chạy lại UAT đã đạt.

## Phạm vi và thiết kế local

- Nạp JSON staging hoặc XLSX: cấu trúc sheet `F1-F3 Mã ngang` A/B/C; metadata từ F1/F2/F3 join theo STT, bỏ header lặp. Không đọc/import nhân sự, Items, Assets hoặc ghi chú thành F4.
- Preview lỗi code/cha/cấp/chu trình; DRAFT; báo tên nhiều code, baseline và diff trước commit.
- CategoryId bất biến, cấp khi commit, source mapping lưu riêng. Phiên bản mới match ID đã xác lập hoặc source key mã nguồn duy nhất đã lưu; không match theo STT/tên. Đổi source code cần chỉ rõ CategoryId; không đoán rename.
- Import snapshot có preview hash + revision + idempotency key. Cùng source/mapping không nhân đôi; đổi payload cùng key bị chặn; stale preview conflict. Giữ version cũ/alias qua lịch sử phiên bản, không dựng move/delete ngoài TASK-006.
- Demo bounded input, commit toàn snapshot bằng một atomic batch trong business spreadsheet: Categories, TreeVersions, ImportJobs, ImportRows, Operations, AuditEvents và marker schema. Pointer version DRAFT hiệu lực demo đổi cùng batch; không phát hành mã chính thức, không nhìn thấy cây dở. Timeout retry đối soát ledger, không append mù. Không cần chunk PARTIAL cho input bounded của task này.
- Đọc/tìm/tree/detail/export lọc bằng ScopeAuthorizationService TASK-005 trước count/path; Category chỉ áp dụng CategorySubtree (OrgUnit/Location N/A). Full snapshot import cần quyền IMPORT trên toàn phạm vi ảnh hưởng; không ghi ngoài scope.
- API bearer; Razor cookie + antiforgery, preview tạm gắn actor/TTL; lỗi/quota thông báo rõ, không log source/secret. Không auto init schema ở startup; schema business được tạo an toàn trong explicit import nếu tab vắng, header không khớp thì dừng, không overwrite dữ liệu chưa biết.
- Không thêm SQL/SQLite, scope OAuth, MAUI, asset endpoint hoặc deployment. Business/system spreadsheet phải khác nhau; giữ lease và single writer hiện có.

## Acceptance criteria

1. Staging khớp 665/15/98/552/39; ghi chênh lệch nguồn nếu có. Workbook gốc authoritative là Bảng mã(Nháp lần 5).xlsx; nếu chưa có thì không giả chứng nhận metadata gốc.
2. Preview metadata/validation/diff rõ; mọi node DRAFT; reimport không duplicate; version đổi giữ ID và preview tác động.
3. Tree/detail/search/TXT Unicode có indent/path đúng, không lộ nhánh ngoài scope; quyền kiểm phía server cho cả API/Razor.
4. Build + tự động kiểm validation, import/idempotency/conflict, quyền, Google atomic/reconcile và HTTP/Razor liên quan pass; fake không thay Google live/browser.
5. Chỉ DONE khi đủ gate live Google và browser thực; chưa có ghi NOT_RUN, giữ IN_REVIEW.

## Nguồn và kiểm tra ban đầu

AI đếm trực tiếp category-candidates.json: 665 nút; F1=15/F2=98/F3=552; 39 tên nhiều code, không lệch baseline. Không tìm thấy XLSX trong repository kể cả hidden/ignored (loại bin/obj/.git); chỉ có staging từ audit trước. Chưa đối chiếu lại SHA256 và definition F1/F2/F3 của workbook gốc. Không ép dữ liệu hoặc chuyển sang DOCX 13 F1.

## Bằng chứng

Local hoàn tất: build server exit 0, 0 warnings/0 errors; 46/46 test TASK-006 và 12/12 hồi quy scope/API/Razor liên quan, exit 0, failed/skipped 0. 4 lệnh format project file mới exit 0. [Test Report](../07-testing/TEST-REPORT-TASK-006.md) ghi lệnh/expected/actual/nguồn bằng chứng và lịch sử.

Google live và browser: `NOT_RUN`; workbook gốc/hash/definition `NOT_RUN_SOURCE_ORIGINAL`. Không ghi dữ liệu thật, không yêu cầu secret hoặc thao tác UAT. Không coi rendered Razor/fake Google là live/browser pass. Backend workspace đã dừng đúng PID18664 để build và không restart UAT. Chưa DONE; TASK-007 vẫn TODO.

Review sửa thêm trong lượt: lưu ledger ngay cả import no-op để chặn key reuse payload khác; kiểm journal/provenance trước append để không overwrite unknown rows; move giữa version không ghép hai assignment; search/TXT bỏ ancestor ẩn. Local giới hạn 10MB/1000 nút/1800KiB batch/20000 dòng mỗi bảng, lỗi trước write nếu vượt; không silently cắt dữ liệu.
