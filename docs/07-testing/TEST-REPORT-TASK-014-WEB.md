# TEST-REPORT — TASK-014 pha Web

Ngày 2026-10-14 (Asia/Saigon). Working directory: `D:\Dev\QuanLyTaiSan_DuyTien`. Nguồn bằng chứng: **AI chạy/quan sát trực tiếp** cho các checks bên dưới. Không có bằng chứng user-run, manual UAT, browser hoặc Google live mới. TASK-014 giữ IN_REVIEW; runtime HTTPS bị BLOCKED_TLS_TRUST. Android/iOS DEFERRED_BY_USER, không chặn pha Web.

## Phạm vi và mức hoàn thiện

Hoàn thiện local các luồng web đã duyệt TASK-005–011, không thêm báo cáo ngoài R01–R04 hoặc nghiệp vụ công ty chưa xác lập. Menu/Home/Help, điều hướng theo context, validation/loading/empty/409/503/giữ form, registry tree và hướng dẫn đã được bổ sung. Không còn failure trong các checks đã chạy; chưa coi đây là sản phẩm đã nghiệm thu hoặc sẵn sàng production.

Giữ .NET 10, Sheets/Drive, drive.file, single-writer, server scope và mật khẩu tối thiểu 8 ký tự. Không mobile code/build mới, SQL/SQLite, live Google, dữ liệu thật, deploy, stage/commit/push. HEAD main `463896d82629847ebd41866eb3e0cb97becfc2f2` giữ nguyên. Bằng chứng và trạng thái các task trước không được nâng thành PASS/DONE.

## Expected / actual local

| Case | Expected | Actual và nguồn |
|---|---|---|
| Menu/Home/Help | Điều hướng thống nhất, nội dung demo rõ, anonymous/admin/context đúng | PASS_LOCAL_HTTP_RENDERED: Home/Help/Login và 12 module Razor, Category→Asset/Item/R02, Asset→ảnh/Category/R01. Collapse/aria có cùng ID; layout/DOM chưa quan sát trong browser |
| Registry tree | Đối soát đầy đủ schema/owner/ledger/revision; scope trước impacts/count | PASS_LOCAL: production Google adapters qua fake HTTP, Categories/Assets/Inventory/Media và references Item/snapshot/Document/version/photo/DOCX/PDF. Schema lạ/thiếu, owner mồ côi hoặc ledger lỗi dừng trước write |
| Commit/relocate | Một batch cho bốn registry, giữ identity/state/stock/content | PASS_LOCAL: Item/Document thay Category/version với journal/audit; stock, files và published versions không đổi. Hierarchy move giữ fields/version của owner. Không gán quantity 0 hoặc phê duyệt DRAFT |
| Archive | Không tự tạo semantics xóa Item; EMPTY_ONLY kiểm cả lịch sử | PASS_LOCAL: EMPTY_ONLY và SOFT_DELETE bị chặn khi có Item; RELOCATE hợp lệ. Document soft archive giữ bytes/version/file vật lý |
| Scope/replay | Không ghép assignment, không lộ tên/count; retry kiểm quyền lại | PASS_LOCAL: thiếu quyền hồ sơ, split assignment nguồn–đích, revoke replay đều bị từ chối; Viewer không xem hidden detail/file/snapshot hoặc admin navigation |
| Lost response/stale | Không ghi trùng/publish partial; đổi registry cần preview mới | PASS_LOCAL: một joint post khi mất response, re-read/replay; stock revision thay dù tree/asset chưa đổi vẫn 409. Job import pending chặn tree version mới; MOVE_ASSETS không đổi tree pointer vẫn hợp lệ |
| Category import | Không mất ownership Document/file qua full import | PASS_LOCAL: mất/đổi nhánh hồ sơ bị 409 và 0 write; partial Files gây safe failure. Guard Asset/Item cũ được giữ |
| Media delta | Không ghi sai dòng khi reorder, không sửa owner ngoài tác động | PASS_LOCAL: reorder Documents rồi relocate đúng owner, metadata/version ngoài nhánh giữ nguyên; cap batch/cell/row vẫn giữ |
| Snapshot form | GET nạp current values/version; POST lỗi giữ payload/key/version | PASS_LOCAL: scoped prefill; GET version 999 được thay bằng version thực. 503 giữ quantity 6.25/key/version 2 rồi retry đúng yêu cầu; stale/key khác bị 409, không tự nâng version |
| Decimal | HTML number wire invariant, UNKNOWN/negative validation đúng | PASS_LOCAL HTTP vi-VN/en-US: 6.25 không thành 625; raw comma/negative bị 422; quantity trống không seed 0. Không đổi rule service |
| Public status | Config-only, không implicit Google probe | PASS_LOCAL: không request Google fake; state WEB_DEMO_LOCAL, liveVerified=false. Live CLI/adapter giữ nguyên nhưng không gọi trong lượt |
| Browser | Chỉ PASS khi thực sự chạy UI trong browser | NOT_RUN_BROWSER: cua inventory apps=0/browsers=0; tạo IAB/Chrome tab đều báo “Browser is not available”. HTTP rendered không thay browser/UAT |

