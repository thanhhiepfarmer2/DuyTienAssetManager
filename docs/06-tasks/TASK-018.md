> Hiện hành16/10: runtime đã kiểm một phần; trạng thái IN_REVIEW. Xem checkpoint16/10 cuối tài liệu và Report021, các NOT_RUN15/10 là lịch sử.

# TASK-018 — Hoàn thiện giao diện danh mục và tài sản

- Status: IN_REVIEW

> Hiện hành15/10: đã triển khai và kiểm phần local. Matrix hiện hành ở cuối tài liệu và Test Report là kết quả; các bảng/routing “chưa bắt đầu” bên dưới được giữ như checkpoint lập kế hoạch. Browser/Google/mạng thật NOT_RUN, không DONE.
- Type: FEATURE
- Requirement/Plan: [REQ-003](../01-requirements/REQ-003-web-experience.md) / [PLAN-003](../02-architecture/PLAN-003-web-experience.md)
- Thiết kế: [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md)
- Created / Updated: 2026-10-15 (Asia/Saigon)

## Mục tiêu và phạm vi

- Duyệt cây scoped, search/filter/page, metadata/detail/TXT Unicode; giữ vị trí/mở cây/filter/scroll phù hợp.
- Asset list/detail/create/edit/soft-delete/restore dùng các field/validation thật; nhãn DRAFT_DEMO/nguồn đúng, không biến thành dữ liệu chính thức.
- Preview/impact/diff/confirm/commit cho sửa/chuyển/ngừng cây và MOVE_ASSETS nhiều AssetIds hiện có; trình bày selection rõ, quyền nguồn/đích đúng.
- Validation/conflict/guard/service error giữ payload/version/key và context; hành động nguy hiểm có xác nhận.

## Ngoài phạm vi

Không tự thêm bulk create/delete/export hoặc nghiệp vụ còn thiếu contract; không đổi ID/mã/counter, duyệt DRAFT/rule nguồn hoặc tắt metadata guard. Ảnh đầy đủ thuộc TASK-019.

## Đầu vào, phụ thuộc và giả định

TASK-017; CategoryCatalog/AssetCrud/TreeMutation và full registry reconciliation hiện có từ TASK-006–008/014.

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML hiện đã STATIC_VERIFIED đúng bytes/hash ở TASK-016; chỉ tham khảo UI, không thực thi hoặc nhập dataset. Rule/nguồn OPEN/TBD giữ nguyên.

## Màn hình bị ảnh hưởng

/Categories, Detail, Import, Manage; /Assets, Create, Detail, Move; _AssetFields/_TreeChangePanel và các links vào ảnh.

## Acceptance criteria

- [ ] AC-01: Cây/search/filter/list/detail/TXT có cùng điều hướng, paging/context và không lộ nhánh/path/count ngoài scope.
- [ ] AC-02: Form create/edit giữ nội dung khi422/409/503, nhãn field/validation đúng binder/service; server giữ version/idempotency.
- [ ] AC-03: Preview/impact/confirm/bulk move hiện có hoạt động; cùng key không trùng, stale preview bị chặn, source/destination đều scope.
- [ ] AC-04: DRAFT/demo/nguồn chưa duyệt vẫn được nhận biết qua nhãn dữ liệu, không product demo branding.
- [ ] AC-05: Empty/loading/errors/hẹp/keyboard và back về filter/selection an toàn theo user/workspace.

## Routing

Đã đọc `conductor:context-driven-development` và `ui-design:design-system-patterns` khi **lập kế hoạch**: đồng bộ docs/links/dependencies và quy chuẩn tokens/components. Dùng cấu trúc docs hiện tại, không tạo conductor/ hoặc orchestrator khác. Chưa thực hiện code/kiểm tra của Task này, không subagent. Khi bắt đầu sẽ đọc skill kỹ thuật thật tương ứng phần đổi; không coi tên skill hoặc planned test là đã chạy.

## Các bước

1. Đối chiếu inventory/contract/permission từng field và bulk action hiện có.
2. Hoàn thiện tree/list/detail/form/impact và giữ context, không sửa domain semantics.
3. Bổ sung tests theo thay đổi, build/test tuần tự; browser nếu có.
4. Ghi gap thiếu backend riêng và bằng chứng local/browser/live đúng nguồn.

## Cách kiểm và bằng chứng

