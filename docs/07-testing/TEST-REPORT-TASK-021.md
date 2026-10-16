> Checkpoint hiệu năng hiện hành16/10: Google CLI A/B3cặp Assets4→3GET, median5.00→3.77s;166/166local +25/25cross-check +Node11/11pass. Không web-session/browser/p95 pass; xem phần giảm Google reads cuối tài liệu.

> Kết quả hiện hành16/10: partial browser Home/Danh mục, login người dùng+Google thật đã có; sửa asset/auth/cache/Razor,101/101local pass. Browser dừng URL-confidence, gate còn mở; xem phần Runtime16/10 cuối tài liệu.

# TEST-REPORT — TASK-021 / tổng kết PLAN-003

2026-10-15 (Asia/Saigon), repo `D:/Dev/QuanLyTaiSan_DuyTien`, Windows x64, .NET SDK10.0.400, Node24.19.0. **AI chạy/quan sát trực tiếp** cho lệnh và số đo dưới đây. Không evidence người dùng/browser/Google mới trong lượt này. Người dùng cho phép tiếp tuần tự khi local đủ dù gate thật chưa có; không chuyển DONE để vượt phụ thuộc.

## Kết quả theo gate

| Gate / expected | Actual / nguồn |
|---|---|
| Tham chiếu đúng file và inventory | TASK-016 **DONE/PASS_DOCS_STATIC**:7190714bytes/SHA256 đúng metadata;31routes+fields/actions/permissions/partials/dynamicOffline/retired410. Đọc CSS/classes tĩnh; không thực thi Apps Script/dataset. [Inventory](../02-architecture/WEB-UI-INVENTORY-TASK-016.md) |
| Shell/module/forms/admin/help | **PASS_LOCAL_HTTP_RENDERED** compiled Razor + Kestrel, fake Google/synthetic stores; tương tác script **PASS_LOCAL_NODE** trên event/DOM doubles, không browser |
| Auth/scope/version/CSRF/registry | **PASS_LOCAL** suite liên quan: hidden branches/403/404, validation422, conflict409 giữ input/key/version, scoped export, registry impact/CSRF commit atomic. Domain/core/Google adapters/guard không đổi |
| Online/offline/reconnect | **PASS_LOCAL** ConnectedCacheTests: first-online-login không cần cache, offline API writes423, revalidate trước ghi, revoke/lock/expiry/change user/logout/restart/cache tamper/missing bytes/UNKNOWN/mixed revision/connection epoch. Probe/clock/central fake, không rút ngắn policy |
| File gateway/cache | **PASS_LOCAL**: thêm đường Razor `/Files/Content`, exact query/bytes, variant thiếu503, không đọc central offline; link coverage theo pathname+query, không tự coi metadata AVAILABLE là bytes offline |
| Dirty forms/upload | **PASS_LOCAL_NODE + HTTP**: cảnh báo rời form, cancel-confirm không submit, upload lỗi giữ File/key; chuyển mạng giữa upload không bật lại control. Native HTTP validation/conflict/form giữ theo handlers; browser Back/reload/timeout chưa quan sát |
| HTTPS/runtime | **PASS_HTTPS_PUBLIC** strict Windows validation, một backend launcher; không auth/Google-read PASS từ200 |
| Browser desktop/narrow/keyboard/DOM/download | **NOT_RUN_BROWSER**: cua.getState apps[]/browsers[]; createBrowserTab iab trả Browser is not available. Không dùng HTTP/Node doubles thay bằng chứng browser, không giao UAT user |
| Google current data/performance/network thật | **NOT_RUN_THIS_TURN**; giữ bằng chứng cũ đúng phiên bản. Không gọi API business/consent/init/PoC/resources/sharing hoặc viết DEV. Background launcher vẫn dùng probe nhỏ hiện có |

## Lệnh và kết quả thực tế

Lệnh đầy đủ/filter/env tại [COMMANDS](../COMMANDS.md), tất cả build/test dùng chung output chạy tuần tự và chờ exit.