Fixtures là dữ liệu GIẢ/RAM, không dữ liệu công ty hoặc Google credential/resource thật. ITEM_RECLASSIFY và DOCUMENT_RELOCATE chỉ đổi ownership/category/version theo thiết kế, giữ code/ID/source 20 cells/tracking/state. Marker INVENTORY_V1_TREE/MEDIA_V1_TREE và optional TreeRegistryDetails đã có code local, **NOT_APPLIED_GOOGLE**. Reader vẫn hỗ trợ schema/action cũ; schema lạ/thiếu dừng. Direct Document metadata update vẫn không đổi Category hoặc published content.

## Lệnh thực tế và exit

SDK quan sát: `dotnet --version` trả 10.0.400, exit 0. Build/test/format chạy tuần tự và chờ session kết thúc trước lệnh kế dùng chung output. Lệnh và exact format paths tại [COMMANDS](../COMMANDS.md). Các test có fake Google handler và Kestrel compiled Razor; không chứng minh Google/browser thật.

- Backend build `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal`: lần đầu exit 0, 0 warnings/errors, 10.72 s; lần cuối **exit 0, 0 warnings/errors, 4.17 s**.
- Filter `FullyQualifiedName~WebIntegration|FullyQualifiedName~TreeRegistry`: checkpoint cuối **41/41 passed, 0 failed/skipped, exit 0, 15 s** (18 registry + 23 web cases).
- Filter hồi quy gồm GoogleTreeMutationTests, TreeMutationTests, GoogleCategoryCatalogStoreTests, InventoryGoogleTests, ManagedMediaGoogleTests và InventoryHttpRazorTests: **85/85 passed, 0 failed/skipped, exit 0, 3 s**.
- Final combined hai filter trên, sau format: **126/126 passed, 0 failed/skipped, exit 0, 15 s**. Không cộng các checkpoint thành số test độc lập hoặc báo toàn suite/UAT task trước đã pass.
- `dotnet format whitespace`: Application 8 files, Google 4 adapters, Server 5 C# files, tests 2 files; từng lệnh exit 0. Không format Mobile/Reports/tests ngoài phạm vi.
- `node --check src/DuyTien.AssetManagement.Server/wwwroot/js/site.js`: exit 0. Syntax không chứng minh DOM/browser behavior.
- `git diff --check`: exit 0 ở các checkpoint code; HEAD giữ, index rỗng. Đối chiếu tài liệu cuối ghi ở cuối report.

### Lỗi phát hiện và sửa

1. Registry ban đầu: 17 total, 0 passed/17 failed, exit 1. Allowlist thiếu CodeRules/OrgUnits/Locations mà Asset adapter đã validate; bổ sung đúng ReferenceHeaders, giữ unknown guard. Fixture publish dùng sai document version 1, sửa thành 2 theo service. Lần tiếp 17 total, 14 passed/3 failed, exit 1: fixture grant chỉ có Category thiếu Org/Location và array equality theo reference; sửa fixture đủ ba scope và so giá trị, không nới quyền production. Sau sửa 17/17, exit 0.
2. Hồi quy: 77 total, 75 passed/2 failed, exit 1. Candidate cycle khiến registry cố dựng hierarchy invalid; null ledger result gây NullReference trước typed validation. Preview dùng hierarchy hiện hành chỉ khi candidate đã có issue (CanCommit vẫn false); null result trả lỗi ExternalStorage an toàn. Sau sửa 77/77, exit 0.
3. Web fixture compile exit 1, CS7036 thiếu TimeProvider cho CategoryPreviewCache, chưa chạy test; sửa DI. Lần chạy 35 total, 32 passed/3 failed, exit 1: test client thiếu Accept HTML nên endpoint trả đúng 401/403; đồng thời phát hiện bug thật culture vi-VN parse 6.25 thành 625. Sửa fixture header và binder nullable invariant, thêm culture/comma/negative cases. Sau sửa 35/35, exit 0; thêm delta/culture 40/40, exit 0; thêm status 41/41, exit 0. Không xóa/skip/làm yếu test hoặc đổi business rule để pass.

Self-review theo .NET patterns, interaction-design và ai-debt-detector; session-guard dùng checkpoint/rules cho lượt dài. Không subagent/independent reviewer hoặc tuyên bố audit bảo mật toàn diện.

## Runtime, TLS và một backend

Start-Process -WindowStyle Hidden chạy `dotnet run --project src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj --no-build --no-restore --launch-profile https` tại working directory trên. Trước start, không quan sát thấy listener 7008/5211 hoặc backend/W13. Lệnh launch exit 0; launcher PID **13756**, backend child PID **23376**. Server chưa kết thúc nên **không có server exit code được quan sát**. Log ở LocalAppData/DuyTienAssets/Task014Web, ngoài repo; không in log hoặc cấu hình bí mật.

Đối chiếu cuối: IPv4/IPv6 7008/5211 đều thuộc PID 23376, một backend; launcher 13756 là parent. Implementation giữ process lease qua app.RunAsync. Probe anonymous HTTP root (không cookie/auto-redirect) trả **307 → https://localhost:7008/**; script exit 0.

