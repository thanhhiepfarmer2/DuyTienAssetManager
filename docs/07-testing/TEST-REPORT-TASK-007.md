# TEST-REPORT-TASK-007 — CRUD máy demo local

Ngày: 2026-10-13. Status: IN_REVIEW. Working directory: D:\Dev\QuanLyTaiSan_DuyTien.
Nguồn bằng chứng: AI chạy/quan sát trực tiếp các lệnh local; không có bước người dùng chạy/xác nhận thủ công trong lượt này. Fixture/store RAM/Google HTTP stub hoàn toàn giả; không dùng credential UAT, không gọi hoặc ghi Google thật. [Thiết kế](../02-architecture/ASSET-DEMO-DESIGN-TASK-007.md), [artifact sạch](TASK-007-LOCAL-EVIDENCE.json).

## Kết quả kiểm tra thực tế

| Kiểm tra / lệnh thực tế | Actual | Exit | Nguồn |
|---|---|---|---|
| `dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal` | PASS_LOCAL; 0 warnings, 0 errors | 0 | AI chạy/quan sát trực tiếp |
| `dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~AssetCrud\|FullyQualifiedName~GoogleAssetCatalog"` | 54 passed, 0 failed, 0 skipped | 0 | AI local fake/stub/Kestrel/Razor HTTP |
| `dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-build --no-restore --nologo -v:minimal --filter "FullyQualifiedName~CategoryHttpRazorTests\|FullyQualifiedName~ScopeAuthorizationTests"` | 9 passed, 0 failed, 0 skipped | 0 | AI hồi quy hẹp, không full TASK-005/006 |
| `dotnet format <csproj> --no-restore --include <absolute file paths TASK-007> --verbosity minimal` theo bốn project tuần tự | Application/Infrastructure.Google/Server/tests: từng command exit0. Hai file adapter/test được format lại sau guard cell (compound command exit0) | 0 ở bốn lệnh và compound quan sát | AI; danh sách include dưới đây |
| `node --check .\src\DuyTien.AssetManagement.Server\wwwroot\js\asset-form.js` | syntax pass; không chứng minh browser | 0 | AI trực tiếp |
| `git diff --check` | không có lỗi whitespace tại checkpoint trước đồng bộ report; kiểm tra final riêng bên dưới | 0 | AI trực tiếp |
| PowerShell Get-CimInstance, đếm Server.exe/dotnet Server.dll và Test-Task005RemainingLive.py | backend0, runner cũ0; không stop/start process nào trong lượt | 0 của probe riêng | AI snapshot, không suy diễn từ PID cũ |