- Suite021 `WebUiShellTests|WebUiModuleTests|ConnectedCacheTests|WebIntegrationTests|AssetCrudHttpRazorTests|CategoryHttpRazorTests|TreeMutationHttpRazorTests|ManagedMediaHttpRazorTests|InventoryHttpRazorTests|ReportHttpRazorTests`: đầu137total/136pass/1fail/0skip,exit1 do assertion UNKNOWN ở empty Stock giữ copy cũ; đổi assertion sang “chưa biết số lượng”, giữ viewer denial/no Quantity fields. **Cuối137/137pass,0failed/skipped,exit0,17s**.
- `node --test tests/web-ui-behavior.test.cjs`: lần đầu syntax runner sai quoting,exit1 (chưa chạy các behavior case). Sửa quoting; **5/5pass,0failed/skipped,exit0**, gồm disclosure/Escape, cancel destructive form, dirty navigation/native submit/media saved, tree descendants, failed upload+connection change giữ file/key. Test sau fix race cuối cũng5/5exit0.
- Node `--check` cả9scripts local: từng exit0. Không npm/download/dependency mới.
- `--filter FullyQualifiedName~WebUiPerformanceTests`: **1/1pass exit0/24s** đầu; bổ sung cold disk reload và using cleanup lease, **cuối1/1pass,0failed/skipped,exit0/26s**. Synthetic data/temp workspace, production ConnectedRuntime/protected cache, không runtime LocalOffline v1.
- Filter riêng `InventoryHttpRazorTests|Warm_online_and_offline_timings_are_separate_fake_central_samples`: **9/9pass,0failed/skipped,exit0,1s** sau copy import cuối và benchmark baseline. Các suite overlap, không cộng làm tổng unique.
- Release server build `-c Release --no-restore --nologo -v:minimal`: **exit0,0warnings/errors,5.47s**. Trước đó Stop-Web exit0 chỉ PID14152 khớp record/executable/start-time; không kill process khác.
- Start-Web `-NoBrowser`: **exit0**, PID25412. Child output/log ở ngoài Git; không in secret. StrictHTTPS `/api/system/status`,Login,/Help và assets200. Marker probe đầu tìm literal `/js/ui.js` trả false vì .NET10 Release fingerprint tên; sửa probe dùng URL thực trang, **9/9 actual CSS/JS URLs200 + icon SVG200**, new shell true. [Runtime public](TASK-021-PUBLIC-RUNTIME.json).
- `git diff --check`, protected-core diff/indexempty/HEAD checks exit0; HEAD5c4f5ad8f3cfc22ce937e6ce63027c43abe26128/main giữ. Không stage/commit/push/deploy.

## Benchmark có nguồn rõ

[JSON chi tiết](TASK-021-UI-BENCHMARK.json). Fixture GIẢ665nút (15/98/552) và20744Items; tài sản5, không công ty. Trung tâm dùng filesystem synthetic ở temp; InternetProbe fake. HTTP loopback, compiled Razor, cache đọc thật có DataProtection; **không browser hoặc Google transport timing**. Warm20samples/route/mode; p50 nearest rank10/20,p95rank19/20, cold1 riêng. Không đặt assert timing thành PASS nghiệp vụ.

- Dựng fixture: 4347.62ms. Reload/kiểm snapshot trung tâm giả từ đĩa: 1235.39ms. Các phase deserialize/snapshot-load/journal-replay/reconcile/index-build trong JSON có thể lồng nhau, **không cộng**.
- Host Kestrel/DI startup sau reload: 287.46ms. GETlogin cold: 319.43ms; POSTlogin synthetic với framework hash: 117.55ms (một mẫu, không warm p95 login).
- Google calls0/external managed HTTP0 trên benchmark cô lập. Auth/Razor/snapshot-clone phase ghi Server-Timing; auth-store memo có một frame/request. Google pacing/cooldown/coordinator thực **NOT_MEASURED_GOOGLE**, không điền0 giả. Không thay pacing hoặc thêm Google calls cho component; menu/breadcrumb/state thuần claims/cache RAM theo source review.

| Mode / nguồn | GET | Cold ms | Warm p50 ms | Warm p95 ms | Max bytes |
|---|---|---:|---:|---:|---:|
| ONLINE_SYNTHETIC_CENTRAL | `/Categories` | 86.62 | 30.75 | 59.58 | 32616 |
| ONLINE_SYNTHETIC_CENTRAL | `/Assets` | 81.20 | 29.48 | 39.25 | 81777 |
| ONLINE_SYNTHETIC_CENTRAL | `/Items` | 126.50 | 166.03 | 183.45 | 11491 |
| ONLINE_SYNTHETIC_CENTRAL | `/Stock` | 575.50 | 290.33 | 392.71 | 9344 |
| ONLINE_SYNTHETIC_CENTRAL | `/Reports?Code=R01` | 158.74 | 28.80 | 39.31 | 78162 |
| ONLINE_SYNTHETIC_CENTRAL | `/Reports?Code=R04` | 299.73 | 340.84 | 433.88 | 77819 |
| OFFLINE_READ_ONLY | `/Categories` | 6.68 | 0.33 | 0.58 | 32914 |
| OFFLINE_READ_ONLY | `/Assets` | 0.64 | 0.36 | 0.41 | 82075 |
| OFFLINE_READ_ONLY | `/Items` | 0.45 | 0.21 | 0.32 | 11789 |
| OFFLINE_READ_ONLY | `/Stock` | 0.38 | 0.21 | 0.22 | 9642 |
| OFFLINE_READ_ONLY | `/Reports?Code=R01` | 0.51 | 0.35 | 0.45 | 78460 |
| OFFLINE_READ_ONLY | `/Reports?Code=R04` | 0.56 | 0.36 | 0.51 | 78117 |

Mục tiêu warm GETp95<500ms **đạt riêng fixture này**; Stock cold 575.50ms không bị gọi là warm pass. Items payload khoảng11,5KB, không nhúng20744rows vào bảng. Không chuyển số đo cache thành tốc độ Google.

