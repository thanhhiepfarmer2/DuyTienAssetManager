# TEST-REPORT — TASK-015 v2: online mặc định, offline chỉ đọc

2026-10-15 (Asia/Saigon), D:\Dev\QuanLyTaiSan_DuyTien. Nguồn **AI chạy/quan sát trực tiếp** cho lệnh/kết quả bên dưới. HEAD đầu lượt d8a508d, worktree sạch. User thay thế LocalOffline ghi độc lập bằng REQ-002/ADR-002/PLAN-002 v2. Bằng chứng v1 ở TEST-REPORT-TASK-015.md và JSON cũ giữ nguyên phiên bản, không tái gán PASS. Không user-run/manual/UAT mới, không secret prompt, không commit/push/deploy/mobile hoặc dữ liệu thật.

## Phạm vi đã triển khai

- Mặc định Google/Connected. DuyTien-Web.cmd và CMD LocalOffline cũ mở cùng launcher, dùng config tài khoản/resource trung tâm đã có. Program từ chối LocalOffline hoặc tắt connection guard; LocalSetup/LocalDemo410. Giữ code adapter/workspace cũ, không reset admin, nhập/sync Google hoặc init schema.
- Probe HTTPS nhỏ độc lập/timeout/coalesce/backoff; HTTP401/403/429/503, DNS/TLS không tự thành offline. Unknown/service failure fail closed. Google executor/CLI gate trước OAuth/pacing/transport, budget1250ms/cooldown60s giữ.
- Snapshot là **response query đã qua auth/scope**, không raw full stores; cache encrypted DataProtection/DPAPI/private ACL/exclusive lease ngoài Git, tách central/user/session. Proof chỉ identity/SecurityVersion/authority fingerprint và hạn min idle/absolute/grant, không mật khẩu/hash/OAuth token hoặc raw opaque token. Logout tombstone, expiry không extend offline, role/scope đổi xóa referenced snapshots/bytes; stale proof request không phục hồi view cũ.
- OFFLINE_READ_ONLY short-circuit trước auth/business handlers, không gọi business Google. API/admin/import/upload/reconcile/tree và auth mutation đều chặn; logout kỹ thuật có anti-forgery được phép. `/Offline` search Unicode không dấu/index RAM/lọc module/paging60 trên snapshots đã lưu; gateway chỉ phục vụ bytes có thực, thiếu/corrupt503 và nhãn thiếu file rõ.
- ONLINE luôn chạy dịch vụ trung tâm, không dùng cache TTL như current data. Header verifiedAt cho read thành công, không gọi bản cũ mới nhất. Reconnect cần fresh authority **và trang nghiệp vụ mới**, trang chủ/menu không mở ghi. Chốt ghi trước handler và sau auth; mutation boundary tuần tự, readers độc lập.
- UNKNOWN intent bền trước gửi, method/path/key/body hash, không payload/queue; theo user trung tâm qua logout/login mới. Reconcile online cookie cần anti-forgery, đọc journals dưới coordinator; media dùng ReconcileAsync hiện có khi cần, không upload bytes local. Chỉ journal xác nhận mới cho replay đúng method/path/key/payload; backend vẫn kiểm scope/version/hash/idempotency. Không journal/key/quyền/bytes thì tiếp tục block, không khẳng định chưa ghi.
- UI giữ form/key, có trạng thái/missing bytes/reconcile action và backoff tải mới; không tự resend mutation. Nguồn/rule/OrgLocation/ngưỡng/DRAFT/demo không đổi.

## Lệnh / expected / actual

Build/test tuần tự, mọi session chờ exit trước lệnh shared output kế. SDK **10.0.400**. Lệnh chính xác tại COMMANDS.

