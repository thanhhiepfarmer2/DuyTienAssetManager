# TEST-REPORT — TASK-010

Date: 2026-10-13. Working directory D:\Dev\QuanLyTaiSan_DuyTien. Nguồn: **AI chạy/quan sát trực tiếp** cho Git/build/test/local probes; chưa có user/browser/Google live evidence mới. Fixture GIẢ, fake Google HTTP và Kestrel compiled Razor HTTP; clock giả chỉ kiểm asOf/permission, không kiểm timeout thời gian thực. Không thay policy mật khẩu/phiên.

## Git checkpoint trước triển khai

Đọc AGENTS, git status/branch/remotes/diff và88file (25tracked modified+63untracked),728835bytes, phần local TASK-007–009/docs. Không có file binary/raw/private/build/secrets trong manifest; scan credential patterns rõ0match, không in secret, không là security audit toàn diện. Không có thay đổi ngoài phạm vi trong manifest; active Admin/User detail không sửa.

- `git ls-remote --heads origin main`: exit0, remote c643fcb333251c4fcb20724d3bbdbf4d17b0e9fb trước checkpoint.
- `git diff --check` trước stage: exit0; stage đúng manifest88file, staged diff/stat/name/hash/pattern check và `git diff --cached --check`: exit0; unstaged diff quiet exit0.
- `git commit -m "Checkpoint local TASK-007 to TASK-009"`: exit0,463896d82629847ebd41866eb3e0cb97becfc2f2,main;88files/4751insertions/85deletions.
- `git push origin main`: exit0, c643fcb..463896d main→main trên remote đã cấu hình/đã xác minh https://github.com/thanhhiepfarmer2/DuyTienAssetManager.git. `git ls-remote --heads origin main` sau push exit0, hash khớp HEAD; `git status --short` rỗng trước code TASK-010.

Không rerun build/test cho checkpoint khi không sửa code. TASK-005–009 giữ IN_REVIEW/gates, DEFERRED_BY_USER/STOPPED_BY_USER giữ nguyên. Quyền commit/push chỉ checkpoint này; thay đổi TASK-010 **chưa commit**.

## Phạm vi / expected và actual

| Case | Expected | Actual local / source |
|---|---|---|
| Mapping20cols/mã text/XLSX | Hai Mô tả5/12 giữ riêng; leading0, custom zero-mask không mất; header/formula/format khác bị chặn | Test JSON+XLSX tổng hợp GIẢ; chưa workbook gốc |
| Duplicate code | Không overwrite/double count tự động; MERGE đồng nhất20cols hoặc SPLIT identities có lý do | Test CĐKEM GIẢ, decisions/mapping/hash/reason giữ job/audit |
| Idempotent/diff | Reimport cùng dữ liệu SKIP giữ ID/version; khác field UPDATE; row không là identity | Test memory+GoogleHTTP stub, code/state/unit giữ |
| Chunk/restart/retry | Staging durable, PARTIAL không COMPLETE; totals khớp; stale/key khác bị chặn, lost response reconcile | Test205rows/failure/prefix/restart/fakeGoogle; synthetic20744rows đạt ở checkpoint58case |
| Default stock | Không Item→Asset/BOM/kho→stock, không seed0 | Test không Assets/StockBalances sau import, raw BOM999 chỉ reference |
| Snapshot | Quantity/min nullable, asOfUTC/source/actor/version/unit exact; UNKNOWN/OUT/LOW; không âm/dịch vụ/unconfirmed | Test fake clock/snapshot, không chứng nhận nguồn/ngưỡng thực |
| Scope | Item Category; stock ba chiều cùng assignment, descendants/sibling/quarantine/revoke/move không cross-combine | Test core/API/HTTP Razor; chưaGoogle/browser |
| API/Razor | Bearer/cookie restrictions/CSRF/validation/no-store/422/409/503, error/input/key retained/empty | Kestrel compiled Razor HTTP; không DOM/browser UAT |
| Registry guard | Unknown schema/tamper fail-closed; Items registry vẫn chặn tree mutation | Fake metadata/chunk; guard file không sửa, category import có Item bảo vệ cấu trúc |

## Lệnh/checkpoint quan sát thực tế

Lệnh đầy đủ tại [COMMANDS](../COMMANDS.md). Build/test tuần tự, chờ exit trước lệnh kế dùng output; không chạy toàn bộ nghiệm thu TASK-005/006/007/008/009.

1. Build server `dotnet build src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal`: exit0,1warning CS0108 upload property che PageModel.File,0errors. Đổi Upload; các build sau exit0,0warnings/errors (lần gần nhất3.25s).
2. Filter `FullyQualifiedName~Inventory` lần đầu exit1 ở compile test do escape quote. Không có tổng test thực thi.
3. Sau sửa escape:58total,53passed,5failed,0skipped,exit1. Một reader header chưa fail lúc đọc; ba theory int→decimal? không convert; một fixture đổi pointer tree sang version thiếu nodes nên fail authorization trước conflict. Sửa reader và fixture đầy đủ/typing, không bỏ/skip/giảm assertion.
4. Filter58/58passed,failed/skipped0,exit0,55s; synthetic20744 import chunk + paging được AI quan sát.
5. Bổ sung byte-budget/digest/multipart/move/guard tests; buildexit0/0warnings/errors. Filter63total,62passed,1failed,0skipped,exit1,58s: fixture guard dùng SourceSheet GIẢ thay header provenance F1-F3 Mã ngang nên bị existing validation trước guard. Sửa fixture đúng mapping, không tắt validation/guard.
6. Lần compile bổ sung assertion fixture exit1: CategoryPreview không có Errors. Đối chiếu contract Issues và sửa; không có test totals ở lần compile này.