[So baseline cùng nguồn](TASK-021-COMPARABLE-BENCHMARK.json): route `/api/assets`, fake-central fixture nhỏ đúng runner015,20samples. Baseline015 p95online41.7425ms/offline0.2868ms; lượt này38.4576ms/0.4632ms, auth central20/0calls tương ứng. Không phải so665/20744với Google, không kết luận cải thiện/hồi quy có ý nghĩa từ một lượt/nguồn nhỏ. Không có baseline UI cũ cùng fixture lớn để so nhân quả.

## Sửa tích hợp và self-review

Giữ snapshot/logical IDs/enum/audit/source/header20cột và server auth/scope. UI labels ở WebUi metadata provider không thay binder/validation rules. Cancel-job/bulk khác/KPI không có contract thì không tạo nút. File cache nhận đúng gateway Razor, query phân biệt. Offline HTML có body state để khóa form ngay khi script chạy; query/lọc/trang trước được giữ, chọn chức năng bằng tiếng Việt. Upload finally đọc writeBlocked để tránh bật nút giữa mất mạng; giữ marker để reconnect mở lại đúng control. Dirty form chỉ RAM, không lưu dữ liệu/password vào browser storage.

Áp dụng .NET/design-system/accessibility và self-review ai-debt-detector/session-guard, không subagent hoặc audit bảo mật độc lập. Core/Application/Infrastructure Google/Local, mobile, launcher/scripts và task005–015 bằng chứng không sửa. Các assertion copy được đổi theo nhãn thật; không skip/làm yếu guard/permission/CSRF tests. Cache lease benchmark using cleanup; ObjectURL preview thu hồi khi thay/hủy/pagehide.

## Phần còn mở / trạng thái

- TASK-016 DONE tài liệu; **TASK-017–021 IN_REVIEW**. Local đủ theo ngoại lệ user, không đồng nghĩa browser/Google/mạng thật đã đạt.
- **NOT_RUN_BROWSER:** visual desktop/narrow, contrast/screen-reader, keyboard/focus/layout/scroll, actual file preview/download, native navigation Back/reload/form persistence. CSS/event doubles/HTTP rendered chỉ bằng chứng phần độc lập. Native POST lỗi trước handler hoặc mất backend chưa có bằng chứng browser giữ form.
- **NOT_RUN_GOOGLE/REAL_NETWORK:** tài khoản GoogleDEV hiện hành/freshness/revoke/reconnect/429 quota/timing trên giao diện mới. Login hiện có PASS_MANUAL ở checkpoint015 của user vẫn giữ, không xác nhận lại trong lượt này.
- **OPEN/TBD:** workbook gốc, rules, đơn vị/địa điểm/ngưỡng và nguồn; DRAFT/demo không được phê duyệt. Registry/schema guard vẫn fail-closed khi chưa đối soát.
- Ngoại tuyến chỉ các trang/query/bytes đã tải trong phiên còn hạn, không đầy đủ dataset và không offline khi backend dừng. Cache HTML cũ có thể giữ giao diện trước cho đến khi trang đó được đọc online lại; không xóa cache/dữ liệu cũ để che giới hạn.
- Giữ TASK-003 IN_REVIEW/004DONE/005–012/014/015IN_REVIEW và các DEFERRED_BY_USER/STOPPED_BY_USER. Android tạm dừng; iOS hoãn. Không triển khai task/mobile tiếp.

Mở `DuyTien-Web.cmd`, truy cập https://localhost:7008/Account/Login; launcher dùng lại đúng một backend (PID25412 tại thời điểm kiểm), không build/restore mỗi lần. Dùng tài khoản trung tâm hiện có, không LocalSetup/reset admin. Backend hiện Google/Development/Connected, anonymous state RECONNECTING không phải phiên đã xác minh. [Hướng dẫn](../WEB-USER-GUIDE.md).


## Đối chiếu cuối — AI quan sát trực tiếp

Python UTF8 checker exit0: baseline480files,58tracked đổi trong phạm vi,74modified/untracked,176local links/0missing,0artifact/HTML/private-data path trong danh sách Git, statuses016DONE/017–021IN_REVIEW đúng. Fingerprint protected core/Application/Infrastructure Google và Local/mobile/scripts/launcher/AGENTS/task003–015 và reports cũ:0changed. `git diff --check` exit0; indexempty và HEAD/main giữ. Chỉ kiểm đường dẫn artifact + fingerprint, không tuyên bố audit secret/security toàn diện. Sau bổ sung ghi chú tài liệu, diff check kiểm lại, không cần build/test lại vì không đổi source.


## Runtime tiếp tục16/10 — đang kiểm, không thêm feature/plan

Nguồn chỉ đạo mới: kiểm runtime thực/Google read-only và cache cũ–mới. Giữ mọi file chưa commit đầu lượt bằng fingerprint ngoài Git. Không business writes Google hoặc mobile.

Tool check: cua.getState apps[]/browsers[]; Windows sky import/list_apps thành công, có Chrome window và Chrome/Edge executable. Chọn Chrome và thử chuẩn bị tab; Computer Use **STOPPED_TOOL_URL_CONFIDENCE** vì không xác định URL Windows đủ chắc để enforce policy, phải dừng app input trong lượt này. Playwright/playwright-core import lỗi export default; @playwright/test/puppeteer-core module not found trong runtime. Không suy là máy không có Chrome; không đọc profile/cookies/password bằng file hoặc lách tool stop qua driver khác. UI Google/browser còn bị chặn, phần backend/cache độc lập tiếp tục.

