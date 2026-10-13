# Danh sách Task

| ID | Tên | Trạng thái | Phụ thuộc |
|---|---|---|---|
| TASK-001 | [Kiểm tra repo và tích hợp workflow](TASK-001-bootstrap.md) | DONE | — |
| TASK-002 | [Xác lập yêu cầu và hợp nhất tài liệu](TASK-002.md) | DONE | Không phụ thuộc code |
| TASK-003 | [Chốt kiến trúc và setup môi trường dev](TASK-003.md) | IN_REVIEW | TASK-002; Google evidenced, host/device gates còn mở |
| TASK-004 | [PoC Sheets/Drive, writer và idempotency](TASK-004.md) | DONE | Live Google và manual tab/TXT/control-file có user-provided evidence; TASK-003 vẫn IN_REVIEW |
| TASK-005 | [Đăng nhập, admin và phân quyền server/web](TASK-005.md) | IN_REVIEW | 173/173 live; 78/78 local và 14 boundary riêng; G2a/G2b PASS_MANUAL; idle 30ph thực PASS; UAT còn lại DEFERRED_BY_USER, W13 STOPPED_BY_USER; ngoại lệ cho phép TASK-006 local |
| TASK-006 | [Import cây và xem/tìm danh mục](TASK-006.md) | IN_REVIEW | Ngoại lệ người dùng cho phép khi TASK-005 chưa DONE; build sạch, 46/46 local + 12/12 hồi quy; nguồn XLSX gốc/Google/browser NOT_RUN |
| TASK-007 | [CRUD tài sản trên web](TASK-007.md) | IN_REVIEW | Ngoại lệ local 2026-10-13 khi TASK-006 IN_REVIEW; build sạch, 54/54 task +9/9 hồi quy; rule/nguồn/Google/browser còn gate |
| TASK-008 | [Sửa/chuyển/ngừng cây và di chuyển đối tượng](TASK-008.md) | IN_REVIEW | Ngoại lệ local13/10 khi TASK-005/006/007 IN_REVIEW; build sạch,42/42 task +91/91 hồi quy; nguồn/rule/Google/browser chưa đạt |
| TASK-009 | [Drive ảnh và biểu mẫu DOCX/PDF](TASK-009.md) | IN_REVIEW | Ngoại lệ13/10 khi TASK-005–008 IN_REVIEW; build sạch56/56 task+67/67 hồi quy; mẫu/Drive/live/browser còn gate, guard giữ |
| TASK-010 | [Import Items và snapshot số lượng](TASK-010.md) | IN_REVIEW | Ngoại lệ13/10 khi TASK-005–009 IN_REVIEW; build sạch66/66 task+8/8 UI cuối+46/46 hồi quy; nguồn/stock/ngưỡng/Google/browser còn gate; trước code463896d đã push |
| TASK-011 | [Báo cáo tài sản và tồn hết/sắp hết](TASK-011.md) | IN_REVIEW | Ngoại lệ13/10 local khi TASK-007/010 IN_REVIEW; build sạch78/78 report+36/36 hồi quy; nguồn/stock/ngưỡng/Google/browser chưa đạt |
| TASK-012 | [App Android: nhập liệu và chụp ảnh](TASK-012.md) | IN_REVIEW | Ngoại lệ13/10 local khi TASK-005/007/009 IN_REVIEW; Android build sạch,42/42 tests; device/UI/camera/OS storage/TLS/Google NOT_RUN; phát triển/runtime DEFERRED_BY_USER14/10 tới sau Web |
| TASK-013 | [App iOS: nhập liệu và chụp ảnh](TASK-013.md) | TODO | DEFERRED_BY_USER14/10: iOS thiếu môi trường/thiết bị; không chặn Web |
| TASK-014 | [Nghiệm thu, backup/restore và hướng dẫn](TASK-014.md) | IN_REVIEW | Pha Web: build sạch, 126/126 checks local; HTTPS runtime BLOCKED_TLS_TRUST, browser/Google/source/restore chưa đạt gate; Android sau, iOS DEFERRED_BY_USER, không chặn Web |

TASK-003 mới tạo scaffold và chốt kiến trúc; không có nghĩa TASK-004–TASK-014 đã
được triển khai. AI cập nhật trạng thái bảng cùng trạng thái trong từng Task.
