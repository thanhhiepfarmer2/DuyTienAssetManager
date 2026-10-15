# Danh sách Task

> Hiện hành15/10: người dùng xác nhận login tài khoản hiện có **PASS_MANUAL riêng case này**; TASK-015 giữ IN_REVIEW, không tự đóng gate khác. [PLAN-003](../02-architecture/PLAN-003-web-experience.md) / [REQ-003](../01-requirements/REQ-003-web-experience.md) thêm TASK-016–021 tuần tự, **đều TODO**, lượt này chỉ lập kế hoạch. [Thiết kế](../02-architecture/WEB-UI-DESIGN.md) còn DRAFT do thiếu HTML gốc (NOT_REVIEWED_HTML). Android tạm dừng/iOS DEFERRED_BY_USER. Dòng chẩn đoán login ngay dưới là checkpoint trước xác nhận mới, giữ lịch sử.

> Login fix 15/10: Google thật trả OAuth HTTP400/`invalid_grant` trước Sheets; config Google/Development và guard first-login đã kiểm. Bản sửa log an toàn/CLI read-only +78/78tests, Release sạch; đang chờ consent desktop cùng app `drive.file`, chưa Google auth-read/login PASS. Xem [báo cáo](../07-testing/TEST-REPORT-TASK-015-ONLINE.md). Giữ IN_REVIEW, không reset admin.

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
| TASK-014 | [Nghiệm thu, backup/restore và hướng dẫn](TASK-014.md) | IN_REVIEW | Web strictHTTPS/GoogleDEV fixture đã đạt: selected78local, finish57live exit0; browser/cookie mới/source/restore còn gate; Android tạm dừng/iOS DEFERRED_BY_USER, không chặn Web |
| TASK-015 | [Online mặc định và cache ngoại tuyến chỉ đọc](TASK-015.md) | IN_REVIEW | REQ-002/ADR-002/PLAN-002 v2 thay v1 ngày15/10;46/46local+35/35affected regression+3/3logout exit0; Release sạch/strictHTTPS/public6checks/start-reuse đạt; Google authenticated/network thật/browser/coverage/restore còn gate; Android dừng/iOS hoãn |
| TASK-016 | [Kiểm kê màn hình và quy chuẩn giao diện](TASK-016.md) | TODO | PLAN-003; Source hiện tại; HTML đúng nguồn còn thiếu; chưa bắt đầu |
| TASK-017 | [Nền giao diện chung và tài khoản](TASK-017.md) | TODO | PLAN-003; TASK-016; auth/cache hiện có; chưa bắt đầu |
| TASK-018 | [Hoàn thiện giao diện danh mục và tài sản](TASK-018.md) | TODO | PLAN-003; TASK-017; core TASK-006–008/014; chưa bắt đầu |
| TASK-019 | [Hồ sơ ảnh, import, Items và báo cáo](TASK-019.md) | TODO | PLAN-003; TASK-018; core TASK-009–011; chưa bắt đầu |
| TASK-020 | [Quản trị, trang chủ, hướng dẫn và nội dung](TASK-020.md) | TODO | PLAN-003; TASK-019; core TASK-005; chưa bắt đầu |
| TASK-021 | [Trải nghiệm, online/offline và hiệu năng web](TASK-021.md) | TODO | PLAN-003; TASK-020; REQ-002 v2/TASK-015, evidence riêng; chưa bắt đầu |

TASK-003 mới tạo scaffold và chốt kiến trúc; không có nghĩa TASK-004–TASK-014 đã
được triển khai. AI cập nhật trạng thái bảng cùng trạng thái trong từng Task.