Sửa kỹ thuật trong phạm vi đang kiểm: version HTML cache riêng với data/schema, giữ snapshot/file/auth/pending cũ; HTML cũ đọc qua trình bày plaintext an toàn, không chạy script/fingerprint cũ, mở online sẽ thay từng trang đã xác minh. Google auth có batch snapshot read thật; khai báo capability IAuthSnapshotValidation để dùng đường đọc sẵn có khi <5 phút, mốc touch/idle/absolute/revoke/security version giữ nguyên. Kiểm blocked-writer và touch/revoke/expiry bằng fixture trước; không gọi thay đổi này là cải thiện latency Google khi chưa có số đo. Thêm số đo bounded auth-diagnose và route-family/request-id/Server-Timing, không log body/URL/credential. Kết quả thực sẽ bổ sung sau.


## Runtime16/10 — kết quả hiện hành sau người dùng đăng nhập

Nguồn: **AI chạy/quan sát trực tiếp** cho Google diagnostic/telemetry/probe/build/tests và browser Home/Danh mục; **người dùng xác nhận thủ công** “Đăng nhập xong”, nhập mật khẩu trực tiếp trên form. Không nhận/đọc mật khẩu, cookie/profile hoặc credential UAT. [Evidence kỹ thuật đã làm sạch](TASK-021-RUNTIME-20261016.json). Các checkpoint NOT_RUN và tool stop phía trên giữ lịch sử, không là trạng thái tổng quát hiện tại.

### Browser và Google thực

Cua provider apps[]/browsers[] không đại diện mọi công cụ. Kiểm thêm Chrome/Edge có sẵn, node runners: playwright/playwright-core lỗi export default trong runtime; @playwright/test/puppeteer-core không resolve. Windows `@oai/sky` import/list_apps được. Sau khi người dùng tự mở ứng dụng/login, sky đọc được đúng cửa sổ Chrome localhost: Home và Danh mục hiện shell mới/header xanh/sidebar/nhãn dữ liệu chưa duyệt, ONLINE sau xác minh. Danh mục hiện hai gốc dữ liệu GIẢ đã có, không nhập dataset nguồn. Phát hiện cấp F hiển thị nguyên văn và breadcrumb lặp.

Khi click Tài sản rồi quan sát lại, tool **STOPPED_TOOL_URL_CONFIDENCE** yêu cầu dừng vì không xác minh được URL Windows. Không tiếp tục app input/reset/chuyển driver để lách. **PARTIAL_BROWSER_OBSERVED**, không “không có browser” và không browser PASS toàn module. Tài sản có Google/HTTP200 trong telemetry nhưng chưa xác nhận layout; không dùng capture khi trang còn loading làm thời gian load. Không có Navigation Timing/TBT/DOM render/paint hoặc browser p50/p95. Các trang Documents/Items/Stock/Reports/Admin, narrow/keyboard/focus, file preview/download và browser cache cũ/mới vẫn NOT_RUN_THIS_TURN/BLOCKED_TOOL cho phần phụ thuộc công cụ. Không giao checklist UAT user.

Google auth CLI `auth-diagnose`: **exit0**, ba snapshot auth batch GET thật, admin tồn tại/Active/ba ALL hiệu lực/không khóa tạm; chỉ bool passwordHashPresent, không hash. Samples **1668.85 /607.10 /1188.07ms**, mỗi mẫu1 Google batch GET,0writes; mẫu3 pacing656.72ms. Token-provider timing không đồng nghĩa số OAuth network calls. Tài khoản và password hash không reset, không OAuth bootstrap/consent mới hoặc thay scope. CLI dưới lease, backend đã dừng trước chạy.

Login Google thật: user xác nhận thành công; **POST /Account/Login302**, sau đó Home200/admin shell được AI quan sát. Đây là evidence phiên hiện có, khác anonymous Login200. Đăng nhập/touch phiên có Google auth session/audit writes theo policy; **không ghi nghiệp vụ** Categories/Assets/Items/media/reports hoặc tạo resource/sharing. Không gọi Google vắng Internet để giả PASS.

### Số đo runtime thật — backend, trước bản sửa static cuối

| Request | n | Backend ms | Google reads/writes | HTTP Google ms | Pacing ms | Auth ms | Coordinator wait ms | Razor ms |
|---|---:|---:|---|---:|---:|---:|---:|---:|
| POST login thành công | 1 | 2428.86 | 2/1 (auth) | 1515.84 | 771.55 | login2407.95 | 0.0015 | 0.03 |
| Home ngay sau login | 1 | 511.75 | 1/0 | 361.19 | 104.72 | 494.35 | không có phase | 1.66 |
| Home lần kế | 1 | 1269.74 | 1/0 | 344.56 | 846.23 | 1240.83 | không có phase | 0.69 |
| Categories lần đầu/lần sau | 2 | 3616.62 /2935.74 | mỗi lần3/0 | 1075.07 /1335.39 | 2381.65 /1257.72 | 1031.23 /836.35 | 0.0044 /0.0040 | 58.26 /13.82 |
| Assets không touch phiên | 1 | 4932.73 | 4/0 | 1494.62 | 3342.58 | 1183.65 | 0.0012 | 2.36 |
| Assets startup/touch, rồi touch sau | 2 | 8134.41 /6671.75 | mỗi lần6/1 (auth) | 5427.69 /3567.42 | 1744.35 /2682.24 | 3801.71 /3692.08 | 0.0135 /0.0021 | 94.55 /3.03 |