| Kiểm tra | Expected | Actual / nguồn |
|---|---|---|
| `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal` đầu | Compile v2 | exit1,0warnings/1error: CategoryImportOperation dùng Actor thay ActorUserId; sửa theo contract thật |
| `dotnet test tests/DuyTien.AssetManagement.ArchitectureTests/DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~ConnectedCache"` | Core/cache/HTTP behavior | checkpoints18/18exit0,26/26exit0,29/29exit0; sau thêm media32total/31pass/1fail exit1; 34/34exit0/11s,35/35exit0/12s; 36/36exit0/15s; metadata checkpoint43combined/43exit0/12s; 41/41exit0/14s; scope/binding44/44exit0/16s,45/45exit0/15s; cuối **46/46passed,0failed/skipped,exit0,15s** |
| Regression filter `FullyQualifiedName~AuthApiIntegrationTests|FullyQualifiedName~GoogleSheetsRequestBudgetTests|FullyQualifiedName~LocalOfflineTests|FullyQualifiedName~WebIntegrationTests` | Giữ auth/quota/web/legacy adapter | **46/46passed,0failed/skipped,exit0,20s**; không full UAT/task suites |
| `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj -c Release --no-restore --nologo -v:minimal` | Release chạy launcher | **exit0,0warning/error,5.03s cuối** (checkpoints trước4.41s/5.25s/6.25s) |

46case gồm: chưa có online proof không offline; direct API/write/import/tree/admin/password blocked; no central calls khi offline; service503/unknown không stale fallback; fake probeHTTP401/403/429/503 vẫn connected, double unreachable vs DNS; WAN/link-up timeout cả3probes, central public HTTP reachable ngăn offline giả; bearer API giữ hạn access15phút; actual Google executor all3semantics trước OAuth/HTTP đều0call offline (transport GIẢ); expiry fake clock không extend; lock/revoke/SecurityVersion trước mở ghi; userB không dùng cacheA; scope fingerprint đổi purge; post-auth gate; Home không đủ refresh; GET/HEAD riêng; file missing/MAC-corrupt; cache object restart/tombstone; UNKNOWN qua user session mới; cookie logout CSRF; actual asset/media services+journals/filesystem bằng fixture GIẢ và request HTTP loopback.

Media failure thuộc phạm vi: URL `/api/assets/{id}/photos` ban đầu bị resolver chọn asset journal, dù MediaReconcile đã hoàn tất; expected200/actual409. Sửa phân loại media trước assets, materialized file state được kiểm (không dùng PENDING lịch sử của operation gốc). Sau sửa cùng test PASS: một ManagedFile/one blob và replay cùng key, không duplicate. Không skip/nới expected. Test journal absent vẫn409UNKNOWN; không suy diễn NOT_COMMITTED.

## Benchmark v2 — tách nguồn

[TASK-015-ONLINE-OFFLINE-BENCHMARK.json](TASK-015-ONLINE-OFFLINE-BENCHMARK.json): Kestrel loopback HTTP + auth trung tâm **GIẢ** + filesystem cache thật, response fixture nhỏ; 20warmGET mỗi mode, full body. Không phải browser paint/TLS process hoặc dataset20.744/665 của v1.

- ONLINE fake central p95 **41.742 ms**, central auth store calls 20/20 (request memoization tránh đọc proof lặp).
- OFFLINE_READ_ONLY p95 **0.287 ms**, central auth store calls **0/20**.
- Google DEV protected cold/warm/login/dataset/quota timings **NOT_RUN**. Không dùng số fake/local này tuyên bố tốc độ Google hoặc reuse benchmark v1 làm v2 pass.

## Giới hạn / gate