Format: Application/Assets/*.cs; Infrastructure.Google/Adapters/GoogleAssetCatalogStore.cs; Server/Assets/*.cs và Pages/Assets/*.cs; tests/AssetCrud*.cs và GoogleAssetCatalogTests.cs. Các đường dẫn tuyệt đối dưới workspace được truyền qua subprocess argv, không format toàn repo. SDK dotnet --version thực tế10.0.400. Build/test dùng chung output được chạy tuần tự.

## Expected / actual theo AC

| Case | Expected | Actual / bằng chứng | Trạng thái |
|---|---|---|---|
| AC-05 concurrency/retry | Hai create khác key mã duy nhất; cùng key một entity, payload khác409 | Unit + Google stub + API test: mã text có zero đầu, counter bền qua reconstruction, actor keys riêng | PASS_LOCAL |
| Timeout trước/sau commit | Không success giả; reconcile hoặc retry ledger không allocate lần hai | Inject fail trước commit, mất response sau commit, reconcile read lỗi rồi reconstruction/retry: một entity/operation và code giữ nguyên | PASS_LOCAL_STUB, không Google live |
| AC-06 lifecycle | Tên/vị trí/phân loại đổi vẫn ID/mã/IssuedCCC; xóa/restore không tái cấp | Unit/API/Razor HTTP; delete bị ẩn mặc định, includeDeleted thấy lại đúng scope, restore cùng mã; alias lịch sử giữ/tìm được/không bị tái dùng | PASS_LOCAL |
| Version/tree | Editor bản cũ409, không overwrite; snapshot cây thay đổi phải reload | Unit/API và form HTTP trả409, dữ liệu bản mới giữ; không auto submit bản cũ | PASS_LOCAL |
| Required/type/rule | Input thiếu/lỗi không cấp counter; DRAFT không tự là approved | 8 case fields +4 case absent/unapproved/proposed/ambiguous rule; MACHINE-only; Org/Location mismatch, missing scope/ID, exhausted counter bị chặn | PASS_LOCAL_FIXTURE |
| Scope/Viewer | Server chặn CREATE/UPDATE/DELETE/RESTORE, detail ngoài404; lọc trước count/paging/search/choices | Viewer/scoped rows, exact/descendant, nhiều assignment không cross-combine, MOVE cùng assignment nguồn/đích, revoke token API mới | PASS_LOCAL |
| Alias/tra cứu | Text/zero đầu/Unicode không bị đổi, tên không dấu tra cứu được | Unit alias/mã cũ/name/serial, alias nhiều tài sản bị409; task này không thêm export tài sản | PASS_LOCAL |
| Google schema/journal | Không overwrite schema/dữ liệu chưa xác định, strings không formula | Stub first atomic write/read/reconstruction/sort, partial/header/range/audit/counter/alias faults; malformed/null JSON an toàn; oversized history cell không commit/cắt dữ liệu | PASS_LOCAL_STUB |
| AC-08 Razor form | Create/edit/search, validation, CSRF, lỗi/loading/empty/access denied | Real local Kestrel và compiled Razor: GET/POST, thiếu CSRF400, field errors, XSS encode, 409, delete/restore, read-only/empty/upstream503; input/key giữ khi lỗi | PASS_LOCAL_HTTP_RENDERED; chưa PASS_BROWSER |

Không có test thời gian thực hoặc giả lập timeout phiên mới của TASK-005 trong lượt này. TimeProvider của fixture chỉ cấp timestamp và assignment hiệu lực; không chứng minh Google elapsed. Không thay mật khẩu min8/session/refresh policy.

## Lỗi phát hiện và sửa

- Trường tùy chọn Razor bị implicit required nếu browser gửi rỗng: chuyển binding nullable, normalize về text rỗng, giữ ngày ghi nhận null. Test form gửi đủ serial/model/hãng/người quản lý/ghi chú/alias/ngày rỗng và API null đều đạt; không tạo dữ liệu công ty thay thế.

- Form sửa nhận validation Reason từ form xóa/restore: làm update hợp lệ trả422. Tách Reason nullable ở binding; bắt buộc lý do tại lifecycle service, clear validation form khác khi render lifecycle error. Thêm test riêng.
- Nguy cơ mất input khi Google read lỗi sau submit: giữ bound form/key và bản chưa xác nhận; HTTP failure test không thấy success/canary, không thêm write.
- Củng cố read JSON/metadata/range/null row và journal consistency; Row sort Assets không làm đổi identity/counter. Chặn cell/history lớn trước batch để không bỏ alias lịch sử.
- Test đầu tiên có expected422 thay vì binding400 của Razor và thiếu Accept:text/html trong probe access denied; sửa probe theo hợp đồng HTTP, không đổi auth. Expected của API validation vẫn422.

Lịch sử chạy thực tế: suite ban đầu40/43, failed3, skipped0, exit1; riêng Razor6/7, failed1, exit1; suite sau thêm case47/48, failed1, exit1 (validation lifecycle); sau sửa48/48 exit0; thêm malformed/cell guard53/53 exit0; sau fix optional binding/null và test form gửi đủ trường rỗng54/54 exit0. Test không bị skip/xóa/làm yếu; assertion lifecycle kiểm thông báo validation thực ở list, không nhầm chuỗi data-val-required trong markup với lỗi đang hiển thị.

## Gate chưa đạt / trạng thái

- OPEN_NOT_APPROVED: rule prefix/CCC/format và required nghiệp vụ từng loại công ty; fixture TEST/TST không là phê duyệt. Code/runtime không seed reference hoặc rule.
- NOT_VERIFIED_SOURCE: nguồn OrgUnits/Locations và mapping thực chưa nạp/xác minh trong lượt; workbook gốc/metadata/hash của TASK-006 vẫn chưa xác minh, staging pass lịch sử không thay original.
- NOT_RUN_GOOGLE: schema/CRUD atomic, owner/ACL/resource dev/drive.file boundary cho adapter mới, persistence sau restart backend thật, retry/quota/timeout trên Google thật. Không apply migration hoặc ghi dữ liệu thật.
- NOT_RUN_BROWSER: tương tác/layout/loading/form trong trình duyệt thật và web UAT AC-08. HTTP render không thay browser; không giao checklist thủ công theo chỉ đạo.

TASK-007 IN_REVIEW; phần local đã thực hiện, chưa DONE. Ngoại lệ ngày13/10 chỉ cho bắt đầu local khi TASK-006 IN_REVIEW. TASK-005 IN_REVIEW và các gate hoãn DEFERRED_BY_USER, bằng chứng idle30ph/W13 dừng giữ nguyên; TASK-006 IN_REVIEW; TASK-003 IN_REVIEW; TASK-004 DONE. TASK-008–014 không triển khai. Bước kế hoạch còn lại là giải quyết các gate trên khi được cho phép, không tự bắt đầu task tiếp theo. Không commit/push/deploy trong lượt; HEAD vẫn c643fcb, worktree chứa thay đổi TASK-007 chưa commit.

Ghi chú công cụ: CIM trong sandbox mặc định báo Access denied, probe escalated sau đó exit0/backend0/runner0; probe gộp với rg|Select-Object có exit1 nên không dùng làm chứng cứ command PASS. Một đọc file qua python trong sandbox mặc định exit1 vì PATH không nhận python; các lệnh ghi/format/build/test được chạy qua runtime đã cấp quyền. Không coi các lỗi công cụ này là test code PASS.
