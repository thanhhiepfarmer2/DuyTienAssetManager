# TEST-REPORT — TASK-019

2026-10-15 Asia/Saigon. AI chạy/quan sát trực tiếp, repo D:/Dev/QuanLyTaiSan_DuyTien.

Thống nhất ảnh/hồ sơ, import vật tư20cột, số lượng và R01–R04: form/bảng/nhãn trạng thái/lỗi, xác nhận thao tác thật, chọn tệp/preview PNG/JPEG/PDF và trạng thái tải. Không thêm nút cancel-job vì backend chưa có contract. Sửa tích hợp cache gateway Razor /Files/Content: bytes và query được phân biệt chính xác; link coverage kiểm pathname+query, không báo file sẵn có khi thiếu bytes. Guard/proof/quyền không nới.

AI chạy trực tiếp filtered tests theo COMMANDS: WebUi|ManagedMediaHttpRazorTests|InventoryHttpRazorTests|ReportHttpRazorTests|ConnectedCacheTests. Cuối 89/89passed,0failed/skipped,exit0,17s. Trước đó compile exit1 do thay nhầm field Tracking (đã khôi phục); các lần89cases:85pass/4fail,87pass/2fail do nhãn UI cũ;88pass/1fail do assertion mới suy tracking=null trong fixture thực true (sửa kiểm không sinh snapshot). Không skip test; giữ test validation/CSRF/scopes/version/retry/nullable. Node check ui/media/connection và git diff --check exit0. Compiled Razor/Kestrel + fixture GIẢ/fake transport/filesystem cache, không Google/browser bằng chứng.

IN_REVIEW. Fixture/Razor HTTP không browser/Google PASS. Browser getState rỗng, iab unavailable; NOT_RUN_BROWSER. Không thay account/Google/data/guard/policy, không UAT hoãn/timeout/mobile/commit/push/deploy. Chỉ local đủ cho phụ thuộc theo ngoại lệ user.


## Bổ sung runtime16/10

Không có browser evidence mới cho ảnh/hồ sơ/import/Items/Stock/Reports/download. Sửa shared public assets loại auth Google thừa và cache HTML version/legacy áp dụng chung, có regression liên quan; không biến checks module giả trước đây thành Google/browser PASS.

Nguồn và kết quả chung: [Report021](TEST-REPORT-TASK-021.md), [runtime JSON](TASK-021-RUNTIME-20261016.json). Local101/101pass/exit0 và Releaseexit0; browser chỉ Home/Danh mục trước sửa cuối, không phải mọi module/Google/network PASS. Task giữ IN_REVIEW; evidence15/10 phía trên giữ lịch sử.