- **NOT_RUN_GOOGLE_AUTHENTICATED_V2**: không có credential ứng dụng tạm mới; không yêu cầu user nhập lại, không thử mật khẩu, không reset admin. Chỉ xác minh boolean cấu hình OAuth/business/system tồn tại và distinct, không in giá trị. Chưa chứng minh login admin thật/revoke thật/schema/quota/ảnh/đối soát thật dưới state machine v2.
- **NOT_RUN_REAL_NETWORK_TRANSITIONS**: disconnected/reconnecting qua probe GIẢ có clock/backoff; không ngắt Internet/card mạng của người dùng. Không actual packet trace/OS-wide zero business egress; Google transport boundary và counter trong tests chứng minh đúng đường được chạy. Offline probe nhỏ được cho phép theo thiết kế.
- **NOT_RUN_BROWSER**: CUA apps0/browsers0; createBrowserTab iab báo unavailable. Không DOM/disabled fields/layout/upload interaction/browser reconnect proof; không giao lại chuỗi UAT thủ công.
- Cache có coverage giới hạn trang/query đã online-read, tối đa200/64MiB/phiên, RAM128MiB. Query/file chưa cache báo thiếu; index tìm snapshot chứ chưa là mirror đầy đủ mọi filter/field từ toàn bộ665/20.744. Mỗi response atomic/asOf riêng; không tuyên bố mọi module cùng một checkpoint toàn hệ thống. Online không shortcut cache nên còn chi phí Google full reread/journal/coordinator; memo metadata properties trong một request đã kiểm/invalidate sau ghi; revision/batch cross-request chưa có bằng chứng.
- UNKNOWN thiếu key/journal hoặc trạng thái chưa terminal sẽ tiếp tục chặn. Không auto retry/sync. Reconcile keys/scopes, disk fault matrix toàn bộ, schema mismatch thật, backup/restore cache v2/power loss chưa kiểm end-to-end. Cache object restart được test; authenticated Google session qua executable restart **NOT_RUN**.
- Nguồn/rule/workbook/ngưỡng/đơn vị/địa điểm vẫn OPEN/TBD. Android dừng, iOS DEFERRED_BY_USER. TASK-003 IN_REVIEW,004 DONE,005–012/014 giữ trạng thái/gate cũ.

TASK-015 v2 giữ **IN_REVIEW** sau phần local và các kiểm tra quan sát. Startup/HTTPS/public/process/preservation được ghi riêng ở phụ lục khi chạy thực tế; không đồng nhất với authenticated Google/browser gate.


## Executable/HTTPS/launcher cuối — AI quan sát trực tiếp

[TASK-015-ONLINE-RUNTIME.json](TASK-015-ONLINE-RUNTIME.json): Python subprocess gọi `powershell.exe -NoProfile -File scripts/Start-Web.ps1 -NoBrowser`, stdin DEVNULL/stdout-stderr private ngoài Git. Start và reuse **exit0/0**, cùng PID, một native backend; owner Windows/executable khớp. Các bản trước PID21900 và17388 dừng đúng bằng Stop-Web.ps1 sau kiểm record/exe/start-time để cập nhật Release; không dừng process khác.

Bản cuối **PID 24384**, URL **https://localhost:7008**, TLS validation bật (`ssl.create_default_context`, không -k/handler bypass). Public status/connection200, Login200, LocalSetup410, connection.js200 và anonymous assets API401: **6/6 expected/actual**. State public RECONNECTING vì chưa có proof đăng nhập trung tâm của request; không gọi anonymous readiness là authenticated ONLINE/data-current. Config OAuth/business/system chỉ kiểm boolean có đủ và distinct; không phát sinh business/auth writes từ các GET public không cookie này.

Launcher CMD cũ forward launcher mới; script legacy từ chối. Workspace v1 **36files, fingerprint tổng không đổi**, không đọc/in credential hoặc xóa tài khoản/audit/data. Cookie Google DEV giữ theo contract cũ; không dùng dt_local. Backend mới đang chạy một instance; gate Google login/phiên thật vẫn NOT_RUN. Chưa làm restart authenticated Google session hoặc runtime Internet loss thật; cache-object restart và executable replacement là hai bằng chứng riêng.

JS `node --check` exit0, PS AST Start-Web/Stop-Web/Start-LocalOffline hợp lệ exit0; `git diff --check` và index-empty đã kiểm, không commit/push. Self-review theo ai-debt-detector: thêm mutation serialization, post-auth gate, persist UNKNOWN theo user, bounded encrypted cache/response buffer, safe errors chỉ log exception type và kiểm central public probe. UI code ràng buộc DOM theo central/user/session/authority và reload bfcache, nhưng browser runtime vẫn NOT_RUN.


## Chốt hardening và kiểm tra cuối

