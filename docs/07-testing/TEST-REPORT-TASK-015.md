# TEST-REPORT — TASK-015 LocalOffline

2026-10-14 (Asia/Saigon), D:\Dev\QuanLyTaiSan_DuyTien. Nguồn: AI chạy/quan sát trực tiếp, trừ phân tích HTML do người dùng cung cấp. Không đọc HTML/dataset công ty. HEAD đầu lượt139155f; worktree sạch. Không commit/push/deploy/mobile hoặc ghi Google trong lượt này.

## Đo trước thay đổi

[TASK-015-BASELINE.json](TASK-015-BASELINE.json) lưu16samples của pipeline Google adapter cũ bằng HTTP handler GIẢ, auth memory fixture nhỏ, pacing không bật. 1/1 test exit0. Categories2calls, Assets4calls; Items/report có đọc metadata từng store. Không dùng số ms này làm baseline latency Google thực hoặc tỷ lệ tăng tốc so full fixture local. Backend Google đầu lượt không còn process; credential ứng dụng tạm đã xóa từ lượt trước. Login/protected Google cold/warm/auth/pacing/deserialize/render thực NOT_RUN_NO_APP_CREDENTIAL; không yêu cầu secret/UAT mới và không tạo phiên Google để lách thiếu bằng chứng. Lỗi429/pacing của TASK-014 chỉ tham chiếu lịch sử, không chạy lại gate.

Review code: session validation đi qua Mutate, business GET dùng shared coordinator; từng store metadata/materialized/journal được đọc/đối soát lại. Local thay bằng validated RAM snapshot, read pinning không lấy writer gate. Google default read consistency và fail-closed giữ; request auth cache giảm reread trong cùng request. Không bỏ pacing1250ms/cooldown60s.

## Kiểm tra đã quan sát (lịch sử các vòng)

- Restore project mới exit0 (packages cache; không runtime restore). Lệnh test dùng nhầm --ignore-failed-sources không hợp lệ exit1, đã tách `dotnet restore ... --ignore-failed-sources` khỏi test.
- Các compile errors helper Equal/RoleExists/PageModel.Conflict được sửa theo implementation thật; không thay policy/test cũ.
- Persistence test đầu1failed exit1 do IgnoreReadOnlyProperties làm mất get-only collections auth; sửa serializer,1/1pass exit0.
- Fixture STT sai8failed/1passed exit1; sửa fixture về1/1.1/1.1.1, không nới guard. Sau đó9/9pass exit0.
- Kiểm pending upload: test dùng sai exception ExternalStorageTransient trong khi service contract trả MediaPending; xác minh state/key/bytes và sửa expectation đúng contract. 10passed/1failed exit1; sau sửa12/12pass exit0.
- Bổ sung demo initializer explicit, không seed startup;13/13 local filesystem+HTTP/Razor checks pass exit0,26s trước vòng kiểm cuối. Gồm commit before/after manifest failure, retry/stale, lease, corrupt checksum, detached data, read không chờ writer, pin snapshot, photo/DOCX/PDF/publish/tree registry/restart, scope/last-admin/revoke, refresh rotation/reuse, cookie/anti-forgery/form giữ khi disk fail. Fake dữ liệu, production local adapter, không fake runtime.
- Hồi quy207checks:203passed/4failed exit1 do fast auth read áp dụng lên adapter legacy, làm lỗi writer không trả503 như contract. Đã giới hạn fast path bằng capability LocalOffline, cache successful mutation state trong request; không sửa test cũ. Sau sửa **207/207passed,0failed/skipped,exit0,1m13s**. Không sửa expectation của test cũ.
- Build backend Debug các checkpoint exit0,0warnings/errors; benchmark ghi riêng startup/login/cold/warm và phases tại [JSON](TASK-015-BENCHMARK.json). Cold là request đầu host/process test đã có JIT, không OS cold boot. 20warmGET mỗi URL,10warmlogin POST, fixture665(15/98/552)/20744Items,5Assets; dữ liệu GIẢ, không phê duyệt nghiệp vụ.

## Giới hạn

NOT_RUN_BROWSER: CUA apps0/browsers0; không DOM/layout/browser network trace/download hoặc thao tác thủ công mới. HTTP rendered không browser pass. Zero managed external HTTP đo bằng EventListener (loopback allowed) và production DI reject handler; không là packet capture mọi OS/browser process. Intentional negative outbound probe bị chặn trước transport, tách khỏi flow zero-outbound.

