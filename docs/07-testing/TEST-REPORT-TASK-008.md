# TEST-REPORT-TASK-008 — Thay đổi cây DRAFT và bulk Asset local

Ngày: 2026-10-13. Status: IN_REVIEW. Working directory: D:\Dev\QuanLyTaiSan_DuyTien.
Nguồn bằng chứng: AI chạy/quan sát trực tiếp local; không có Google live, trình duyệt thật, bước user-run hoặc xác nhận thủ công mới. Fake store/Google HTTP stub/Kestrel và compiled Razor chỉ trong tests. Không dùng credential UAT, không gọi/ghi resource Google thật. [Thiết kế](../02-architecture/TREE-MUTATION-DESIGN-TASK-008.md), [artifact sạch](TASK-008-LOCAL-EVIDENCE.json).

## Lệnh và kết quả thực tế

Các lệnh chạy tuần tự dùng chung output, theo COMMANDS:

```powershell
dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~TreeMutation"
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-build --no-restore --nologo -v:minimal --filter "FullyQualifiedName~CategoryCatalogTests|FullyQualifiedName~CategoryHttpRazorTests|FullyQualifiedName~GoogleCategoryCatalogStoreTests|FullyQualifiedName~AssetCrudTests|FullyQualifiedName~AssetCrudHttpRazorTests|FullyQualifiedName~GoogleAssetCatalogTests|FullyQualifiedName~ScopeAuthorizationTests"
node --check .\src\DuyTien.AssetManagement.Server\wwwroot\js\tree-change.js
git diff --check
```

| Check | Expected | Actual | Exit | Nguồn |
|---|---|---|---|---|
| Build server/application/Google/Razor cuối | Compile sạch | 0 warnings, 0 errors | 0 | AI chạy/quan sát trực tiếp |
| TASK-008 filter TreeMutation cuối | Success/error/boundary không fail/skip | 42 passed, 0 failed, 0 skipped | 0 | AI; unit/fake store/Google stub/HTTP rendered Razor |
| Hồi quy các lớp đã đổi | Category/Asset/scope không regression | 91 passed, 0 failed, 0 skipped | 0 | AI local; không chạy full nghiệm thu TASK-005/006 |
| node syntax | JS parse được | Không lỗi; không là browser test | 0 | AI trực tiếp |
| git diff --check trước đồng bộ report | Không whitespace error | Không output/lỗi | 0 | AI trực tiếp; kiểm tài liệu final ghi riêng sau |
| dotnet format từng project, --no-restore --include absolute paths --verbosity minimal | Chỉ file TASK-008 liên quan | Application/Infrastructure.Google/Server/tests từng command0; hai file guard/test mới và hai file HTTP/unit cuối format riêng cũng từng command0 | 0 từng lệnh quan sát | AI subprocess argv tuần tự |
| Probe Get-CimInstance chỉ đếm executable/backend workspace và Python W13 | Không backend tranh output/không runner cũ | Backend0, W13 runner0; không stop/start tiến trình | 0 | AI probe riêng |
| git rev-parse --short HEAD | Không thay checkpoint Git | c643fcb; không commit/push/deploy | 0 ở command quan sát | AI đọc trực tiếp |

Format include cụ thể: Application Categories/{CategoryCatalogService,CategoryModels,CategoryHistoryService,CategoryTreeShape,TreeMutationModels,TreeMutationService}.cs và Assets/AssetCrudService.cs; Infrastructure.Google Adapters/{GoogleCategoryCatalogStore,GoogleAssetCatalogStore,GoogleTreeMutationStore}.cs; Server Categories/{TreeMutationWeb,TreeChangePageModel,CategoryWeb}.cs và Pages/Categories/{Manage,Detail}.cshtml.cs, Pages/Assets/Move.cshtml.cs; tests/{TreeMutationTests,TreeMutationHttpRazorTests,GoogleTreeMutationTests}.cs. Hai file GoogleTreeMutationStore.cs/GoogleTreeMutationTests.cs format lại sau metadata guard. Không format toàn repo hoặc Admin/User file người dùng đang mở.

Hồi quy91 chạy sau các thay đổi import guard, reader coordination, exporter, Category/Asset adapter và Razor error retention; thay đổi cuối chỉ guard metadata joint-store/test và panel retry đã xác nhận mới, đã build và test TASK-008 lại42/42, gồm thêm scoped history và refresh lỗi sau commit. Không chạy lại91 sau đó vì không thay các lớp hồi quy; không thay auth/session/password hoặc chạy elapsed/UAT TASK-005.

## Expected / actual theo acceptance criteria