- Sau các checkpoint36/43, bộ v2 **41/41passed,0failed/skipped,exit0,14s**. Metadata-only request memo loại đọc properties lặp, không values/bytes/auth/TTL xuyên request; invalidate epoch+lock khi ghi. Test cùng URI2lần chỉ1transport, values luôn2calls, sauwrite/requestmới đọc lại; pacing cho actual calls không đổi.
- Hồi quy cuối `--no-build --no-restore --filter "FullyQualifiedName~GoogleAdapterHttpTests|FullyQualifiedName~SafeGoogleHttpClientLoggingTests|FullyQualifiedName~AuthApiIntegrationTests|FullyQualifiedName~WebIntegrationTests"`: **35/35passed,0failed/skipped,exit0,14s**. GoogleAdapterHttpTests là DelegateHandler GIẢ, không Google live dù tên file có Live.
- Sửa nhánh logout proof đã hết hạn: chỉ được thu hồi capability riêng, không cấp quyền business. Retest filter3case Idle_expiry/Expiry_offline/Cookie_offline_logout: **3/3passed,0failed/skipped,exit0,1s**; không rút ngắn policy hoặc chờ thời gian thật. Nhóm này overlap41,35/46/43 cũng có overlap; không cộng thành tổng độc lập.
- Probe chạy hosted background/coalesced/backoff; known-offline Observe không chờ probe đang treo. SlowProbe TCS chứng minh read hoàn tất ngay khi probe còn chờ, không giả là WAN runtime. Request bind connection epoch: response bắt đầu trước drop/reconnect không mở ghi ở epoch mới, cần fresh read khác. Proof timestamp lấy lúc bắt đầu central validation, SecurityVersion thấp không ghi đè proof mới dù trả muộn.
- Intent chỉ persisted sau central auth/post-auth gate, ngay trước business handler. Auth thất bại trước business không tự tạo UNKNOWN; sau handler có thể đã gửi thì UNKNOWN giữ. Oversize request413 trước auth/mutation. Safe exception response không đưa header/token/body vào log.
- UI reconnect lấy trang nghiệp vụ refreshPath được server chọn theo role, tránh loop khi mở thẳng form/Admin; form dirty giữ, không resend mutation. Nhãn byte cache cập nhật lại nếu missing/evicted; user/session/central/authority/bfcache bindings có source/JS syntax checks, **NOT_RUN_BROWSER**.
- Final Release build **exit0,0warning/error,4.41s**, sau dừng đúng owned PID17388; final start/reuse0/0 cùng PID 19468. C# không đổi sau build; JS tinh chỉnh source chỉ retest `node --check` exit0. HTTPS strict/public6case và workspace36files nguyên vẹn ở runtime JSON cuối.


## Checkpoint cuối — scope/coherence/binding, thay phần “cuối” ở các vòng trước

Vòng cuối **46/46passed,0failed/skipped,exit0,15s**; hồi quy cuối cùng35/35passed,0failed/skipped,exit0,14s. Release cuối **exit0,0warnings/errors,5.03s** sau dừng đúng owned PID19468. Các số41/44/45 và build4.41s bên trên là checkpoints trước vòng này, giữ lịch sử; không cộng checkpoints thành tổng test độc lập.

ObservedBusinessStores delegate đúng Google stores/commits hiện có, không thay guard/core hoặc phát sinh RPC. Dependency/checkpoint từ các state thực đã đọc chỉ lưu private: khi module/ref checkpoint đổi, cache query và bytes phụ thuộc bị loại; không phục hồi revision cũ từ response muộn, kể cả request có validation timestamp cũ nhưng data mới. Connection epoch mới purge corpus cũ sau revalidate, không ghép snapshot của hai epoch. Đọc trong một response có hai version khác nhau hoặc đi qua connection cycle trả409, không cache/publish partial data là current. Không là checkpoint toàn bộ database hoặc mirror mọi query; scope coverage giới hạn vẫn giữ như trên.

Binding: ticket MAC chứa credential digest, bytes cipher và RAM index theo phiên+snapshot filename; testcase copy ticket/bytes A sang B không authenticate/decrypt được. Không raw opaque token/password/hash đăng nhập/Google credential trong cache. Prototype cache thiếu binding không đủ điều kiện offline; cần online xác minh lại, giữ files/intent và không reset account hoặc workspace v1. Operation lease HTTP/CLI kiểm mỗi RPC trước OAuth và trước transport sau pacing: tác vụ cũ không tiếp tục RPC sau reconnect, pending key/UNKNOWN giữ cho request mới revalidate/reconcile.