Google→local transfer/actual restore/real source NOT_RUN. Không copy Google auth/session hoặc manifest Drive thiếu bytes. Local chỉ nhận bytes LOCAL có size/hash đúng; AVAILABLE thiếu/corrupt bytes làm startup fail closed. Không actual power-loss/hardware-disk-full/network-drive test; before/after manifest fault injection chỉ chứng minh các checkpoint tương ứng. Workspace local NTFS, owner/SYSTEM ACL; không claim distributed CAS hoặc an toàn mọi kiểu mất điện. Generations trước được giữ cho recovery, dung lượng journal/snapshot tăng theo sử dụng, chưa có tự động compact.

Nguồn/rule/OrgLocation/ngưỡng vẫn OPEN/TBD. TASK-005–014/status/gates hoãn giữ. Android tạm dừng, iOS DEFERRED_BY_USER. TASK-015 giữ IN_REVIEW; xem tổng kết cuối bên dưới.

## Kết quả cuối — adapter thật và executable thật

TASK-015 IN_REVIEW, phần local hoàn tất; không đồng nghĩa các gate browser/nguồn/Google cũ đã đạt. Build Release cuối `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj -c Release --no-restore --nologo -v:minimal`: **exit0,0warnings/errors,6.15s**. SDK10.0.400. Root single-writer, ref/rule immutable trong commit thường, CAS manifest,8creates đồng thời unique codes/IDs được kiểm thêm cuối.

Local final `dotnet test ... --filter "FullyQualifiedName~LocalOfflineTests|FullyQualifiedName~LocalOfflineHttpTests|FullyQualifiedName~GoogleSheetsRequestBudgetTests"`: **21/21passed,0failed/skipped,exit0,27s**. Bao gồm6budget tests có overlap nhóm207; không cộng thành tổng không trùng. Vòng20checks trước có1failure fixture SourceHash placeholder sai contract; sửa fixture thành rỗng khi không có file gốc, không nới SHA256 validation; case riêng1/1pass exit0, các vòng20/20 rồi21/21pass. Formatter chỉ44file C# theo [danh sách](TASK-015-FORMAT-EVIDENCE.json),5project exit0; sau bổ sung guard/test format lại LocalWorkspace/2testfiles exit0. JS `node --check` exit0; PS AST start/stop OK. Python runner AST OK, không pyc.

Actual executable/Program/DI/launcher/.NET runtime khác host test: `python -B scripts/Test-Task015OfflineRuntime.py --workspace <private FAKE-TASK015 workspace> --output docs/07-testing/TASK-015-RUNTIME-HTTPS.json`: **128/128assertions,exit0,complete=true,ownedBackendStopped=true**. [Evidence cuối](TASK-015-RUNTIME-HTTPS.json). HTTPS validation bật; không -k/bypass TLS. Profile fake exported từ fixture đã kiểm, ngoài Git, không auth/session/credential Google; không phải default workspace. Các check gồm strict login/cookie, local resources,5pages/paging, actual API create/replay, successful edit/replay,409 stale, photo bytes/upload/retry/gateway,0outbound attempts trước/sau login/write/open/report và restart thực giữ asset/phiên/cookie/bytes. Source/state luôn GIẢ/DRAFT; field12cột HTML không được nhập.

Runtime runner history được giữ riêng: [run1](TASK-015-RUNTIME-HTTPS-RUN1.json) exit1 sau AI dừng đúng fixture backend để giải phóng inherited stdout pipe;0assertions, không product PASS. Sửa operator subprocess dùng private file handles/stdin DEVNULL. [run2](TASK-015-RUNTIME-HTTPS-RUN2.json) exit1 do PowerShell Set-Acl đòi SeSecurityPrivilege; sửa launcher dùng .NET DACL-only operation như adapter, không xin admin/broader ACL. [run3](TASK-015-RUNTIME-HTTPS-RUN3.json)115/116assertions exit1: runner kỳ vọng200 sai contract POST201 và cần PATCH update; actual status lần đó không được lưu, không suy diễn. Sửa runner đúng implementation, không đổi API validation, không xóa dữ liệu đã ghi. [run4](TASK-015-RUNTIME-HTTPS-RUN4.json)126/126assertions exit0; sau review guard cuối, rerun final128/128exit0. Retry dùng cùng key, giữ file/asset cũ. Không tổng hợp các checkpoints thành số assertions độc lập.

