# TASK-017 — Nền giao diện chung và tài khoản

- Status: TODO
- Type: FEATURE
- Requirement/Plan: [REQ-003](../01-requirements/REQ-003-web-experience.md) / [PLAN-003](../02-architecture/PLAN-003-web-experience.md)
- Thiết kế: [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md)
- Created / Updated: 2026-10-15 (Asia/Saigon)

## Mục tiêu và phạm vi

- Header xanh/thương hiệu/người dùng/kết nối, sidebar theo module/quyền, breadcrumb/page heading và components theo thiết kế đã chốt.
- Chuẩn hóa form/validation/empty/loading/feedback/focus; login/đổi mật khẩu/logout và offline shell dùng nhận diện thống nhất.
- Giữ local assets, không CDN; dùng quyền/phiên đã được server xác minh và request memo hiện có, không thêm Google calls cho từng component.
- Bỏ branding demo/task khỏi shell, vẫn giữ badge trạng thái/nguồn dữ liệu phù hợp.

## Ngoài phạm vi

Không đổi auth/OAuth scope, min8/session policy, tạo admin hoặc reset dữ liệu, không thêm offline write hoặc sửa hàng loạt logic nghiệp vụ các module.

## Đầu vào, phụ thuộc và giả định

TASK-016; auth TASK-005, ConnectedRuntime TASK-015 v2 và tài khoản hiện có tiếp tục dùng.

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML tham chiếu hiện MISSING_REFERENCE; không dùng phân tích cũ làm bằng chứng đã đọc. Rule/nguồn OPEN/TBD giữ nguyên.

## Màn hình bị ảnh hưởng

_Layout, site.css/layout.css, shared partials, local JS/icons; /Account/Login, ChangePassword, Logout, AccessDenied; /Offline và trạng thái lỗi chung.

## Acceptance criteria

- [ ] AC-01: Header/sidebar/active menu/breadcrumb/return URL và shared components thống nhất trên màn hình đã liệt kê.
- [ ] AC-02: Login cũ, đổi mật khẩu bắt buộc, logout, access denied, cookie/anti-forgery giữ hành vi; error503 có correlation an toàn.
- [ ] AC-03: Font/icon/JS/CSS thiết yếu local; đọc tiếng Việt rõ, keyboard/focus/skip link và sidebar hẹp hoạt động.
- [ ] AC-04: Connection/verified time/missing cache hiển thị đúng; form dirty/UNKNOWN không tự gửi lại hoặc mất dữ liệu, không mở LocalSetup.
- [ ] AC-05: Hình thức mới không tăng Google auth/metadata calls hoặc dùng cache UI để mở quyền.

## Routing

Đã đọc `conductor:context-driven-development` và `ui-design:design-system-patterns` khi **lập kế hoạch**: đồng bộ docs/links/dependencies và quy chuẩn tokens/components. Dùng cấu trúc docs hiện tại, không tạo conductor/ hoặc orchestrator khác. Chưa thực hiện code/kiểm tra của Task này, không subagent. Khi bắt đầu sẽ đọc skill kỹ thuật thật tương ứng phần đổi; không coi tên skill hoặc planned test là đã chạy.

## Các bước

1. Đọc output TASK-016 và baseline auth hiện có; chọn file ownership/layout migration nhỏ.
2. Triển khai tokens/partials/shell và account/offline feedback; giữ server guard.
3. Build/filtered auth/cache/rendered tests, browser nếu khả dụng.
4. Đối chiếu file nguồn/version của cache HTML và asset local, ghi limits và evidence.

## Cách kiểm và bằng chứng

Lệnh build/filtered tests chọn theo code thực đổi và ghi trong [COMMANDS](../COMMANDS.md) trước khi chạy; build/test tuần tự nếu chung output. Browser khi provider có; không giao thêm UAT cũ hoặc timeout dài. Tách AI trực tiếp/người dùng chạy/người dùng xác nhận/chưa chạy. Mỗi lần ghi expected/actual, exit và tổng test khi quan sát được; fake/fixture/local không là Google/browser pass.

| AC | Cách kiểm dự kiến | Working directory / môi trường | Kết quả hiện tại |
|---|---|---|---|
| AC-01 | Compiled Razor/HTTP links và browser interaction khi provider có | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-02 | Filtered auth/Razor/API tests; login PASS_MANUAL cũ là baseline, không tự tính regression pass | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-03 | Source/no-CDN checks và browser desktop/narrow/keyboard; thiếu provider NOT_RUN_BROWSER | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-04 | ConnectedCache regression/HTTP và browser, không gọi fixture là real network | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-05 | So sánh instrumentation/call count với nguồn bằng chứng rõ | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |

## Review và checkpoint

Mới tạo tài liệu Task trong lượt lập kế hoạch. Review tính nhất quán docs được ghi ở [report lập kế hoạch](../07-testing/TEST-REPORT-WEB-UI-PLANNING.md), không phải nghiệm thu AC của Task này. Chưa code/build/browser/Google hoặc chốt UI theo HTML. TODO giữ; không chuyển DONE khi còn gate bắt buộc. Thiếu backend ghi gap/contract riêng, không tạo nút giả hoặc tự chốt rule.