Vòng44 đầu43passed/1failed exit1 ở fixture offline browser resume: fixture dùng bearer qua redirect, request tới /Offline không giữ Authorization nên401. Sửa fixture dùng dt_session cookie như launcher/browser (không nới expected). Vòng44/44exit0 sau sửa. Mở Login khi offline với cookie/proof hợp lệ tự redirect /Offline; chưa có hoặc hết hạn vẫn đòi online. Test còn đọc wrapper filesystem thật để phát hiện mixed revisions, invalidate cached detail/photo khi asset checkpoint đổi dù grant không đổi.

Native cuối **PID 24384**, một process owner/exe đúng, launcher start/reuse0/0 cùngPID, strictHTTPS6/6public/anonymous expectations; JS đang phục vụ byte-for-byte đúng source. Workspace v1 nguyên36files. [Runtime](TASK-015-ONLINE-RUNTIME.json), [check bằng chứng](TASK-015-ONLINE-CHECK-EVIDENCE.json). Không authenticated Google/browser/network transition thật; NOT_RUN không đổi. TASK-015 IN_REVIEW, không commit/push/deploy/mobile.


Đối chiếu cuối:39files modified/untracked,127local links,4JSON parse,0issue; v1 histories5/5 giữ nguyên; application/auth/password-policy/legacy adapter/tree guard và Task003/004/005/UAT checklist diff-quiet exit0. HEADd8a508d không đổi, index rỗng, source JS khớp byte đang phục vụ. Scan pattern không là security audit toàn diện. Chỉ doc/evidence edit sau build; không chạy lại build/test vì không đổi C#.


## Sửa lỗi đăng nhập thực tế — 2026-10-15 (đang chờ consent)

Nguồn: **AI chạy/quan sát trực tiếp** các lệnh bên dưới; lỗi form ban đầu và việc không thấy cửa sổ consent là **người dùng báo**. Không có mật khẩu, token, cookie, hash hoặc body Google trong evidence. [JSON](TASK-015-LOGIN-FIX-EVIDENCE.json).

**Nguyên nhân đã chứng minh:** credential OAuth hiện có bị Google từ chối với HTTP400, mã allowlist `invalid_grant`. Bảy telemetry GoogleOAuth POST400 đã có trong log riêng. Probe refresh token trực tiếp (Python urllib/TLS validation bật, chỉ xuất code/status) trả400/invalid_grant, exit0 của probe vì đã ghi nhận kết quả; đây là **FAIL Google auth**, không phải OAuth thành công. Chưa xác định vì sao grant bị vô hiệu (expiry/revoke/khác); không suy diễn thời hạn Testing. Lỗi xảy ra trước Sheets, chưa có bằng chứng hash/password/admin hoặc schema bị lỗi.

Runtime đầu lượt: record PID24384 nhưng process đã kết thúc, cổng7008 không listener/nativebackend0. User-secrets của MAY03\May 03 đầy đủ6setting cần thiết (chỉ kiểm bool); không environment Storage/DOTNET/ASPNETCORE override trong shell chẩn đoán. Không reset/init hoặc sửa resource IDs.

Chuỗi DI thực: `Server.exe auth-diagnose` ở Debug và Release, Development/Google/cache enabled, OAuth+system settings đủ, system khác business, probe **Connected**. Cả hai **exit1**, `ExternalStorageAuthenticationException`, HTTP400/invalid_grant, chưa đọc được auth tables. TLS Microsoft probe bị NameMismatch nhưng Google204 xác minh transport; không bỏ TLS, không coi OAuth400 là mất Internet/offline. Config/schema/429/503 guard giữ nguyên.

Sửa: parse bounded tối đa4096bytes OAuth error và allowlist code, không retain/log description/body; Login bắt cancellation đúng, failure503 và log type/status/code/correlation, không raw exception; không đổi invalid-password/lockout/hash/session policy. CLI `auth-diagnose` có system-writer lease và chỉ ReadAsync (không mutation/init/journal/password check). Launcher đặt cả DOTNET_ENVIRONMENT/ASPNETCORE_ENVIRONMENT Development, khôi phục env shell sau launch; runtime technical status có buildId/environment/storage để đối chiếu executable. Probe bỏ default verbose HTTP exception logger. Regression xác minh first online login không cần cookie/proof/cache; không nới business write gate.