Lệnh build/filtered tests chọn theo code thực đổi và ghi trong [COMMANDS](../COMMANDS.md) trước khi chạy; build/test tuần tự nếu chung output. Browser khi provider có; không giao thêm UAT cũ hoặc timeout dài. Tách AI trực tiếp/người dùng chạy/người dùng xác nhận/chưa chạy. Mỗi lần ghi expected/actual, exit và tổng test khi quan sát được; fake/fixture/local không là Google/browser pass.

| AC | Cách kiểm dự kiến | Working directory / môi trường | Kết quả hiện tại |
|---|---|---|---|
| AC-01 | Category/Asset HTTP+scope tests; browser khi có | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-02 | Filtered CRUD/Razor/conflict/retry tests và browser form | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-03 | TreeMutation/registry tests liên quan; không bypass guard để có happy path | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-04 | Content checks dựa context và regression data enums/IDs | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-05 | Browser interaction; nếu không chạy ghi NOT_RUN_BROWSER | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |

## Review và checkpoint

Mới tạo tài liệu Task trong lượt lập kế hoạch. Review tính nhất quán docs được ghi ở [report lập kế hoạch](../07-testing/TEST-REPORT-WEB-UI-PLANNING.md), không phải nghiệm thu AC của Task này. Chưa code/build/browser/Google hoặc chốt UI theo HTML. TODO giữ; không chuyển DONE khi còn gate bắt buộc. Thiếu backend ghi gap/contract riêng, không tạo nút giả hoặc tự chốt rule.


## Triển khai15/10 — IN_REVIEW

Danh mục có cây thụt cấp/thu gọn phần con thực trong60rows, tìm/xóa lọc/page và context theo phiên/quyền. Assets list/detail/create/edit/lifecycle, chuyển nhiều AssetIds/impact/confirm giữ nguyên backend và input/key/version; nhãn Việt và provenance dữ liệu riêng. Không thêm bulk khác/KPI hoặc tắt guard.

dotnet test filterWebUi/AssetCrudHttpRazor/CategoryHttpRazor/TreeMutationHttpRazor biên dịch Server: vòng đầu33total/32pass/1fail exit1 do assertion copy cũ snapshot DRAFT. Đổi assertion sang câu Việt mới và bổ sung assert core nodes vẫn DRAFT; không giảm behaviorchecks. Thêm integration form/confirmation/source test; vòng cuối34/34pass,0failed/skipped,exit0,6s. node ui.js exit0. Browser collapse/keyboard/layout NOT_RUN, không tự PASS.

[Report](../07-testing/TEST-REPORT-TASK-018.md). Phần phụ thuộc local đủ; browser/Google/mạng thật chưa chạy giữ NOT_RUN. Người dùng cho phép tiếp task kế, không đổi DONE để vượt gate. Checkpoint NOT_RUN trước đó là lịch sử.


### Matrix hiện hành sau tích hợp021

AC01–04: tree/filter/page/asset form/impact/lifecycle và server scope/guard/CSRF/version/idempotency PASS_LOCAL_HTTP; treefold/confirmation PASS_LOCAL_NODE021. Visual/focus/scroll/Back/download trong browser NOT_RUN.

[Xem tổng hợp021](../07-testing/TEST-REPORT-TASK-021.md). Các bảng NOT_RUN khi lập kế hoạch là lịch sử; trạng thái task vẫn IN_REVIEW. Không mở lại UAT đã hoãn.


## Runtime16/10 — IN_REVIEW

AI browser quan sát Danh mục Google DEV (hai gốc GIẢ có sẵn), phát hiện literal F@n.FLevel và breadcrumb lặp; sửa list/detail/impact và shared root breadcrumb, compiled Razor regression đạt. Assets Google GET200 có telemetry, chưa visual PASS. Tree fold/filter/form/conflict/narrow và browser sau bản sửa cuối chưa quan sát do tool stop URL-confidence.

Bằng chứng, lệnh/exit, giới hạn và backend PID13680/build mới tại [Report021](../07-testing/TEST-REPORT-TASK-021.md) và [JSON runtime](../07-testing/TASK-021-RUNTIME-20261016.json). Suite cuối101/101pass,0failed/skipped,exit0 (16s); Releaseexit0/0warnings/errors3.82s. Không cộng với các suite overlap. Browser tool dừng thì dừng app input, không yêu cầu chuỗi UAT user. Tài khoản/data/auth policy/guard/freshness/pacing giữ; không business writes Google hoặc mobile/Git/deploy.
