# TEST-REPORT — TASK-011

Date: 2026-10-13 (Asia/Saigon). Working directory D:\Dev\QuanLyTaiSan_DuyTien. Nguồn bằng chứng: **AI chạy/quan sát trực tiếp** cho các lệnh/checks bên dưới. Không user-run/manual/browser/Google live evidence mới; fixture GIẢ và HTTP adapter fake. Không đánh dấu gate thật PASS.

## Phạm vi và trạng thái

Chỉ R01–R04 REQUIRED. TASK-011 IN_REVIEW sau hoàn tất local. Người dùng cho phép bắt đầu khi TASK-007/010 IN_REVIEW, giữ trạng thái/gates/DEFERRED_BY_USER/STOPPED_BY_USER cũ. CSV UTF8BOM là lựa chọn Task kỹ thuật local; không XLSX/PDF/R05–R15/stale-policy/MAUI. Sources DRAFT/demo/workbook/rule/stock/ngưỡng/OrgLocation chưa được phê duyệt.

Đầu lượt Git main HEAD463896d82629847ebd41866eb3e0cb97becfc2f2,39modified/untracked TASK-010. Không stage/commit/push/reset/clean; baseline fingerprint nội bộ để đối chiếu protected source/test/evidence. Dùng .NET/backend patterns, self-review theo ai-debt-detector/session-guard đã đọc; không agent/subagent/independent reviewer hoặc tuyên bố audit bảo mật toàn diện.

## Expected / actual local

| Case | Expected | Actual và nguồn |
|---|---|---|
| R01 Assets-only | Không deleted/invalid refs, count khác Items, filters và scoped page | PASS_LOCAL memory/API/HTTP, source20744Item giả không tăngAssetCount |
| R02 tree | ParentId/current tree, direct/subtree/unique/cấpF, không double sum/hidden ancestor | PASS_LOCAL core/current-tree-change/multiple scope/path tests, fakeGoogle reread |
| R03/R04 | Zero vs0<qty<=min; qtynull UNKNOWN, nonstock/unconfirmed khác; thiếu min/source rõ | PASS_LOCAL nullable/units/source/asOf/dataquality/Supplement sections; không Company source proof |
| No inference | Không seed0/min, không BOM/warehouse→qty/location, không historical valuation | PASS_LOCAL ItemWithoutVisibleSnapshot noLoc/noAsOf/noQty; date/geo chỉ actual snapshots |
| Scope | Trước filter/count/aggregate/page/export, ba chiều và action/data cùng assignment | PASS_LOCAL hidden branch/source/counters, widerViewer+narrowExport, revoke/expired authority existingcore, hiddenOverflow |
| Unit/count | Exact units riêng, không grandQty; Item distinct vs snapshot count | PASS_LOCAL two-unit sums nullable/knowncount/UNKNOWN/missingMinimum overlap, page total trướcpaging |
| Large source | Không20k client payload mỗi page; scoped summary rồipage | PASS_LOCAL synthetic20744Items,10372eligible/7rows payload<20000chars; không dữ liệu công ty/performance Google |
| Export | Fresh export scopes, toàn filter không page, UTF8BOM/quoted/Unicode/CSV formula guard | PASS_LOCAL CSV allrows, =+-@/TAB/CR/BOM-prefix, attachment/nosniff/header, source markers |
| Failure | Corrupt sources503 safe, không empty/0 giả, không report write | PASS_LOCAL fakeGoogle headers/guard/no newPosts và failure/upstream/permission tests |
| UI | Defaults/filter/dropdowns/page/error/empty/loading/asOf/unit | PASS_LOCAL_API_HTTP_RENDERED/Razor binding+content+links; NOT_RUN_BROWSER DOM/layout/download |

Thiếu minimum là check độc lập có thể overlap OUT/UNKNOWN; không cộng các check counts. Item catalog không snapshot trong phạm vi là N/AOrg/Loc; hidden snapshot không thay đổi row/counters/choices nên không suy ra bên ngoài. Report không phục hồi record scope thiếu/ngừng, kể cả Admin. Snapshot timestamps current materialized, query date range không time-travel. Không public global Asset/Inventory revision counters hoặc rawItems20cells.

## Lệnh thực tế / exit

Working directory như trên; exact commands và scoped format paths tại [COMMANDS](../COMMANDS.md). Build/test chạy tuần tự, mọi session đã chờ exit trước lệnh kế shared output. Filter `Report` bao gồm tests mới và tests liên quan có tên matching; không báo toàn solution/UAT các task trước pass.

