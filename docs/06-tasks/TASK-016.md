# TASK-016 — Kiểm kê màn hình và quy chuẩn giao diện

- Status: TODO
- Type: DOCS
- Requirement/Plan: [REQ-003](../01-requirements/REQ-003-web-experience.md) / [PLAN-003](../02-architecture/PLAN-003-web-experience.md)
- Thiết kế: [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md)
- Created / Updated: 2026-10-15 (Asia/Saigon)

## Mục tiêu và phạm vi

- Kiểm kê route, field/required/default/validation, action/handler/service, quyền/scope, trạng thái, paging và links/test hiện có. Phân biệt source có code với runtime đã kiểm.
- Đọc đúng HTML gốc khi có, chỉ rút phong cách/cách tổ chức thao tác; ghi nguồn/size/hash metadata, không chép dataset vào repo. Chốt quy chuẩn WEB-UI-DESIGN và bản đồ màn hình.
- Lập gap log tách thiếu backend/contract khỏi thiếu UI; xác định component dùng chung và tiêu chí cụ thể cho TASK-017–021.

## Ngoài phạm vi

Không code UI/backend, không đổi source/data/rule, không mock dashboard hoặc kết luận browser pass. Không dùng phân tích tĩnh cũ thay việc đọc đúng HTML.

## Đầu vào, phụ thuộc và giả định

Source hiện tại TASK-005–011/014/015; cần đúng file QuanLyVatTu_DuyTien_v4_Offline.html để chốt đối chiếu. Không yêu cầu các task cũ đổi DONE.

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML tham chiếu hiện MISSING_REFERENCE; không dùng phân tích cũ làm bằng chứng đã đọc. Rule/nguồn OPEN/TBD giữ nguyên.

## Màn hình bị ảnh hưởng

Toàn bộ Pages/Account, Admin, Categories, Assets, Documents, Files, Items, Stock, Reports, Home/Help/Privacy/Error; shared layout/JS/CSS và /Offline do ConnectedRuntime sinh.

## Acceptance criteria

- [ ] AC-01: Liệt kê đầy đủ route hiện hành/legacy, field/action/contract/permission/error/empty/loading/paging; có bảng đối chiếu source và phần thiếu.
- [ ] AC-02: Đọc đúng HTML, ghi provenance và khác biệt với domain hiện tại; không đưa file/dataset công ty vào Git.
- [ ] AC-03: Quy chuẩn định lượng về header/sidebar/breadcrumb/colors/type/focus/local assets/responsive; mapping từng module sang component.
- [ ] AC-04: Gap backend ghi trigger/actual/expected/contract/permission/phạm vi; không biến assumption OPEN/TBD thành rule.

## Routing

Đã đọc `conductor:context-driven-development` và `ui-design:design-system-patterns` khi **lập kế hoạch**: đồng bộ docs/links/dependencies và quy chuẩn tokens/components. Dùng cấu trúc docs hiện tại, không tạo conductor/ hoặc orchestrator khác. Chưa thực hiện code/kiểm tra của Task này, không subagent. Khi bắt đầu sẽ đọc skill kỹ thuật thật tương ứng phần đổi; không coi tên skill hoặc planned test là đã chạy.

## Các bước

1. Kiểm tra file tham chiếu và trạng thái nguồn; triển khai kiểm kê source độc lập nếu nguồn còn thiếu.
2. Hoàn thiện route/field/action/role/state matrix và inventory tài nguyên local/license.
3. Đối chiếu HTML khi có, cập nhật thiết kế/criteria/gap/dependency.
4. Kiểm links/consistency, ghi đúng gate còn thiếu; không chuyển DONE khi chưa đối chiếu file.

## Cách kiểm và bằng chứng

Lệnh build/filtered tests chọn theo code thực đổi và ghi trong [COMMANDS](../COMMANDS.md) trước khi chạy; build/test tuần tự nếu chung output. Browser khi provider có; không giao thêm UAT cũ hoặc timeout dài. Tách AI trực tiếp/người dùng chạy/người dùng xác nhận/chưa chạy. Mỗi lần ghi expected/actual, exit và tổng test khi quan sát được; fake/fixture/local không là Google/browser pass.

| AC | Cách kiểm dự kiến | Working directory / môi trường | Kết quả hiện tại |
|---|---|---|---|
| AC-01 | Đối chiếu Pages, DTO/services/endpoints và test names; đọc source có mục tiêu | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-02 | File thật và bản ghi đối chiếu; hiện MISSING_REFERENCE/NOT_REVIEWED_HTML | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-03 | Self-review Requirement→design→Task, chưa là UI runtime | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-04 | Review gap log BE/REF/MAP và link các task chịu trách nhiệm | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |

## Review và checkpoint

Mới tạo tài liệu Task trong lượt lập kế hoạch. Review tính nhất quán docs được ghi ở [report lập kế hoạch](../07-testing/TEST-REPORT-WEB-UI-PLANNING.md), không phải nghiệm thu AC của Task này. Chưa code/build/browser/Google hoặc chốt UI theo HTML. TODO giữ; không chuyển DONE khi còn gate bắt buộc. Thiếu backend ghi gap/contract riêng, không tạo nút giả hoặc tự chốt rule.