Journal replay Assets14.10ms ở startup,0.32/0.27ms các lần sau; coordinator gần0 trong các mẫu này, không chứng minh hết contention. Auth/Google/pacing có scope lồng nhau, **không cộng** các cột như độc lập. Không có phase deserialize/đối soát riêng trên các request Google này: NOT_MEASURED, không ghi0. Google read count là actual SendAsync, pacing count là số lần chờ, không là số call. Các mẫu lấy từ request của tab (có thể gồm refresh tự động khi reconnect), không quy mọi GET thành click người dùng. Các mẫu cơ hội ít, chưa cùng điều kiện cold/warm/Google quota; không báo warm p95 đại diện hoặc dùng fixture chứng minh nhanh. **Mục tiêu GET warm p95<500ms trên Google chưa đạt bằng chứng; các mẫu GET chính vẫn nhiều giây.** Không có số đo Items/Reports Google mới, không suy từ fake benchmark.

### Lỗi sửa và kiểm lại

1. **Public assets kích hoạt auth Google thừa, đã chứng minh:** GET JS fingerprint thực từ HTML, cookie hoàn toàn GIẢ không hợp lệ, strictTLS: HTTP200/477.786ms nhưng có1 Google auth read419.56ms, auth467.38ms. `UseStaticFiles` không nhận route fingerprint của manifest, endpoint chạy sau auth. Đổi sang helper `MapPublicUiAssets` dùng `MapStaticAssets(...).ShortCircuit()` của framework; chỉ public wwwroot/build-manifest, không prefix allowlist cho nghiệp vụ. `/Files/Content`, API và đường giả dưới `/css` vẫn qua auth. Giữ pacing1250ms/cooldown/quota/scope/guard/freshness.
2. **Razor:** `F@n.FLevel`/`F@x.BeforeLevel` được hiểu như text liền email; dùng expression `F@(…)` tại list/detail/impact. Breadcrumb chỉ thêm link module ở trang con, không lặp ở module root; chỉnh khoảng trắng “tối đa 60”. Có regression compiled Razor cấp F1/list/detail và breadcrumb, impact tests.
3. **Auth read:** Google batch snapshot mới mỗi request đủ capability `IAuthSnapshotValidation`; reuse core path read-only trước touch5ph, không đợi writer khi chưa cần touch. Revoke/lock/SecurityVersion/idle/absolute vẫn fresh và touch re-read dưới writer. Tests blocked-writer,5 case expiry/revoke/lock và touch2GET/1write giả đạt. Chưa có so latency contention Google trước/sau đủ điều kiện, không tuyên bố tăng tốc phần này từ test.
4. **Cache UI:** revision hiện tại `web-ui-2026-10-16.2`. HTML cũ/thiếu revision hiển thị text encode chỉ đọc và báo asOf/cũ, bỏ script/form/fingerprint cũ; link tệp chỉ từ ticket cùng phiên thực có bytes. Giữ old bytes, JSON/file/pending/session/expiry; online thay từng view sau xác minh. Fixture protected filesystem kiểm legacy script/form không chạy, text/file link giữ, version mới/JSON/pending giữ; không xóa toàn cache. Browser cache cũ/mới thật chưa quan sát.
5. **Instrumentation:** random trace-id + route-family/method/status, token-provider/read/write/pacing/coordinator/auth/Razor phases; không raw URL/query/user/body/password/hash/token/cookie. Diagnostic middleware trước routing để capture cả static, không tham gia quyền.

Sau stop đúng PID4864, Release build **exit0,0warnings/errors,3.82s**. Khởi động qua `scripts/Start-Web.ps1 -NoBrowser` — chính script mà `DuyTien-Web.cmd` gọi; không mở thêm tab khi Computer Use đã dừng. **PID13680, duy nhất1 native backend**, Google/Development, build `43068903-cfc6-4236-935d-0777b9fa5d55`. Anonymous status RECONNECTING là không có proof của request ẩn danh, không chứng minh tài khoản bị mất hoặc Internet mất. Không nhận cookie của người dùng để kiểm bằng HTTP riêng.

**Probe sau sửa thực:** cùng JS fingerprint/cookie giả,5 HTTP GET strictTLS:32.289 /17.506 /15.293 /14.001 /26.527ms (median17.506ms, không coi n5 là benchmark warm đại diện). Thêm9CSS/JS local từ URL thực trang login:9/9HTTP200. Đối chiếu đủ14trace-id với JSONL: **0 auth phases/0 Google calls**; trước sửa1sample có1Google read. Đây là cải thiện chứng minh riêng public asset; **không phải page-navigation/Google business p95 sau sửa**. Browser đang bị stop nên không đo lại trang đã đăng nhập trên final build.

