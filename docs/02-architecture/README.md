# Điều hướng kiến trúc hiện hành — 2026-10-15

Xem [PROJECT-CONTEXT](../PROJECT-CONTEXT.md)/[INDEX](../06-tasks/INDEX.md) để biết trạng thái mới nhất. Kiến trúc giữ .NET10/Razor/Sheets/Drive/drive.file và single-writer; [REQ-002 v2](../01-requirements/REQ-002-local-offline.md)/[ADR-002](ADR-002-local-offline.md) thay LocalOffline ghi độc lập. Người dùng xác nhận login tài khoản hiện có PASS_MANUAL, không là các gate khác PASS.

Trải nghiệm web: [REQ-003](../01-requirements/REQ-003-web-experience.md), [PLAN-003](PLAN-003-web-experience.md), [WEB-UI-DESIGN DRAFT](WEB-UI-DESIGN.md), TASK-016–021 TODO; thiếu HTML gốc nên chưa đối chiếu reference. Android tạm dừng/iOS hoãn. Bảng scaffold dưới đây giữ như checkpoint lịch sử, không phải tình trạng triển khai hiện tại.

---

# Kiến trúc

[ADR-001](ADR-001-proposed-sheets-drive-web-mobile.md) đã được chấp nhận ngày
2026-10-06. Scaffold local đã tồn tại; Google dev có user-run live evidence cho
TASK-004, còn hạ tầng public chưa được chọn.

| Phần | Quyết định | Trạng thái triển khai |
|---|---|---|
| Backend/API | ASP.NET Core .NET 10, một host/instance cho demo | Auth/admin API và status endpoint đã build/test local |
| Web | Razor Pages cùng backend, HTTPS | Login/đổi mật khẩu/admin user-role-scope đã build/test local; live Google chưa chạy |
| Mobile | .NET MAUI, target Android và iOS riêng | Cả hai target compile; chưa chạy thiết bị |
| Database | Google Sheets qua backend single-writer | REST adapter + atomic PoC/fault tests local; live PoC và manual tab user-confirmed pass |
| File | Thư mục My Drive của tài khoản vận hành | REST upload/download + state/reconcile; live checksum/AVAILABLE và manual folder/file user-confirmed pass |
| Auth tích hợp | Google OAuth của tài khoản vận hành, tách app login | Desktop PKCE/loopback, token scope fail-closed; live 200/404 và manual control-file user-confirmed pass |
| Auth ứng dụng | Backend quản lý identity; web cookie, mobile opaque access/refresh | [Thiết kế TASK-005](AUTH-DESIGN-PROPOSED.md) đã `ACCEPTED`; implementation local/test đã có, system spreadsheet live chưa tạo/chọn/ghi |
| Deploy | Dùng trong công ty và từ Internet qua HTTPS | Host/domain/TLS/reverse proxy `TBD`; chưa deploy |

Không thêm SQL/SQLite thay Google Sheets. Auth web/CLI dùng chung file lock liên
tiến trình trên một host; lock RAM chỉ tuần tự hóa request trong process. Không
scale-out nhiều backend instance trước khi có coordinator dùng chung. Xem tên cấu
hình và cách giữ secret tại [CONFIGURATION](../CONFIGURATION.md).
