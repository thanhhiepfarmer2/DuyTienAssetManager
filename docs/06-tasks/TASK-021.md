# TASK-021 — Rà soát trải nghiệm, online offline và hiệu năng web

- Status: TODO
- Type: FEATURE
- Requirement/Plan: [REQ-003](../01-requirements/REQ-003-web-experience.md) / [PLAN-003](../02-architecture/PLAN-003-web-experience.md)
- Thiết kế: [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md)
- Created / Updated: 2026-10-15 (Asia/Saigon)

## Mục tiêu và phạm vi

- Rà luồng hoàn chỉnh, desktop/narrow/keyboard/focus, validation/loading/empty/error/409/503 và xác nhận thao tác nguy hiểm; sửa lỗi tích hợp trong scope web đã duyệt.
- Kiểm first login online, offline chỉ đọc/ghi API trực tiếp bị chặn, reconnect xác minh phiên/SecurityVersion/scope/freshness trước ghi; revoke/expiry/logout/đổi user/restart/cache thiếu/bytes thiếu/UNKNOWN.
- Đo startup/cold/warm online và offline riêng; auth/Google calls/pacing/coordinator/deserialize/registry/render; nguồn fixture, Google thật và browser tách rõ.
- Tổng kết mức sẵn sàng dùng web và giới hạn; không tự chuyển Android hoặc iOS.

## Ngoài phạm vi

Không mở UAT cũ/timeout dài, yêu cầu thiết bị/secret UAT, Google writes thật hoặc test live chưa được cho phép. Không giảm pacing/bỏ guard/scope/freshness, sync offline write hoặc dùng fake timing chứng minh Google nhanh.

## Đầu vào, phụ thuộc và giả định

TASK-020; bản tích hợp TASK-017–020 và hành vi REQ-002/ADR-002 v2/TASK-015. Các gate cũ vẫn theo nguồn bằng chứng riêng.

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML tham chiếu hiện MISSING_REFERENCE; không dùng phân tích cũ làm bằng chứng đã đọc. Rule/nguồn OPEN/TBD giữ nguyên.

## Màn hình bị ảnh hưởng

Tất cả module/web flows, layout/auth, /Offline/connection UI, gateway file, dirty forms, cross-user navigation và launch/runtime web hiện có.

## Acceptance criteria

- [ ] AC-01: Có matrix route/role/error/interaction và browser evidence thực khi provider có, missing case ghi riêng; không coi build/HTTP rendered là browser.
- [ ] AC-02: Online→offline read-only→reconnect đúng, API mutation fail closed; revoke/expired/change-user/missing cache/file/UNKNOWN không leak hoặc retry mù.
- [ ] AC-03: Có cold/warm p50/p95, sample count/environment/fixture/Google calls; so với baseline cùng nguồn; mục tiêu warm GETp95<500ms ghi actual/đạt/chưa đạt.
- [ ] AC-04: File gateway/preview/download và form dirty/double-submit/conflict giữ quyền, data và key hợp lệ.
- [ ] AC-05: Report tổng kết từng gate, chưa xác minh/OPEN-TBD và cách chạy; không Task DONE nếu thiếu gate bắt buộc hoặc chuyển sang mobile tự động.

## Routing

Đã đọc `conductor:context-driven-development` và `ui-design:design-system-patterns` khi **lập kế hoạch**: đồng bộ docs/links/dependencies và quy chuẩn tokens/components. Dùng cấu trúc docs hiện tại, không tạo conductor/ hoặc orchestrator khác. Chưa thực hiện code/kiểm tra của Task này, không subagent. Khi bắt đầu sẽ đọc skill kỹ thuật thật tương ứng phần đổi; không coi tên skill hoặc planned test là đã chạy.

## Các bước

1. Đọc diff và evidence các task trước, xác định cases liên quan và khả năng browser/runtime.
2. Thực hiện checks tự động và browser nếu có; không bắt user chạy chuỗi UAT.
3. Đo có phase/source/percentile thực, sửa regressions và retest đúng phần đổi.
4. Tổng kết web/gates/cách chạy và đề xuất bước sau; Android chỉ sau quyết định riêng.

## Cách kiểm và bằng chứng

Lệnh build/filtered tests chọn theo code thực đổi và ghi trong [COMMANDS](../COMMANDS.md) trước khi chạy; build/test tuần tự nếu chung output. Browser khi provider có; không giao thêm UAT cũ hoặc timeout dài. Tách AI trực tiếp/người dùng chạy/người dùng xác nhận/chưa chạy. Mỗi lần ghi expected/actual, exit và tổng test khi quan sát được; fake/fixture/local không là Google/browser pass.

| AC | Cách kiểm dự kiến | Working directory / môi trường | Kết quả hiện tại |
|---|---|---|---|
| AC-01 | Browser tự động trên fixture/DEV được phép; NOT_RUN_BROWSER nếu không có provider | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-02 | Automated fake transport/filesystem + real network riêng khi được phép; không chờ timeout dài | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-03 | Benchmark runner/instrumentation; local/online Google/offline đo riêng, không tự tuyên bố đạt | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-04 | HTTP/behavior tests và browser; actual bytes/network không fake pass | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |
| AC-05 | Self-review Requirement→AC→evidence, đồng bộ014/015/PLAN/INDEX/Context khi trạng thái thực thay đổi | Repo root; nguồn/môi trường thực ghi khi chạy | NOT_RUN — Task chưa bắt đầu |

## Review và checkpoint

Mới tạo tài liệu Task trong lượt lập kế hoạch. Review tính nhất quán docs được ghi ở [report lập kế hoạch](../07-testing/TEST-REPORT-WEB-UI-PLANNING.md), không phải nghiệm thu AC của Task này. Chưa code/build/browser/Google hoặc chốt UI theo HTML. TODO giữ; không chuyển DONE khi còn gate bắt buộc. Thiếu backend ghi gap/contract riêng, không tạo nút giả hoặc tự chốt rule.