### Lệnh / exit và kết quả local

Repo root; exact filter tại [COMMANDS](../COMMANDS.md). Build/test tuần tự:

- Focused suite auth/cache/UI/Google executor:92/92passed,0failed/skipped,exit0 (17s), chạy lại sau legacy file links cũng92/92exit0. Không cộng overlap.
- Thêm actual Kestrel/build-manifest regression, tree impact và Razor fixes: compile đầu **CS0121/exit1** trong callback test host; ghi rõ kiểu HttpContext/RequestDelegate. Lượt kế101total/100pass/1fail/exit1 do selector test giả định class đứng ngay sau <nav>; sửa selector chấp nhận attributes xen giữa, vẫn kiểm F1/no literal/no duplicate. **Cuối101/101passed,0failed/skipped,exit0,16s**. Static test thực phục vụ CSS/JS/SVG,0auth middleware calls;3protected paths401. Không skip/làm yếu scope/guard/CSRF tests.
- `powershell.exe -NoProfile -File scripts/Stop-Web.ps1`: exit0, chỉ PID4864 khớp record/executable/start-time. `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj -c Release --no-restore --nologo -v:minimal`:exit0/0warnings/errors3.82s.
- `powershell.exe -NoProfile -File scripts/Start-Web.ps1 -NoBrowser`:exit0/PID13680, env diagnostics chỉ child process; không sửa user-secrets hay launcher. HTTP/probe Python `python -B -`:exit0, TLS default validation bật; synthetic cookie không liên quan browser credential. Get-CimInstance backendcount1.
- Không thay JS lượt runtime này, không rerun Node/full UAT/task cũ/timeout dài. Không build mobile.

### Gate còn mở / cách chạy

TASK-017–021 **IN_REVIEW**: partial browser/real login/Google auth+Categories+Assets HTTP đã có, nhưng chưa đủ AC: toàn bộ modules/narrow/keyboard/download/dirty/error/conflict browser, cache cũ/mới trên browser, mất mạng/reconnect/revoke mạng thật và thống kê warm Google trước/sau đủ mẫu. Không tự mở lại các DEFERRED_BY_USER/STOPPED_BY_USER. Quyền/rule/nguồn OPEN/TBD, DRAFT/demo không đổi.

Backend mới đang chạy1instance tại https://localhost:7008. Mở `DuyTien-Web.cmd` để launcher dùng lại đúng backend, URL login https://localhost:7008/Account/Login; tài khoản trung tâm hiện có/mật khẩu hiện tại, không LocalSetup/reset. Tab đã mở có thể cần mở lại trang để nhận markup mới; không xóa cache/storage. Snapshot cũ còn nguyên, không bị coi là dữ liệu hiện hành. Không commit/push/deploy/mobile/resources/sharing hoặc dữ liệu nghiệp vụ thật.


### Đối chiếu worktree cuối16/10

AI checker UTF8/JSON/status/204local links:exit0,0issues. Baseline496files đầu lượt:33file đổi đúng allowlist runtime/docs,463file giữ fingerprint;4file mới đúng phạm vi, không mất file.85modified/untracked file thực (Git directory display có thể gộp). TASK-005–015/source core/mobile/scripts/AGENTS và bằng chứng trước ngoài phạm vi nguyên vẹn; không xóa code hoặc workspace. HEAD5c4f5ad8f3cfc22ce937e6ce63027c43abe26128/main, indexempty. Pattern/path scan cơ bản không phải audit bảo mật toàn diện; không in credential. `git diff --check`exit0 trước ghi checkpoint này, kiểm tiếp sau docedit; không build/test lại khi chỉ thêm evidence docs.


## TASK-021 — giảm Google reads16/10, checkpoint hiện hành

Nguồn **AI chạy/quan sát trực tiếp** cho code/tests/CLI Google/public HTTPS; không user-run/manual/browser mới. Chỉ tiếp TASK-021, không feature/task/mobile mới. Worktree đầu lượt sạch, HEAD55ddab07cb6eeea6e4e36ba569f175a3a36facb2/main. Giữ workspace/cache/account và mọi gate lịch sử. [Bản đồ request/source/guard](TASK-021-REQUEST-MAP.md), [JSON số đo đầy đủ](TASK-021-READ-PERFORMANCE.json).

### Những lượt đọc đã xử lý

| Luồng | Trước | Sau | Phạm vi bằng chứng |
|---|---|---|---|
| Categories list | auth1 + business metadata1 + values1 =3 |3 | Google CLI control, không bỏ đọc cấu trúc/schema |
| Assets list | auth1 + metadata1 + asset values1 + category values1 =4 | auth1 + metadata1 + union values1 =3 | Google CLI3cặp thật, validators cũ đầy đủ |
| Category detail + history | metadata1 + category values2 (auth memo1) | metadata1 + category values1 (auth memo1) | fake HTTP adapter3→2 business calls; Razor2business calls; tương ứng4→3 tổng nếu web auth không touch, **chưa đo Google page** |
| Asset detail + form choices (editor) | metadata1 +2asset values+2category values (auth memo1) | metadata1 + union values1 (auth memo1) | Code trace trước + compiled Razor sau2business calls; dự kiến6→3 tổng không touch, **không gọi là Google browser measurement** |
| Clean business RECONNECTING | verification GET rồi bỏ response và navigation GET lại | navigation GET duy nhất xác minh auth/data | Node event doubles; NOT_RUN_BROWSER/REAL_NETWORK |