## Benchmark sau — actual Release HTTPS

Profile GIẢ665nút15/98/552,20744Items20cột, có Assets riêng, không source công ty. 1cold-first request +20warm mỗi URL, Python client TLSvalidation bật, full response download. Đây không browser paint/layout hoặc cold OS boot. Lần cuối:

| GET | Cold-first ms | Warm p95 ms | Warm samples | Mục tiêu<500ms |
|---|---:|---:|---:|---|
| categories | 58.60 | 29.83 | 20 | ĐẠT fixture |
| assets | 60.65 | 35.50 | 20 | ĐẠT fixture |
| items | 109.99 | 106.82 | 20 | ĐẠT fixture |
| reportR01 | 64.20 | 34.87 | 20 | ĐẠT fixture |
| reportR04 | 391.82 | 248.81 | 20 | ĐẠT fixture |

Razor login actual Release: GET cold126.86ms, POST cold142.77ms. Login warm được đo thêm ở real Kestrel/compiled Razor+filesystem host test: GET p958.22ms, POST p9594.35ms,10samples mỗi loại; không đổi nhãn thành process/TLS benchmark. Host startup1545.73ms, workspace load/validate1246.29ms; phase CPU/IO/replay/index trong [benchmark JSON](TASK-015-BENCHMARK.json). Các phase lồng nhau (reconcile bao gồm replay), không cộng chồng. Warm read không deserialize/replay journals hay lấy business writer; auth/clone/render/request đo riêng. Auth cache chỉ một request; revoke/quyền request sau vẫn fresh. Local Google calls0. Target đạt trên fixture/máy hiện tại, không benchmark dữ liệu công ty hoặc throughput nhiều user.

Không có baseline protected Google latency thực trước sửa do backend không chạy/không còn app credential. Baseline handler nhỏ chỉ chứng minh repeated metadata/calls; không dùng để tính tỷ lệ tăng tốc hay giả before/after cùng điều kiện. Google mode giữ writer/pacing/cooldown và freshness; tối ưu chính là LocalOffline explicit, không bỏ guard/quota.

## Runtime sử dụng hiện tại

[Default proof](TASK-015-DEFAULT-RUNTIME.json): launcher exit0, một backend **PID25112** tại **https://localhost:7008**, Windows owner khớp current user, public6URL200 với certificate validation. Server còn chạy, không có exit code kết thúc server. Workspace `%LOCALAPPDATA%\DuyTienAssets\LocalOffline\default`, ID tách fixture. Không copy fake20744dataset vào default, không auto bootstrap/reset/admin/rule. `/Account/LocalSetup` khả dụng cho workspace mới; demo GIẢ chỉ nút explicit Admin. CMD/PS launcher không restore/build/download mỗi lần; branch OS browser-open được triển khai, chưa xác minh UI. Backend phải chạy, không PWA/offline DB.

CUA inventory apps0/browsers0; `createBrowserTab(iab,https://localhost:7008)` báo Browser is not available:iab. **NOT_RUN_BROWSER**, không giao UAT mới. Actual process status0outbound là DI counter; unit EventListener0external managedHTTP cho flow bình thường; không nâng thành OS/browser packet capture. No source/Google migration/actual restore/power-loss thật gate pass. Default/fixture private workspace/log/auth/keys/blobs đều ngoài Git; generations cũ giữ, chưa automatic compaction.

TASK-003 IN_REVIEW,TASK-004 DONE; TASK-005–014/status/gates hoãn giữ. Android tạm dừng,iOS DEFERRED_BY_USER. HEAD139155f giữ; không stage/commit/push/deploy, không dữ liệu thật/Google writes hoặc mobile work.

## Đối chiếu cuối worktree / tài liệu

[Checker](TASK-015-CHECK-EVIDENCE.json) exit0:85paths trước tạo chính evidence,132local links,13JSON/Python/project parses,0issues; diff --check exit0/indexempty/HEAD139155f không đổi. Protected TASK-003–014 và toàn bộ source mobile không đổi. Lần check đầu chỉ false alarm do TASK-006 ghi Status trong backticks; sửa checker, không sửa trạng thái Task. Không phát hiện credential pattern/artifact paths trong phạm vi scan, không phải security audit toàn diện. Bằng chứng sau này có thêm chính checker; không cộng thành test pass. Doc-only append này được diff check lại trước kết thúc.
