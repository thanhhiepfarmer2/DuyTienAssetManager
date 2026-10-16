> Hiện hành16/10: runtime đã kiểm một phần; trạng thái IN_REVIEW. Xem checkpoint16/10 cuối tài liệu và Report021, các NOT_RUN15/10 là lịch sử.

# TASK-019 — Hoàn thiện hồ sơ ảnh, import, Items và báo cáo

- Status: IN_REVIEW

> Hiện hành15/10: đã triển khai và kiểm phần local. Matrix hiện hành ở cuối tài liệu và Test Report là kết quả; các bảng/routing “chưa bắt đầu” bên dưới được giữ như checkpoint lập kế hoạch. Browser/Google/mạng thật NOT_RUN, không DONE.
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

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML hiện đã STATIC_VERIFIED đúng bytes/hash ở TASK-016; chỉ tham khảo UI, không thực thi hoặc nhập dataset. Rule/nguồn OPEN/TBD giữ nguyên.

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


## Triển khai15/10 — IN_REVIEW

Thống nhất ảnh/hồ sơ, import vật tư20cột, số lượng và R01–R04: form/bảng/nhãn trạng thái/lỗi, xác nhận thao tác thật, chọn tệp/preview PNG/JPEG/PDF và trạng thái tải. Không thêm nút cancel-job vì backend chưa có contract. Sửa tích hợp cache gateway Razor /Files/Content: bytes và query được phân biệt chính xác; link coverage kiểm pathname+query, không báo file sẵn có khi thiếu bytes. Guard/proof/quyền không nới.

AI chạy trực tiếp filtered tests theo COMMANDS: WebUi|ManagedMediaHttpRazorTests|InventoryHttpRazorTests|ReportHttpRazorTests|ConnectedCacheTests. Cuối 89/89passed,0failed/skipped,exit0,17s. Trước đó compile exit1 do thay nhầm field Tracking (đã khôi phục); các lần89cases:85pass/4fail,87pass/2fail do nhãn UI cũ;88pass/1fail do assertion mới suy tracking=null trong fixture thực true (sửa kiểm không sinh snapshot). Không skip test; giữ test validation/CSRF/scopes/version/retry/nullable. Node check ui/media/connection và git diff --check exit0. Compiled Razor/Kestrel + fixture GIẢ/fake transport/filesystem cache, không Google/browser bằng chứng.

[Report](../07-testing/TEST-REPORT-TASK-019.md). Phần phụ thuộc local đủ; browser/Google/mạng thật chưa chạy giữ NOT_RUN. Người dùng cho phép tiếp task kế, không đổi DONE để vượt gate. Checkpoint NOT_RUN trước đó là lịch sử.


### Matrix hiện hành sau tích hợp021

AC01–05 phần độc lập PASS_LOCAL_HTTP: managed media/20cột/nullable units/source/CSV/querybytes/cache. Preview/giữ File/trạng thái/race PASS_LOCAL_NODE021. Real browser/download/Drive/quota nguồn NOT_RUN, không task DONE.

[Xem tổng hợp021](../07-testing/TEST-REPORT-TASK-021.md). Các bảng NOT_RUN khi lập kế hoạch là lịch sử; trạng thái task vẫn IN_REVIEW. Không mở lại UAT đã hoãn.


## Runtime16/10 — IN_REVIEW

Không có browser evidence mới cho ảnh/hồ sơ/import/Items/Stock/Reports/download. Sửa shared public assets loại auth Google thừa và cache HTML version/legacy áp dụng chung, có regression liên quan; không biến checks module giả trước đây thành Google/browser PASS.

Bằng chứng, lệnh/exit, giới hạn và backend PID13680/build mới tại [Report021](../07-testing/TEST-REPORT-TASK-021.md) và [JSON runtime](../07-testing/TASK-021-RUNTIME-20261016.json). Suite cuối101/101pass,0failed/skipped,exit0 (16s); Releaseexit0/0warnings/errors3.82s. Không cộng với các suite overlap. Browser tool dừng thì dừng app input, không yêu cầu chuỗi UAT user. Tài khoản/data/auth policy/guard/freshness/pacing giữ; không business writes Google hoặc mobile/Git/deploy.