Helper đọc python default sandbox không tìm executable (exit1); escalated Python UTF8 hoạt động. Hai write-helper dừng trước ghi do decode cp1252/guard assertion (exit1); sửa helper rồi ghi, không coi đó là test PASS. Không in secret/log body Google/token/hash auth. File/memory/HTTP streams dùng dispose; không tạo credential UAT hoặc temp file dữ liệu nguồn.

## Gate còn mở

- **NOT_VERIFIED_SOURCE_ORIGINAL**: không tìm thấy workbook XLSX/CSV VTHH trong repository. SOURCE-AUDIT baseline20744rows/20743codes/20464VTHH-276thànhphẩm-4dịchvụ/CĐKEM839-840 chỉ bằng chứng lịch sử, chưa đối chiếu gốc/header20/format/hash lần này. Fixture20744 GIẢ không là dữ liệu công ty, không ép nguồn để khớp.
- **OPEN/TBD**: nguồn stock thật/ngưỡng/đơn vị-địa điểm/rule nghiệp vụ; stockTracked phải tường minh, minnull chưa chốt. DRAFT/DEMO/quarantine không official; SourceHashDeclared không chứng nhận workbook authoritative.
- **NOT_RUN_GOOGLE**: chưa apply INVENTORY_V1/ACL/owner/drive.file/resource grant/chunk/restart/reconcile/quota/throughput100k-history/cell limits trên Google thật. Sheets adapter stub không thay evidence này. Backend bounded read/replay history vẫn có performance gate với Google.
- **NOT_RUN_BROWSER**: chưa chạy DOM/file retention/loading/layout/browser UAT. HTTP render/CSRF pass không thay browser. Theo chỉ đạo không giao checklist hoặc yêu cầu secret mới.
- Guard TASK-008 giữ nguyên: Items/StockBalances/Files/Documents/DocumentVersions registry chưa đối soát thì tree mutation chặn; không bypass để import Items. Import cây có Item cũng không đổi ancestors/state/mất node. Không đối soát toàn bộ registry vào tree mutation hoặc làm TASK-011.

## Kết quả cuối

TASK-010 **IN_REVIEW**, phần local hoàn tất; chưa DONE vì các gate thật bên trên. Ngoại lệ cho local khiTASK-005–009 IN_REVIEW đã ghi Task/Plan/INDEX/Context; trạng thái các task trước/gate hoãn/historical evidence giữ nguyên. Không real writes/Google live/secret mới/deploy/TASK-011, phầnTASK-010chưacommit.

## Kiểm chứng cuối — AI chạy/quan sát trực tiếp

- Filter Inventory65/65passed,0failed/skipped,exit0,58s sau multipart/source/fixture sửa. Review tiếp: chunk sequence không dựa materialized plan sau sort, chuyển sang immutable journal và thêm test sort. Buildexit0/0warnings/errors; filterInventory66/66passed,0failed/skipped,exit0,61s. Không tuyên bố full67suite chạy một lần.
- Filter hồi quy đúng COMMANDS CategoryCatalogTests/GoogleCategoryCatalogStoreTests/ScopeAuthorizationTests/GoogleTreeMutationTests:46/46passed,0failed/skipped,exit0,1s. Sau checkpoint này chỉ thay thứ tự plan trong Inventory, Razor mapping và format; không đổi auth/GoogleTree guard hoặc hành vi Category hồi quy.
- UI cuối: groupCategory/tracking và MERGE/SPLIT/identity/reason bằng form/dropdowns, giữ JSON staging tùy chọn; snapshotLocation dropdown. Buildserverexit0/0warnings/errors; kiểm riêng `--filter "FullyQualifiedName~InventoryHttpRazorTests"`:8/8passed,0failed/skipped,exit0,2s. Core/Google không đổi sau suite66; không chạy lại synthetic20744 chỉ vì UI.
- Format whitespace4project exit0; bổ sung paths file cụ thể Application5files/Category guard + ImportModel + InventoryHttpRazorTests exit0, chỉ format. Build cuối sau formatexit0/0warnings/errors,2.99s. Lệnh/paths rõ trong COMMANDS. Không suy diễn directory include đã format mọi file.
- `node --check .../inventory-form.js` exit0; `git diff --check` và kiểm staged rỗng/history/guard quiet exit0; `dotnet --version` exit0:10.0.400. Probe Get-CimInstance cuối exit0, khôngbackend/W13runner được match, không dừng process khác. Không tạo temp credentials/file data.
- Helper UI lần đầu exit1 tại vị trí test insert sau formatter đổi khoảng trắng; ba file UI trước đó đã ghi, kiểm state rồi hoàn thiện insertion/test, không bỏ assertion. Không là testpass/fail.

Các case local trong bảng expected/actual đã PASS_LOCAL với bằng chứng suite66 và8HTTPUIcuối; gate source/Google/browser giữ NOT_RUN/OPEN/TBD. Chỉ self-review theo AC/risk, không independent reviewer hoặc audit bảo mật toàn diện. [Artifact](TASK-010-LOCAL-EVIDENCE.json).

Kiểm cuối tài liệu/Git: checker UTF8 Python read-only logic quét39file chưa commit,90local links tồn tại, JSON/status TASK-003–011 đúng,0credential-pattern/missing-link issue; exit0. Diff check/staged-empty/history-auth-guard riêng từng subprocess exit0; HEAD463896d không đổi. Checker trước đó exit1 do yêu cầu Status không backticks trong TASK-006; sửa checker sau đọc actual `IN_REVIEW`, không sửa Task/gate/lịch sử. Artifact đã lưu kết quả quan sát; diff sau ghi evidence kiểm tiếp, không build/test cho doc edit.
