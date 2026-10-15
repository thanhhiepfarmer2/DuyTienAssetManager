# TASK-018 — Hoàn thiện giao diện danh mục và tài sản

- Status: TODO
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

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML tham chiếu hiện MISSING_REFERENCE; không dùng phân tích cũ làm bằng chứng đã đọc. Rule/nguồn OPEN/TBD giữ nguyên.

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