Lệnh thực tế từ repo root (build/test tuần tự):
- `dotnet test tests/DuyTien.AssetManagement.ArchitectureTests/DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~GoogleOAuthTokenProviderTests|FullyQualifiedName~LoginFailureDiagnosticsTests|FullyQualifiedName~ConnectedCacheTests|FullyQualifiedName~GoogleAuthSystemStoreTests"`: vòng đầu77/77 exit0/16s; thêm case first-online-login, vòng cuối **78/78passed,0failed/skipped,exit0,16s**. Có overlap tests cũ, không cộng hai vòng. Fake HTTP/auth không Google pass.
- `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj -c Release --no-restore --nologo -v:minimal`: **exit0,0warning/error,00:00:05.17**.
- PowerShell AST Start-Web/Reconnect-GoogleLogin và `git diff --check`: exit0.
- Native Release `auth-diagnose` từ thư mục Server với env Development/Google: **exit1 OAuth400/invalid_grant**; buildId `cc896f93-d1c4-4a57-84e7-b322f38f2659`.
- `Start-Web.ps1 -NoBrowser` qua child PowerShell, stdout/stderr riêng ngoài Git: exit0; **PID21540**, nativecount1, Windows owner/exe/start-time đúng. StrictTLS /api/system/status200, Login200, LocalSetup410; status Google/Development/Connected/buildId khớp CLI. Anonymous RECONNECTING không phải auth ONLINE proof.

Agent mở `google-oauth-bootstrap` bằng Desktop flow hiện có sau khi chứng minh invalid_grant; người dùng không thấy browser. Không nhận callback/không lưu credential mới. Dừng **đúng** listener53682 PID24228, exe+command matched; unified exec quan sát exit-1 do stop, không báo consent PASS. Helper `scripts/Reconnect-GoogleLogin.ps1` đã chuẩn bị để người dùng chạy trong PowerShell desktop: stop đúng backend, chỉ consent nếu auth-diagnose invalid_grant, dùng lại Desktop app/drive.file, auth-read sau consent rồi start một backend trong finally. Không bootstrap admin/schema/resource. Không yêu cầu mật khẩu qua chat hoặc secret UAT.

**Gate hiện tại:** BLOCKED_OAUTH_INVALID_GRANT cho read Google auth/schema/admin-assignment thực; consent desktop đang chờ người dùng. Login bằng password cũ trên form **NOT_RUN**; HTTP200 không login PASS. TASK-015 vẫn IN_REVIEW; các gate trước và workspace v1 giữ nguyên. Không mobile/UAT dài/commit/push/deploy.

Bổ sung cuối phần fix: hồi quy `--no-build --no-restore --filter "FullyQualifiedName~AuthApiIntegrationTests|FullyQualifiedName~SafeGoogleHttpClientLoggingTests"` **8/8passed,0failed/skipped,exit0,1s**. Self-review .NET/debugging-strategies/ai-debt-detector/session-guard, không subagent hoặc audit bảo mật độc lập. Baseline fingerprint kiểm toàn bộ tracked/untracked đầu sửa; chỉ thay các file thuộc fix và docs được liệt kê,0 ngoài phạm vi; local links0missing, indexempty/HEADd8a508d giữ. Bằng chứng v1 và các task cũ không ghi đè.


## Xác nhận đăng nhập hiện có — người dùng, 2026-10-15

Người dùng xác nhận “Tôi đã đăng nhập thành công bằng tài khoản hiện có”. Ghi **PASS_MANUAL** cho riêng login tài khoản hiện có sau bước khôi phục kết nối Google; **không phải AI chạy/quan sát login**. Không suy ra consent/provider output, auth-diagnose exit0, schema/assignment, restart, revoke, network transition, freshness, hiệu năng hoặc browser suite PASS. Không thu thập mật khẩu/token/cookie; không chạy lại UAT cũ. TASK-015 **IN_REVIEW**, các gate khác và bằng chứng lịch sử giữ nguyên.

Các đoạn chẩn đoán invalid_grant/PID/build ở checkpoint trước vẫn là lịch sử đúng tại thời điểm quan sát; không mô tả chúng thành lỗi hiện tại sau xác nhận này. Lượt cập nhật kế hoạch không probe/restart backend hoặc Google.