Auth system spreadsheet khác business spreadsheet: không gom hai resource vào một values.batchGet. Auth memo đã reuse giữa session validation/Remember/service trong **cùng request**, không bỏ SecurityVersion/revoke/expiry hoặc dùng cache qua request. Các mẫu web tới touch5ph vẫn có read/write bắt buộc; lượt này không sửa policy/touch. Header/sidebar/breadcrumb không gọi Google. Registry journal trong category parser vẫn đọc; full tree guard/legacy tables/Items/Media/registry checkpoint chỉ ở luồng cần full registry, không thay bằng snapshot list.

`GoogleAssetCategorySnapshotReader` chỉ gom raw ranges cùng API executor/business URI trong một read lease; mỗi range giữ bound5001/20001, đúng subset về parser Assets/Categories để kiểm schema/header/partial/journal/counter/alias/audit/tree digest. `CanReadTogether` kiểm facade category, broker kiểm nguồn/executor trước/sau fetch. Không global cache, không coalescing giữa user/request, không shared filtered data; token cancel/errors không lưu thành task dùng lại, scope/response disposed. Application write Load và Prepare/Commit vẫn đi nhánh fresh riêng, metadata invalidation/single-writer/version/idempotency giữ nguyên. Thêm DetailWithChoices và DetailWithHistory để render các phần từ cùng context; read/update/history scopes vẫn được kiểm độc lập như trước.

Reconnect: trang đọc sạch ở RECONNECTING điều hướng thẳng một GET; dirty/non-readable form giữ input/key, chỉ verification read, không replay. pagehide abort các GET/poll; trạng thái ONLINE không thêm business request. Self-review giữ nhánh SERVICE_UNAVAILABLE dùng verification/backoff tới thành công rồi mới navigation để tránh vòng503 reload; **nhánh phục hồi lỗi này còn có thể read+navigation**, không nhận đã loại mọi trường hợp. Không thay phân loại401/403/429/503 thành offline. Không thêm queue/sync hoặc tăng parallel Google calls.

### Google DEV thật — CLI, không web session

Browser giữ trạng thái **STOPPED_TOOL_URL_CONFIDENCE** từ lượt trước. Lượt này không gọi lại browser, đổi driver, trích cookie/profile hoặc yêu cầu/reset password. Vì không có phiên web qua công cụ được phép, đo bằng operator CLI `catalog-read-diagnose` trong Google/Development và cùng process lease. CLI chỉ lookup admin hiện có/role-scope rồi gọi application services; **không đăng nhập, không ValidateAccessToken, không session touch, không Razor/browser**. Vì vậy các số này là **CLI_SERVICE_READ_NO_WEB_SESSION**, không phải thời gian chuyển trang có đăng nhập.

Cùng Release binary: chế độ Separate bọc store thành đường đọc cũ, Batched dùng capability mới. Mỗi case có fresh request auth/metadata frame và source values, không cache xuyên sample; Categories là control dùng cùng path cả hai mode. Warmup1/case riêng,3cặp mỗi route, luân phiên separate→batch / batch→separate / separate→batch, shared pacing cursor giữ nguyên không reset. MaxRetryAttempts1 chỉ env CLI để giới hạn phép đo; backend trả config retry thường sau đó. Không giảm1250ms pacing/cooldown, không parallel. **6/6cặp payload/authority/revision bằng nhau**, kiểm bằng digest nội bộ nhưng chỉ xuất bool, không data/hash/IDs. Tổng cả warmup **45 Google GET,0 Google API writes trong trace**; OAuth token-provider có thể refresh riêng, không suy số OAuth HTTP từ token invocation. Không business mutations hoặc resource/sharing/schema writes.

| Route / pair | Separate elapsed ms | Batched elapsed ms | GET trước→sau | Google HTTP trước→sau ms | Pacing trước→sau ms |
|---|---:|---:|---|---|---|
| Categories1 |3523.53|3802.13|3→3|990.10→1038.52|2482.31→2720.53|
| Categories2 (đảo thứ tự) |3582.45|3927.82|3→3|1039.43→1054.47|2499.01→2666.14|
| Categories3 |3774.28|3740.04|3→3|1059.42→1333.20|2672.67→2362.13|
| Assets1 |4964.57|3748.65|4→3|1406.72→1043.35|3266.56→2658.68|
| Assets2 (đảo thứ tự) |5021.56|3774.37|4→3|1366.66→1005.88|3389.27→2724.13|
| Assets3 |5002.12|4359.13|4→3|1385.01→1611.88|3566.08→2703.97|

