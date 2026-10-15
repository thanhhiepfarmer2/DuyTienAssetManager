# TASK-019 — Hoàn thiện hồ sơ ảnh, import, Items và báo cáo

- Status: TODO
- Type: FEATURE
- Requirement/Plan: [REQ-003](../01-requirements/REQ-003-web-experience.md) / [PLAN-003](../02-architecture/PLAN-003-web-experience.md)
- Thiết kế: [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md)
- Created / Updated: 2026-10-15 (Asia/Saigon)

## Mục tiêu và phạm vi

- Ảnh/hồ sơ/version/DOCX/PDF: chọn file, preview, tiến trình, trạng thái, primary/description, publish/archive hiện có; tải/mở qua backend gateway.
- Import cây/Items: mapping/validation/diff/trùng tên hoặc code, staging/chunk/resume theo contract hiện có; không giả có nút hủy job nếu backend chưa hỗ trợ, lỗi từng bước và không nhân đôi.
- Snapshot: quantity/minimum nullable, nguồn/đơn vị/asOf, trạng thái theo dõi rõ; Items không trở thành Assets.
- R01–R04 filter/aggregation/page/CSV theo scope, Assets count khác Items, không cộng khác đơn vị; nguồn/UNKNOWN/missing threshold không bị che.

## Ngoài phạm vi

Không report PROPOSED, format export mới, rule stock/ngưỡng mới hoặc phê duyệt workbook; không upload thẳng Drive, file AVAILABLE giả/offline write hoặc thêm state/schema để làm đẹp.

## Đầu vào, phụ thuộc và giả định

TASK-018; Media TASK-009, Inventory TASK-010, Reports TASK-011, auth/cache/registry hiện có.

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML tham chiếu hiện MISSING_REFERENCE; không dùng phân tích cũ làm bằng chứng đã đọc. Rule/nguồn OPEN/TBD giữ nguyên.

## Màn hình bị ảnh hưởng

/Assets/Photos/{id}, /Documents và Detail, /Files/Content/{id}, /Items và Detail/Import, /Stock, /Reports cùng partial/filter JS liên quan.

## Acceptance criteria

- [ ] AC-01: Preview/upload/reconcile/cancel/retry/version/download hoạt động theo contract và scope; pending/failed/unknown rõ, cùng key không file trùng.
- [ ] AC-02: Import preview/error/staging/resume/diff đúng, cùng nguồn không bản sao; source12cột tham chiếu không ép thành VTHH20cột.
- [ ] AC-03: 0/LOW/UNKNOWN/nontracked/missing minimum/source thể hiện khác nhau; asOf/unit nullable không suy tồn từ BOM/kho.
- [ ] AC-04: R01–R04 scope trước tổng/count/page/export, không grand quantity khác unit hoặc branch leak.
- [ ] AC-05: Offline file chỉ mở khi có bytes cache hợp lệ; form lỗi giữ dữ liệu/key, input file mất bytes được báo đúng; mọi trạng thái UI có keyboard/empty/error.

## Routing

Đã đọc `conductor:context-driven-development` và `ui-design:design-system-patterns` khi **lập kế hoạch**: đồng bộ docs/links/dependencies và quy chuẩn tokens/components. Dùng cấu trúc docs hiện tại, không tạo conductor/ hoặc orchestrator khác. Chưa thực hiện code/kiểm tra của Task này, không subagent. Khi bắt đầu sẽ đọc skill kỹ thuật thật tương ứng phần đổi; không coi tên skill hoặc planned test là đã chạy.

## Các bước

1. Đọc contract thực và inventory từ TASK-016, thống nhất components với017/018.
2. Hoàn thiện từng luồng media→import→Items/snapshot→report bằng backend hiện có.
3. Kiểm errors/UNKNOWN/retry/reconcile và source labels, không fake success.
4. Build/filtered tests + browser khả dụng; tách evidence Google/browser/fixture/NOT_RUN.

## Cách kiểm và bằng chứng

Lệnh build/filtered tests chọn theo code thực đổi và ghi trong [COMMANDS](../COMMANDS.md) trước khi chạy; build/test tuần tự nếu chung output. Browser khi provider có; không giao thêm UAT cũ hoặc timeout dài. Tách AI trực tiếp/người dùng chạy/người dùng xác nhận/chưa chạy. Mỗi lần ghi expected/actual, exit và tổng test khi quan sát được; fake/fixture/local không là Google/browser pass.

| AC | Cách kiểm dự kiến | Working directory / môi trường | Kết quả hiện tại |
|---|---|---|---|
| AC-01 | Media API/Razor/failure/idempotency tests và browser file interaction | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-02 | Category/Inventory import tests liên quan; fixture có nhãn GIẢ | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-03 | Inventory/Report behavior+HTTP tests, không nguồn company pass | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-04 | Report authorization/count/export/CSV checks; không thêm reports | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-05 | ConnectedCache/media/Razor checks + browser; không coi rendered là browser | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |

## Review và checkpoint

Mới tạo tài liệu Task trong lượt lập kế hoạch. Review tính nhất quán docs được ghi ở [report lập kế hoạch](../07-testing/TEST-REPORT-WEB-UI-PLANNING.md), không phải nghiệm thu AC của Task này. Chưa code/build/browser/Google hoặc chốt UI theo HTML. TODO giữ; không chuyển DONE khi còn gate bắt buộc. Thiếu backend ghi gap/contract riêng, không tạo nút giả hoặc tự chốt rule.