- `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal`: các lần exit0/0warnings/errors; lần cuối TimeElapsed3.79s. Source/runtime.NET10.0.400.
- `dotnet test tests/DuyTien.AssetManagement.ArchitectureTests/DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~Report"`:49/49passed exit0 đầu core; sau HTTP61total/58passed/3failed/0skipped exit1 (binding nullable defaults); sửa binding +default URL test62/62exit0; thêm fakeGoogle68/68exit0; CSV/asOf/empty-page75/75exit0; synthetic/current-tree/overflow +format: **78/78passed,0failed/0skipped,exit0,3s** cuối.
- Regression `--filter "FullyQualifiedName~AssetCrudHttpRazorTests|FullyQualifiedName~InventoryHttpRazorTests|FullyQualifiedName~ScopeAuthorizationTests|FullyQualifiedName~GoogleTreeMutationTests"`: **36/36passed,0failed/skipped,exit0,2s**. Không rerun full TASK-005/006/007/008/009/010 UAT/test suite.
- `dotnet format whitespace` riêng Application5report files, Server2reportfiles/Program, tests3report files với --no-restore --include exact paths: exit0 từngproject. Không format Inventory/TASK-010 files.
- `node --check src/DuyTien.AssetManagement.Server/wwwroot/js/report-filter.js`: exit0; `git diff --check`, staged empty/history/guard checks exit0; `dotnet --version` exit0 10.0.400. Probe Get-CimInstance exit0, matchedbackend0/W13runner0, không dừng process khác.

## Lỗi phát hiện / sửa

HTTP/Razor61case có3failure: query thiếu Section bị implicit Required400, Code mặc định cũng có nguy cơ tương tự. Đổi optional Code/Section thành nullable và fallbackR01/MATCHES, giữ invalid query validation; thêm default-URL regression. Không đổi expected422 hoặc skip tests. Sau sửa62/62pass, các checkpoint cuối vẫnpass.

Self-review tăng byte-bound CSV trong append (chống buffer quá lớn/silent partial), formula guard sau whitespace/BOM, loại timestamp không hợp lệ khỏiusable asOf range và empty-page feedback giữ scopedtotal thật; thêm case. Không viết source/test của task trước để làm reportpass, không bypass metadata guard. All synthetic resources/fake creds chỉ tests/RAM, không real secrets hoặc temporary UAT credential.

## Gate chưa xác minh

- **NOT_RUN_GOOGLE**: actual credentials/resources/ACL/drive.file reads, realtime changes/coherence/quota/latency/throughput/restart, exported dataset thật; fake handler no businessPosts không Googlepass. Single backend/writer giả định demo, không distributed CAS/cacheSQL.
- **NOT_RUN_BROWSER/UAT**: DOM/filter/defaults/layout/loading/keyboard/mobileviewport/download/CSV mở trong browser/spreadsheet reader và UATfilter AC; rendered HTTP không browserpass. Không giao checklist UAT theo chỉ đạo.
- **NOT_VERIFIED_SOURCE/OPEN_TBD**: workbook VTHH/tree gốc, sourceCode/header/typecounts20744/20743/CĐKEM839-840, business rules/OrgLocation/stock/ngưỡng/unit chuẩn chưa đối chiếu. Fixtures GIẢ/declared min/source không phê duyệt công ty. Source/minimum missing giữ trạng thái, không gán default policy.
- Guard TASK-008 nguyên vẹn, registry Items/StockBalances/Files/Documents chưa đối soát thì tree mutation vẫnfail-closed; reports chỉ đọc separate stores. Không áp dụng/schema hoặc làm Task012.

TASK-011 IN_REVIEW. TASK-003 IN_REVIEW,TASK-004 DONE; TASK-005–010 IN_REVIEW/gates hoãn và lịch sử giữ. TASK-012–014 TODO. Phần local TASK-010 vàTASK-011 chưa commit, HEAD463896d giữ. Bước tiếp theo thuộc task là source/Google/browser gates khi có phạm vi cho phép; không tự yêu cầu người dùng UAT/secret hoặc bắt đầu task sau.

## Đối chiếu worktree/tài liệu cuối — AI chạy/quan sát trực tiếp

Checker UTF8 Python quét56file modified/untracked và103local links/JSON/status TASK-003–012,0missing-link/credential-pattern/status issue; exit0. Pattern scan không là security audit toàn diện và không in credential. `git diff --check`, index-empty và history/auth/OAuth/guard/activeScript quiet từng subprocess exit0; HEAD463896d không đổi.

So baseline39file đầu lượt:27file giữ nguyên fingerprint (bao gồm nguồn/tests/evidence riêng TASK-010 và Category import guard).12file dùng chung docs/Program/Layout được bổ sung TASK-011,0change ngoài allowlist. TASK-010.md/Test Report010/evidence010/sourceInventory và testInventory nguyên vẹn, TASK-010 hooks/menu vẫn giữ. Task005gates/Task003/004/006–009/history/guard/auth/script đang mở không sửa. Không stage/commit/push. Artifact lưu kết quả; diff check sau ghi evidence đã kiểm tiếp trước kết thúc, không build/test vì chỉ docedit.