Trung vị3mẫu Assets **5002.12→3774.37ms**, range4964.57–5021.56→3748.65–4359.13ms. Categories control median3582.45→3802.13ms, không đổi call count/code đọc nên không suy cải thiện hoặc hồi quy nhân quả từ n3/Google latency/pacing carry-in khác. Các phase lồng nhau, không cộng thành tổng. JSON giữ mọi phase đã đo; không có browser render/DOM/paint/Razor trong CLI, không ghi0 giả cho các phase không chạy. **Không tính p95 đại diện hoặc hứa<500ms.** CLI wall time59414.75ms/exit0, không timeout dài.

### Kiểm tra thực tế / exit

Lệnh/filter/env chi tiết tại [COMMANDS](../COMMANDS.md). Build/test chung output tuần tự; native CLI chỉ sau khi xác minh backend dừng.

- Suite adapter/auth/scope/cache/guard/version ban đầu158/158pass/exit0 (25s), có1warning xUnit2031 test assertion Where+Single; đổi sang predicate overload, lượt sau158/158pass/exit0 (17s), không warning. Không fail/skip hoặc sửa expected để bỏ guard.
- Mở rộng category detail/history và compiled Razor thật qua capability: **166/166passed,0failed/skipped,exit0,17s**. Batch riêng kiểm same-source/executor mismatch trước network, canceled batch rồi đọc lại thành công, header/partial/range/counter/materialized journal sai đều fail closed/noPOST; fresh call sau không reuse values trước, scopes user/admin không chia sẻ projection, revoked assignment bị chặn trước business I/O. Category history nhánh ẩn404/revoke403; Razor Assets/list/detail/Category detail mỗi GET2fake business calls,0writes.
- Kiểm chéo `WebIntegrationTests|WebUiModuleTests|GoogleApiRequestExecutorTests`: **25/25passed,0failed/skipped,exit0,15s**. Không cộng các run overlap thành tổng unique/UAT cũ.
- `node --test tests/web-ui-behavior.test.cjs tests/connection-reconnect.test.cjs`: đầu10/10exit0, sau service-error backoff **11/11passed,0failed/skipped,exit0**. `node --check .../connection.js`exit0 trước thêm branch; node runner cuối parse/chạy file cuối. Event/DOM doubles không browser.
- `powershell.exe -NoProfile -File scripts/Stop-Web.ps1`:exit0, runtime trước đo đã dừng/nativecount0; không dừng PID cũ hoặc process khác. Release build **exit0,0warnings/errors,7.92s**. `catalog-read-diagnose` process exit0, cùng lease; không init/bootstrap/OAuth consent mới.
- `powershell.exe -NoProfile -File scripts/Start-Web.ps1 -NoBrowser`:exit0, đúng script của DuyTien-Web.cmd. Final **PID24368/1native backend**, Google/Development, build **b9ad1fca-6c42-4bca-9680-a09e24c53494** (cùng binary đoCLI), UIrevision **web-ui-2026-10-16.3**. Python strictTLS public system/login/new connection JS200, no TLS bypass. Anonymous status RECONNECTING không là proof user; Login200 không được tính login PASS. Không mở browser đang bị stop.

### Gate / trạng thái

TASK-021 **IN_REVIEW**. Đã chứng minh giảm Google reads root Assets và giữ kết quả trên cùng nguồn thật; phần browser/phiên đăng nhập/touch trước-sau, Category/Asset detail Google, concurrency nhiều tab/navigation, mạng/reconnect/revoke thật và warm p95 đủ mẫu **NOT_RUN/NOT_MEASURED_THIS_TURN**. Không reopen DEFERRED_BY_USER/STOPPED_BY_USER hoặc yêu cầu chuỗi UAT/manual/secret mới. Snapshot/files/workspace/tài khoản hiện có và source/rule OPEN/TBD giữ; không coi DRAFT/demo là chính thức. Giữ TASK-003IN_REVIEW/004DONE/005–020 như trước; Android dừng/iOS hoãn. Không commit/push/deploy.

Web https://localhost:7008/Account/Login, mở `DuyTien-Web.cmd` để dùng lại một backend. Các số Google CLI thay thế khoảng trống **CLI**, không thay các gate browser/web session chưa có.


### Đối chiếu cuối lượt giảm reads

AI checker exit0: baseline500file,21file thay đổi trong phạm vi +7file mới;479file baseline giữ fingerprint/0missing.121local links/UTF8/JSON/status/artifact/credential-pattern checks0issues; chỉ scan cơ bản, không audit bảo mật toàn diện. SDK10.0.400; `node --check` connection.js bản cuối exit0, `git diff --check`exit0, indexempty/HEAD55ddab0/main không đổi. Auth core/policy, Google pacing/coordinator/tree guard, mobile/Local workspace adapter/scripts/launcher/AGENTS và task/report005–020 nguyên vẹn. Không stage/commit/push. Ghi thêm checkpoint docs không làm chạy lại build/test; diff check kiểm lại sau ghi. Backend cuối1/PID24368, build đúng binary đã đo, dữ liệu/workspace/cache cũ không reset/xóa.
