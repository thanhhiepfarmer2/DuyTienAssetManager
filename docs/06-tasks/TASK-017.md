> Hiện hành16/10: runtime đã kiểm một phần; trạng thái IN_REVIEW. Xem checkpoint16/10 cuối tài liệu và Report021, các NOT_RUN15/10 là lịch sử.

# TASK-017 — Nền giao diện chung và tài khoản

- Status: IN_REVIEW

> Hiện hành15/10: đã triển khai và kiểm phần local. Matrix hiện hành ở cuối tài liệu và Test Report là kết quả; các bảng/routing “chưa bắt đầu” bên dưới được giữ như checkpoint lập kế hoạch. Browser/Google/mạng thật NOT_RUN, không DONE.
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

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML hiện đã STATIC_VERIFIED đúng bytes/hash ở TASK-016; chỉ tham khảo UI, không thực thi hoặc nhập dataset. Rule/nguồn OPEN/TBD giữ nguyên.

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


## Triển khai15/10 — IN_REVIEW

Đã triển khai header/sidebar/breadcrumb/account panels/localSVG/systemfont/semanticCSS và nhãn metadata Việt. Menu chỉ dùng claims, không thêm I/O/Google; cookie/auth/role/scope giữ. Offline/error HTML dùng CSS local, provenance tách branding; view state bind user/session/authority.

Build Server Debug --no-restore:exit0,0warnings/errors,17.36s. Filter WebUi/AuthApiIntegration/LoginFailureDiagnostics/ConnectedCache:65/65passed,0failed/skipped,exit0,17s. node ui/connection syntax exit0 từng lệnh. Lệnh ghi checkpoint đầu exit1 do Python default cp1252 đọc helperUTF8; sửa encoding rõ, không lỗi ứng dụng hoặc đổi test. Compiled Razor/fixtureHTTP không browser/Google pass.

[Report](../07-testing/TEST-REPORT-TASK-017.md). Phần phụ thuộc local đủ; browser/Google/mạng thật chưa chạy giữ NOT_RUN. Người dùng cho phép tiếp task kế, không đổi DONE để vượt gate. Checkpoint NOT_RUN trước đó là lịch sử.


### Matrix hiện hành sau tích hợp021

AC01–04: shell/local assets/static navigation + account/connection states PASS_LOCAL_HTTP; AC05 keyboard/narrow/loading mới có source/event tests, NOT_RUN_BROWSER. Đọc proof RAM không tăng business I/O theo code review, không Google performance claim.

[Xem tổng hợp021](../07-testing/TEST-REPORT-TASK-021.md). Các bảng NOT_RUN khi lập kế hoạch là lịch sử; trạng thái task vẫn IN_REVIEW. Không mở lại UAT đã hoãn.


## Runtime16/10 — IN_REVIEW

Google auth store thật đọc được3snapshot; user login hiện có thành công (POST302, Home200/admin shell được AI quan sát). Cải thiện auth read không chờ writer khi chưa touch5ph, public assets không auth; cache UI có revision/legacy chỉ đọc. Header/sidebar/Home được quan sát browser desktop; narrow/keyboard/logout/change-password/error và mạng thật chưa kiểm lại.

Bằng chứng, lệnh/exit, giới hạn và backend PID13680/build mới tại [Report021](../07-testing/TEST-REPORT-TASK-021.md) và [JSON runtime](../07-testing/TASK-021-RUNTIME-20261016.json). Suite cuối101/101pass,0failed/skipped,exit0 (16s); Releaseexit0/0warnings/errors3.82s. Không cộng với các suite overlap. Browser tool dừng thì dừng app input, không yêu cầu chuỗi UAT user. Tài khoản/data/auth policy/guard/freshness/pacing giữ; không business writes Google hoặc mobile/Git/deploy.
