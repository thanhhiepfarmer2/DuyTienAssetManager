> Hiện hành16/10: runtime đã kiểm một phần; trạng thái IN_REVIEW. Xem checkpoint16/10 cuối tài liệu và Report021, các NOT_RUN15/10 là lịch sử.

# TASK-021 — Rà soát trải nghiệm, online offline và hiệu năng web

- Status: IN_REVIEW

> Hiện hành15/10: đã triển khai và kiểm phần local. Matrix hiện hành ở cuối tài liệu và Test Report là kết quả; các bảng/routing “chưa bắt đầu” bên dưới được giữ như checkpoint lập kế hoạch. Browser/Google/mạng thật NOT_RUN, không DONE.
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

Giữ mọi thay đổi chưa commit và trạng thái/gate TASK-005–015. Google/Sheets/Drive/drive.file, server auth/scope/guard/single-writer/version/idempotency và mật khẩu min8/phiên không đổi. Android tạm dừng, iOS DEFERRED_BY_USER. Không commit/push/deploy/dữ liệu thật. HTML hiện đã STATIC_VERIFIED đúng bytes/hash ở TASK-016; chỉ tham khảo UI, không thực thi hoặc nhập dataset. Rule/nguồn OPEN/TBD giữ nguyên.

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


## Triển khai15/10 — IN_REVIEW

Hoàn tất phần local PLAN-003: shell/header xanh/sidebar/breadcrumb và tài nguyên local; giao diện danh mục/tài sản/ảnh/hồ sơ/import/vật tư/số lượng/báo cáo/quản trị, hướng dẫn online mặc định/ngoại tuyến chỉ đọc. TASK-016 DONE tài liệu; TASK-017–021 IN_REVIEW do browser/Google/mạng thật chưa đủ. Không mở task/mobile khác.

TASK-021:137/137 integration passed exit0 (17s), Node5/5 exit0, benchmark Connected/Razor665nút/20744Items1/1 exit0 (26s);9/9 copy-import+baseline check exit0 (1s), có overlap không cộng thành tổng unique. Release build exit0/0warnings/errors5.47s. Một backend PID25412, Google/Development/Connected; strictHTTPS public login200 và tài nguyên local có fingerprint200, không AI login/Google business pass.

[Report](../07-testing/TEST-REPORT-TASK-021.md). Phần phụ thuộc local đủ; browser/Google/mạng thật chưa chạy giữ NOT_RUN. Người dùng cho phép tiếp task kế, không đổi DONE để vượt gate. Checkpoint NOT_RUN trước đó là lịch sử.


### Matrix hiện hành sau tích hợp021

AC01 matrix/HTTP+event evidence có, browser NOT_RUN. AC02 PASS_LOCAL fake transport/clock/cache, real network NOT_RUN. AC03 đủ số đo nguồn/sample/cold/warm/phase synthetic, Google timing NOT_RUN. AC04 local HTTP/node có; browser còn mở. AC05 report/gates/launcher đã đồng bộ.

[Xem tổng hợp021](../07-testing/TEST-REPORT-TASK-021.md). Các bảng NOT_RUN khi lập kế hoạch là lịch sử; trạng thái task vẫn IN_REVIEW. Không mở lại UAT đã hoãn.


## Runtime16/10 — IN_REVIEW

Đã đọc auth Google thật3sample, user login thành công và partial browser Home/Danh mục. Sửa static manifest/auth-writer/cache version/literal cấp F/breadcrumb;101/101tests pass, Release0warnings/errors. Public JS trước1Google call/477.786ms, sau5samples14–32ms/0calls (14asset traces). GET chính Google vẫn nhiều giây, chưa đủ warm p95 và chưa đo browser navigation sau sửa; tool dừng URL-confidence ở Assets. Không DONE.

Bằng chứng, lệnh/exit, giới hạn và backend PID13680/build mới tại [Report021](../07-testing/TEST-REPORT-TASK-021.md) và [JSON runtime](../07-testing/TASK-021-RUNTIME-20261016.json). Suite cuối101/101pass,0failed/skipped,exit0 (16s); Releaseexit0/0warnings/errors3.82s. Không cộng với các suite overlap. Browser tool dừng thì dừng app input, không yêu cầu chuỗi UAT user. Tài khoản/data/auth policy/guard/freshness/pacing giữ; không business writes Google hoặc mobile/Git/deploy.


## Hiệu năng16/10 — giảm call count, IN_REVIEW

Theo chỉ đạo tiếp TASK-021: [request map](../07-testing/TASK-021-REQUEST-MAP.md) và [evidence A/B](../07-testing/TASK-021-READ-PERFORMANCE.json). Gom business asset/category ranges cùng spreadsheet thành1values.batchGet, hai parsers/guard/journal giữ nguyên; auth spreadsheet riêng. Detail+form choices và category detail+history cùng context/read lease. Clean RECONNECTING bỏ prefetch/discard rồi navigation lại; dirty/error/backoff/read-only/version không đổi, không cross-request filtered cache/coalescing.

Google thật CLI3cặp/case: Assets4→3GET, median5002.12→3774.37ms, payload/authority/revision đều khớp; Categories control3→3.45GET tổng warmup/samples,0Google API writes. **CLI_SERVICE_READ_NO_WEB_SESSION**, không auth touch/Razor/browser, không p95 hoặc page-login performance PASS. Local166/166 +cross25/25 +Node11/11exit0, Releaseexit0/0warnings/errors7.92s. Backend1/PID24368/Google Development, UIrevision.3. Browser giữ STOPPED_TOOL_URL_CONFIDENCE, không lách driver/đọc cookie/profile; real session/touch/detail/network/multi-tab browser gates còn mở. [Report đầy đủ](../07-testing/TEST-REPORT-TASK-021.md). Không task mới/mobile/Git/deploy; trạng thái cũ và gate hoãn giữ nguyên.
