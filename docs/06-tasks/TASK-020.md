# TASK-020 — Quản trị, trang chủ, hướng dẫn và nội dung toàn web

- Status: TODO
- Type: FEATURE
- Requirement/Plan: [REQ-003](../01-requirements/REQ-003-web-experience.md) / [PLAN-003](../02-architecture/PLAN-003-web-experience.md)
- Thiết kế: [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md)
- Created / Updated: 2026-10-15 (Asia/Saigon)

## Mục tiêu và phạm vi

- Trình bày tài khoản/role/scope/effective dates/descendants/audit thống nhất; xác nhận khóa/reset/đổi quyền, giữ bảo vệ admin cuối và revoke.
- Home là điểm vào công việc theo quyền, dùng links/action thật; chỉ dùng thông tin từ nguồn backend đã xác minh trong scope, không thêm KPI nếu chưa có contract.
- Viết lại nội dung giới thiệu/hướng dẫn thành ngôn ngữ công việc; bỏ demo/PoC/scaffold/task từ branding/copy thông thường.
- Tách nhãn trạng thái/nguồn thử nghiệm/chưa duyệt khỏi giới thiệu ứng dụng; hướng dẫn vận hành kỹ thuật đúng đối tượng và evidence.

## Ngoài phạm vi

Không thay password/session/role model, reset admin để test, tạo users công ty, phê duyệt nguồn hoặc sinh dashboard số liệu. Không xóa trạng thái/audit/history/raw enums vì content scan.

## Đầu vào, phụ thuộc và giả định

TASK-019; auth/admin/scope TASK-005 và nội dung/bằng chứng mới từ TASK-015.

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML tham chiếu hiện MISSING_REFERENCE; không dùng phân tích cũ làm bằng chứng đã đọc. Rule/nguồn OPEN/TBD giữ nguyên.

## Màn hình bị ảnh hưởng

/Admin/Users, Detail, Authorization, SecurityEvents; /, /Help, /Privacy, /Error; headings/menu/footer/messages toàn web và WEB-USER-GUIDE khi liên quan.

## Acceptance criteria

- [ ] AC-01: Users/role/scope/audit form/permission/errors rõ; server chặn Viewer/admin cuối/revoke và chống CSRF như core.
- [ ] AC-02: Home điều hướng theo quyền và mọi link/action có thực; không có counter/card giả hoặc lượt gọi trung tâm không cần thiết.
- [ ] AC-03: Toàn bộ copy ứng dụng được rà theo inventory, không product demo/task branding; badge DRAFT/fixture/source vẫn đúng.
- [ ] AC-04: Help/privacy/error giải thích thao tác/thiếu cache/offline/service/conflict ngắn, không đưa thông tin triển khai cho người dùng thường.
- [ ] AC-05: Keyboard/narrow/loading/empty/dangerous confirmation ổn định và login hiện có không hồi quy.

## Routing

Đã đọc `conductor:context-driven-development` và `ui-design:design-system-patterns` khi **lập kế hoạch**: đồng bộ docs/links/dependencies và quy chuẩn tokens/components. Dùng cấu trúc docs hiện tại, không tạo conductor/ hoặc orchestrator khác. Chưa thực hiện code/kiểm tra của Task này, không subagent. Khi bắt đầu sẽ đọc skill kỹ thuật thật tương ứng phần đổi; không coi tên skill hoặc planned test là đã chạy.

## Các bước

1. Rà scope/field/actions quản trị, tái sử dụng core và components.
2. Hoàn thiện home/help/privacy/error; không vẽ dashboard trước contract.
3. Content sweep toàn bộ screens kể cả error/offline dynamic HTML theo glossary.
4. Build/filtered checks, cập nhật guide và source labels; giữ các gate lịch sử.

## Cách kiểm và bằng chứng

Lệnh build/filtered tests chọn theo code thực đổi và ghi trong [COMMANDS](../COMMANDS.md) trước khi chạy; build/test tuần tự nếu chung output. Browser khi provider có; không giao thêm UAT cũ hoặc timeout dài. Tách AI trực tiếp/người dùng chạy/người dùng xác nhận/chưa chạy. Mỗi lần ghi expected/actual, exit và tổng test khi quan sát được; fake/fixture/local không là Google/browser pass.

| AC | Cách kiểm dự kiến | Working directory / môi trường | Kết quả hiện tại |
|---|---|---|---|
| AC-01 | Admin/Razor/Auth tests liên quan, fixture GIẢ; không lặp UAT cũ | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-02 | Route/rendered checks, scoped data tests nếu có số liệu thật được tái sử dụng | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-03 | Context-aware content review/allowlist, không grep-xóa raw trạng thái | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-04 | Doc/content/HTTP+browser checks; operator docs giữ kỹ thuật cần thiết | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-05 | Filtered tests và browser khi có, thiếu bằng chứng ghi NOT_RUN | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |

## Review và checkpoint

Mới tạo tài liệu Task trong lượt lập kế hoạch. Review tính nhất quán docs được ghi ở [report lập kế hoạch](../07-testing/TEST-REPORT-WEB-UI-PLANNING.md), không phải nghiệm thu AC của Task này. Chưa code/build/browser/Google hoặc chốt UI theo HTML. TODO giữ; không chuyển DONE khi còn gate bắt buộc. Thiếu backend ghi gap/contract riêng, không tạo nút giả hoặc tự chốt rule.