| Case | Expected | Actual / bằng chứng | Trạng thái |
|---|---|---|---|
| AC-06 edit/code/history | ID bất biến, mapping lịch sử/provenance giữ | Unit và history API/Razor: đổi tên/mã/definition, full path descendants mới; old code/path còn theo version, code node khác lịch sử không tái dùng | PASS_LOCAL |
| AC-06 move Asset | CategoryId/AssetId/ObjectCode/IssuedCCC/aliases/counter không bị tái cấp | Move category + bulk nhiều Asset, đổi category/Org/Location, version tăng khi fields đổi; no-op không write | PASS_LOCAL |
| AC-07 shape | Cả cây con; cycle/self/thiếu cha/F11 bị chặn, F10 hợp lệ | Unit preview/commit, validate generic shape và stored digest. Invalid plan không ghi | PASS_LOCAL |
| Xóa/ngừng nhánh | Rỗng mới EMPTY_ONLY, còn con/Asset kể cả deleted cần plan/confirm | RELOCATE/SOFT_DELETE giữ IDs/codes/history; DRAFT_INACTIVE không xuất thường; false confirm bị400 và không write | PASS_LOCAL |
| Scope/Viewer/revoke | Permission và đủ scope source/destination cùng assignment, count không leak | Unit/API: Viewer/cross-assignment/exact-descendant/outside/revoke bị chặn; category action có Asset cần Asset permission; history/path không lộ ancestor | PASS_LOCAL |
| Snapshot/TXT/report | Publish toàn bộ tree, không partial, report/tra cứu theo active snapshot | Joint fake store, shared reader waits writer; stored full versions and pointer cùng batch, tree/detail/search/filter/export UTF-8/indent cập nhật | PASS_LOCAL, không Google live |
| Import bypass | Không dùng import đổi ancestor/state/mất category có Asset vượt plan | Unit chặn ancestor move dù Asset CategoryId trực tiếp không đổi; giữ original snapshot | PASS_LOCAL |
| Concurrency/preview | Stale tree/asset/reference/scope hoặc hash/key khác409, không overwrite | Unit/API/Razor recheck; payload đã commit replay sau reconstructed service/adapter không ghi lần hai | PASS_LOCAL |
| Failure/atomic | Fail trước write không success; lost response đối soát toàn bộ | Google HTTP stub một batch có category pointer/Assets/operations/audit; fail trước giữ hai snapshot; lost response reconcile; reconcile fail rồi retry/reconstruction một write | PASS_LOCAL_STUB |
| Schema/integrity/bounds | Không overwrite unknown/partial/journal/counter/digest lỗi, không oversized cell | Stub malformed/null metadata/result, partial detail/digest/counter/audit faults; future Documents tab chặn; synthetic665-node complete snapshot + per-row diff trong giới hạn1800KiB/45000chars | PASS_LOCAL_STUB; fixture665 không là baseline công ty |
| Razor | Form preview/confirm/error/CSRF/scopes/data state đúng | Kestrel compiled Razor GET/POST: missing CSRF400, reason validation, cycle/no commit, stale409, bulk choices, Viewer access denied, upstream503 không canary/success giả; giữ input/key/IDs/đích | PASS_LOCAL_HTTP_RENDERED, chưa PASS_BROWSER |

Preview tác động và TXT là report thuộc TASK-008; không tạo báo cáo nghiệp vụ TASK-011. Không có test UI MAUI hoặc browser interaction/layout thật. Logic commit-success/refresh-error đã sửa và build; nhánh refresh lỗi sau commit có HTTP failure injection pass, giữ success thật và không có nút retry commit; browser/live riêng chưa chạy, không ghi PASS_BROWSER.

## Lỗi/rủi ro phát hiện và xử lý trong task

- Đường import có thể thay ancestor của category có Asset mà không qua bulk plan: chặn so sánh state và toàn ancestor ID chain tại commit, có regression test riêng.
- Reader metadata/values và đọc Category+Asset có thể xen write: các service đọc cùng single writer, fake test reader chờ và Google joint adapter một atomic batch. Không coi đây là chứng minh external multi-writer, vẫn chỉ một backend/lease.
- Node/path diff lớn nếu nhét JSON trong một cell audit có thể vượt giới hạn: dùng TreeChangeDetails mỗi node một dòng, audit link/count/result; synthetic665-node case đạt. Không cắt diff/ghi PARTIAL.
- Sau đọc Sheets lỗi, dropdown/checkbox có thể mất ID/đích dù bound model còn: thêm fallback phản chiếu chính input đã gửi (HTML encode), giữ reason/key/IDs/đích, server vẫn recheck mọi quyền; HTTP failure tests đạt.
- Không báo mutation thất bại giả khi commit đã xác nhận nhưng tải lại choices lỗi; thông báo riêng cần reload. Null/malformed JSON metadata/journal trả storage error an toàn, không lộ upstream/private canary.
- Assertion Google đầu tiên tìm tên tab trong updateCells vốn dùng sheetId: sửa kiểm sheetId của Assets/AssetOperations/SchemaVersions trong cùng batch; không làm yếu chứng cứ atomic và không sửa code để chiều test.