HTTPS public `/`, `/Help`, `/api/system/status` bằng HttpClient mặc định đều NOT_VERIFIED, exception chain gồm MethodInvocationException/HttpRequestException/WebException/AuthenticationException. TLS 1.2 probe trước đó cũng thất bại. `dotnet dev-certs https --check` exit 0 cho thấy cert localhost hợp lệ; read-only X509Chain.Build trả **ChainValid=false, UntrustedRoot**, script exit 0. Không ghi thumbprint; chưa tạo/trust/export cert hoặc tắt TLS validation. Exit 0 của probe chỉ nghĩa script hoàn tất báo trạng thái, không HTTPS PASS.

URL dev **https://localhost:7008** đang lắng nghe; client hiện bị **BLOCKED_TLS_TRUST**. HTTPS body/configurationPresent chưa quan sát thành công. Server listening/config-only status không chứng minh Google/schema/ACL/grant. Chỉ probe public, không login/Google/gate đã hoãn; không tạo backend thứ hai hoặc yêu cầu người dùng UAT/trust certificate ngay.

## Gate còn mở

- **BLOCKED_TLS_TRUST**: client certificate chain UntrustedRoot; HTTP redirect đạt nhưng HTTPS body chưa xác minh.
- **NOT_RUN_BROWSER**: không có browser provider; layout/DOM/loading/native file control/download/keyboard/viewport chưa quan sát. Không thay bằng HTTP rendered.
- **NOT_RUN_GOOGLE / DEFERRED_BY_USER**: schema/marker mới trên resource thật, owner/ACL/drive.file/coherence/quota/throughput/restart/backup-restore chưa chạy. Không provisioning/init/bootstrap/PoC hoặc lặp gate đã hoãn.
- **OPEN/TBD**: workbook công ty, code/required fields, OrgLocation, stock/source/minimum, retention, host/domain và policy sản xuất. Fixtures/DRAFT không phải dữ liệu đã duyệt.
- **DEFERRED_BY_USER mobile**: Android TASK-012 IN_REVIEW giữ local 42 tests/build, phát triển/runtime hoãn; iOS TASK-013 TODO giữ target/scaffold, execution hoãn vì môi trường/thiết bị chưa sẵn. Không đổi task trước thành DONE hoặc tự chuyển Android.

[Web guide](../WEB-USER-GUIDE.md), [operations](../WEB-OPERATIONS.md), [thiết kế trước code](../02-architecture/WEB-COMPLETION-DESIGN-TASK-014.md), [evidence](TASK-014-WEB-LOCAL-EVIDENCE.json). Bước tiếp theo thuộc Web là xử lý các gate trên khi có phạm vi cho phép; chưa tự chuyển mobile.

## Đối chiếu tài liệu/worktree cuối — AI chạy/quan sát trực tiếp

Checker đọc UTF-8 các file text trong 115 paths modified/untracked, parse JSON và kiểm 164 liên kết Markdown local: 0 lỗi UTF-8/JSON/link, 0 artifact path bất thường. Checker trạng thái ban đầu chưa nhận backtick trong `Status: IN_REVIEW` của TASK-006; sửa parser nhận cả hai dạng, kiểm lại TASK-003–014 đều đúng trạng thái, exit 0. Không sửa TASK-006 để khớp parser.

Đối chiếu SHA-256 có normalize hoa/thường: 67/85 file baseline giữ nguyên; 18 file dùng chung được mở rộng đúng phạm vi Web, 0 file baseline mất. 30 paths ngoài baseline là code/test/UI/Web docs được bổ sung hoặc sửa trong lượt này. Mobile source/tests, Reports source/tests, Test Report/evidence TASK-010/011/012 giữ nguyên fingerprint; TASK-012.md chỉ bổ sung lịch hoãn, Inventory/shared hooks được mở rộng có chủ đích. Không so worktree Mobile với HEAD để nhầm thay đổi đã có thành thay đổi mới.

`git diff --check`, `git diff --cached --quiet`, quiet check auth và active script mỗi command exit 0; HEAD main463896d không đổi. Không stage/commit/push/reset/clean. Pattern/path checker không phải audit bảo mật toàn diện. Process/listener kiểm lại: launcher 13756 → backend 23376, bốn listener IPv4/IPv6 thuộc cùng backend; shell exit 0. Public probe cuối vẫn HTTP 307/HTTPS NOT_VERIFIED, không gán PASS mới.

Sau cập nhật evidence/report chỉ có doc edit; không chạy lại build/test đã đạt nếu không đổi code.

## Bằng chứng tiếp theo — 2026-10-14

[Report Google DEV/HTTPS](TEST-REPORT-TASK-014-GOOGLE-DEV.md) bổ sung live thật và sửa429 sau checkpoint local này. Các ghi chú TLS blocked/Google chưa apply/HEAD463896d ở trên giữ lịch sử; không đổi thành PASS hồi tố. Browser/cookie mới/actual restore/source vẫn còn gate.