Lịch sử test thực tế:17/17 exit0;26 passed/2 failed/0 skipped tổng28 exit1 (hai mode cùng lỗi assertion tên tab); sau sửa và thêm HTTP34/34 exit0; thêm import guard35/35 exit0; thêm null-result và form retention37/37 exit0; thêm malformed metadata40/40 exit0; thêm scope-history và confirmed-commit/refresh-error cuối42/42 exit0. Hồi quy91/91 exit0 tại hai checkpoint liên quan. Không xóa/skip test.

Automatic approval review từ chối tối ưu bỏ một lần guard metadata vì lo ngại mất hàng rào bảng chưa hỗ trợ. Giữ nguyên guard ở read và commit, không bỏ qua từ chối; format/build/test tiếp tục được cấp quyền. Không còn phần local bị chặn bởi việc này. Một tool result trước đó bị truncate, không dùng làm bằng chứng exit0; các lần build/test cuối ghi trên là kết quả quan sát riêng.

## Gate còn mở

- NOT_RUN_GOOGLE: chưa apply hai tab mới, chưa xác minh resource dev/ACL/drive.file boundary cho adapter mới, atomic tree+Asset/pointer thực, persistence/restart/quota/timeout trên Google thật. Fake transport/reconstruction không thay evidence này.
- NOT_RUN_BROWSER: chưa chạy tương tác desktop/mobile layout/JS loading/empty/validation/preview/confirm/error trong trình duyệt thật. HTTP render và node syntax không là UAT browser; không giao thao tác thủ công mới theo chỉ đạo.
- OPEN/TBD / NOT_VERIFIED_SOURCE: rule mã/trường nghiệp vụ công ty, nguồn đơn vị/địa điểm TASK-007 và workbook gốc TASK-006 chưa chốt/đối chiếu. Không nạp dữ liệu giả thành dữ liệu công ty, không phê duyệt DRAFT.

TASK-008 IN_REVIEW: phần local hoàn tất; chưa DONE vì Google/browser/source/business gates. Ngoại lệ13/10 chỉ cho bắt đầu TASK-008 khi TASK-005/006/007 IN_REVIEW, không đóng các gate đó. TASK-003 IN_REVIEW; TASK-004 DONE; TASK-005/006/007 IN_REVIEW, DEFERRED_BY_USER và bằng chứng lịch sử giữ nguyên. TASK-009–014 TODO. Không secret UAT, provisioning/init/bootstrap, ghi Google/dữ liệu thật, deploy/commit/push hoặc triển khai TASK-009. Worktree ban đầu có33 thay đổi TASK-007, đã giữ; HEAD không đổi.

## Kiểm tra tài liệu cuối — AI chạy/quan sát trực tiếp

PowerShell UTF-8 here-string → `python -B -`, đọc 14 tài liệu liên quan, parse links/JSON/status và quét mẫu credential rõ trên changed/untracked source/doc files: exit0;81 local links tồn tại,61 files scan,0 issue; trạng thái TASK-003–009 và evidence42/91 nhất quán. Pattern scan chỉ tìm format credential rõ, không là security audit toàn diện; không in giá trị. Lần checker đầu exit1 vì assertion không nhận markdown backtick của Status TASK-006, đã sửa checker nhận cả plain/backtick; không đổi trạng thái hay code để đạt.

`git diff --check` sau đồng bộ report lần đầu exit0/không lỗi. Final diff checkpoint sau toàn bộ đồng bộ report exit0/không lỗi; kiểm lại sau phần ghi kết quả này trước kết thúc. Không build/test lại chỉ vì chỉnh tài liệu.

Probe riêng `git diff --quiet --` TASK-003/004/005, UAT/Test Report TASK-005 và TASK-009 exit0 (không đổi so HEAD); `dotnet --version` exit0, SDK10.0.400. Probe gộp quiet+HEAD trước đó chỉ có exit tổng hợp, không dùng để suy diễn exit của quiet. HEAD c643fcb đọc riêng đã quan sát ở checkpoint; không commit/push.
