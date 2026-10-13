# TEST-REPORT-TASK-009 — Ảnh web và cặp DOCX/PDF demo local

Ngày2026-10-13. Status IN_REVIEW. Working directory D:\Dev\QuanLyTaiSan_DuyTien; SDK pin10.0.400. Nguồn bằng chứng: AI chạy/quan sát trực tiếp local; không user-run/manual confirmation mới. Synthetic PNG/JPEG/DOCX/PDF và fake store/HTTP transport chỉ trong tests, không mẫu công ty. Không Google live, browser UAT, credential UAT hoặc dữ liệu thật. [Thiết kế](../02-architecture/MEDIA-DESIGN-TASK-009.md), [artifact sạch](TASK-009-LOCAL-EVIDENCE.json).

## Lệnh/kết quả thực tế

Build/test dùng chung output chạy tuần tự theo COMMANDS:

```powershell
dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~ManagedMedia"
dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-build --no-restore --nologo -v:minimal --filter "FullyQualifiedName~TreeMutation|FullyQualifiedName~AssetCrudHttpRazorTests|FullyQualifiedName~CategoryHttpRazorTests|FullyQualifiedName~ScopeAuthorizationTests|FullyQualifiedName~GoogleAdapterHttpTests|FullyQualifiedName~SafeGoogleHttpClientLoggingTests"
node --check .\src\DuyTien.AssetManagement.Server\wwwroot\js\media-upload.js
git diff --check
```

| Check | Expected | Actual | Exit | Nguồn |
|---|---|---|---|---|
| Build cuối server/application/Google/Razor | Compile sạch | 0 warnings,0 errors | 0 | AI chạy/quan sát trực tiếp |
| Filter ManagedMedia cuối | Cases thành công/lỗi/biên/permission không fail hoặc skip | 56 passed,0 failed,0 skipped | 0 | AI local unit/format/fake Google/HTTP compiled Razor |
| Hồi quy liên quan | Guard/HTTP/CSRF/scope/executor/logging không regression | 67 passed,0 failed,0 skipped | 0 | AI local, không full nghiệm thu trước |
| JS syntax | Parse được | Không output/lỗi | 0 | AI; không browser test |
| git diff --check trước report sync | Không whitespace errors | Không output/lỗi | 0 | AI checkpoint riêng |
| Format file liên quan từng project | Chỉ file TASK-009/executor bị đổi | Application/Infrastructure.Google/Server/tests từng command0 | 0 từng lệnh | AI subprocess argv tuần tự |
| Probe backend đầu lượt | Không backend workspace tranh output | Backend0, không stop/start backend tồn tại | 0 | AI Get-CimInstance chỉ in count |

Format thực tế: `dotnet format <csproj> --no-restore --include <absolute file paths> --verbosity minimal` qua Python subprocess argv. Application/Media/*.cs; Infrastructure.Google/Adapters/GoogleMediaCatalogStore.cs và GoogleManagedMediaContentStore.cs, Http/GoogleApiRequestExecutor.cs; Server/Media/*.cs, Pages/Documents/*.cs, Pages/Files/*.cs, Pages/Assets/Photos.cshtml.cs; tests/ManagedMedia*.cs. Không format guard, toàn repo hoặc Admin/user file. GoogleAdapterHttpTests nằm trong file GoogleLiveAdapterHttpTests.cs nhưng dùng fake HttpMessageHandler, không là live Google. Tests Kestrel stop/dispose app trong test, không backend persistent.

## Expected/actual theo AC và rủi ro

| Case | Expected | Actual / bằng chứng | Trạng thái |
|---|---|---|---|
| AC-10 registry/version/counterpart | DOCX/PDF đúng cùng version, thiếu file không phát hành, old version immutable | Unit/API/Razor create metadata/version/upload từng slot/confirm PUBLISHED_DEMO; thiếu counterpart409, unknown approval JSON400; v2 nháp không ghép PDFv1, v1 vẫn tải đúng bytes | PASS_LOCAL |
| Data state/effective date | DRAFT không tự official; Viewer chỉ bản hiệu lực | Document DRAFT_DEMO, Version DRAFT/PUBLISHED_DEMO chỉ explicit confirm/pair AVAILABLE; future UTC date hidden; source/rule công ty không phê duyệt | PASS_LOCAL_FIXTURE |
| AC-10 gateway/scope | Biết opaque file ID không đủ truy cập | Current owner+permission đủ scope, draft manager-only, Viewer write403/outside404 trước Drive reads; owner deleted/unknown/assignment revoke bị chặn; Razor route parent phải match | PASS_LOCAL |
| Ảnh web/lifecycle | PNG/JPEG, description/primary, soft delete/retention | Unit/HTTP upload/edit/primary một AVAILABLE ảnh mỗi owner, delete/cancel không xóa Drive hoặc reset Asset code/ID; gateway bytes/checksum chính xác | PASS_LOCAL |
| AC-13 Drive+Sheets không cùng transaction | Durable PENDING trước create, không success giả sau lỗi | Real adapters qua fake transport kiểm PENDING/op/DriveID có trước upload; final Sheets lỗi giữ PENDING; new service retry/reconcile cùng ID một create; lost Drive/Sheet response đều đối soát | PASS_LOCAL_STUB, không Google live |
| Concurrency/idempotency/version | Cùng key một file, payload khác/stale409, không overwrite | 8 parallel samekey một file/two journal stages, distinct primary uploads atomic; document/file versions, cancelled upload không tự hồi sinh, archive retry giữ file | PASS_LOCAL |
| Type/size | Không tin extension/MIME, không file quá cap | 4 valid formats,11 faults extension/MIME/magic/CRC/truncate/path/empty/JPEG tail/PDF tail/DOCX external/macro, configured byte cap và HTTP413; ZIP/XML bounds/DTD guard | PASS_LOCAL |
| Drive metadata integrity | Không phục vụ nội dung/owner/parent/op/size không khớp | Fake cases parent/operation/MIME/content/download capability; owned folder false không pending/create; actual bounded download SHA/file bytes check | PASS_LOCAL_STUB |
| Large upload/308 | >5MiB dùng resumable đúng URI/range, không bearer tới foreign URI | Synthetic PDF>6MiB normal/lostPUT/partialRange thành một file, samekey replay; foreign Location không PUT/create;308 chỉ explicitPUT, GET/POST/default fail | PASS_LOCAL_STUB |
| Schema/atomic | Unknown/partial/header/range/audit/materialized lỗi không overwrite | Registry6tabs atomic/journal replay; five corrupt cases no POST, file/doc state/version refs and immutable old records validated | PASS_LOCAL_STUB |
| Guard metadata | Giữ cả read/commit guard TASK-008 | Không sửa GoogleTreeMutationStore; contract test thêm Documents rồi tree ReadAsync vẫn throw storage error; guard không bị rename/bypass | PASS_LOCAL_STUB; tree flow hiện fail-closed khi registry tồn tại |
| Razor/CSRF/errors | GET/POST thật, validation/empty/forbidden/413/409/503/input giữ | Kestrel compiled Razor create/version/upload/publish/photo/cookie gateway/XSS encode; missing CSRF400, no canary/success giả; stale form giữ old version/key qua retry và cancel dùng key khác | PASS_LOCAL_HTTP_RENDERED, chưa PASS_BROWSER |

Fetch upload/File-object retention, local preview/cancel, busy controls, responsive Bootstrap/aria và PDF viewer được implement; syntax/HTTP không chứng minh DOM/browser interaction/layout hoặc PDF viewer thật. Không test camera/MAUI/iOS hoặc elapsed auth trong lượt.

## Lỗi đã phát hiện/sửa và lịch sử chạy

- Compiler record Range trùng property Range: đổi ValueRange. Null guard luôn throw được annotate DoesNotReturn để compiler hiểu, không bỏ null validation. Build đầu exit1/5warnings/1error; build sau sạch0.
- Test fixture dùng property State không có của auth store và analyzer Single/Where: sửa gọi ReadAsync/Assert.Single predicate. Filter lần đầu compile exit1, chưa chạy tests; sau sửa8/8 exit0. Ngày fixture dùng TimeProvider và ZIP timestamp cố định cho bytes giả deterministic, không đổi clock/policy thật.
- Sau thêm format/Google contract37/37 exit0. API DELETE thiếu FromBody làm endpoint startup fail:37passed/7failed tổng44 exit1, sửa binding explicit rồi44/44 exit0. Không đổi auth để chiều test.
- Rà soát docs Drive: multipart dành file nhỏ; thêm resumable cho>5MiB với server Range/status và same pre-generatedID. Lỗi using declaration trong else compile exit1, sửa disposal block,48/48 exit0.
- Form dùng chung key và tự nạp current version sau409 có thể conflict cancel hoặc ghi đè retry: per-form key, đúng form lỗi giữ old version/input, sibling action key mới; upload owner version riêng với photo file version. Thêm HTTP failure/concurrency cases51/51 exit0.
- Thêm explicit308/default executor cases và format/build cuối:55/55 exit0. Hồi quy67/67 exit0; không xóa/skip/làm yếu test.

Self-review dùng .NET/backend/UI pattern, error-handling-patterns và ai-debt-detector; chỉ self-review, không independent reviewer hoặc subagent mới. Bounded file/stream/temp request cleanup, same-ID failure/retry, no raw secret/URL/Drive ID client và scope/counterpart được đối chiếu; không gọi đây là audit bảo mật toàn diện. Không dependency package mới/SQL/SQLite.

## Gate còn mở / trạng thái

- NOT_RUN_GOOGLE: chưa apply MEDIA_V1/verify folder/dev resources/owner/ACL/drive.file grant cho luồng mới, upload/download/resumable/restart/reconcile/quota thực trên Sheets/Drive. Public API docs đã đọc, không gọi resource thật.
- NOT_RUN_BROWSER: DOM/file retention/loading/preview/cancel/layout/access denied/PDF viewer/download trong browser thật. Không giao UAT thủ công theo chỉ đạo.
- NOT_VERIFIED_SOURCE/OPEN_TBD: không có mẫu DOCX/PDF nguồn trong repo (chỉ image MAUI scaffold), workbook gốc/rule công ty/Org-Location vẫn thiếu; fixture không là dữ liệu công ty. Drive target chưa nghiệm thu TASK-009. Retention thời hạn/cleanup sản xuất vẫn OPEN, không hard-delete trong task.
- Guard TASK-008 nguyên vẹn sẽ block tree mutation khi registry Files/Documents/DocumentVersions có mặt. TASK-009 upload/gateway dùng stores đọc và registry, không gọi/bypass guard. Chưa hỗ trợ đối soát impact của các module mới vào tree plan; không tự mở rộng TASK-008 hoặc TASK-010.

TASK-009 IN_REVIEW: local đầy đủ nhưng chưa DONE. Ngoại lệ13/10 cho local dù TASK-005/006/007/008 IN_REVIEW; không đổi task trước/gates hoãn/lịch sử. TASK-003 IN_REVIEW, TASK-004 DONE, TASK-010–014 TODO. Bước còn lại trong task là nguồn/Drive/live/browser gates khi được cho phép, không giao checklist hoặc yêu cầu secret mới. Worktree TASK-007/008 đầu lượt giữ; HEADc643fcb không đổi, không commit/push/deploy/real writes.

## Bổ sung kiểm chứng cuối

Self-review sửa current published pair khi hai PublishAt bằng nhau/rows sort: chọn latest effective theo durable VERSION_PUBLISH journal order, không theo thứ tự materialized table. Thêm test hai version cùng fake clock, đảo rows vẫn currentv2/exact DOCXv2. Sau sửa build0warnings/errors exit0,56/56 task exit0/failed0/skipped0. Hồi quy67 giữ checkpoint trước, không thay các lớp hồi quy sau đó.

Tool helper patch lần đầu exit1 do pattern chưa match khoảng trắng; assert dừng trước edit, sửa pattern thực rồi áp dụng; không tính đó là test pass/fail. Hai lệnh build/test cuối từng trả session, test đã được launch trước khi nhận exit build. Đã chờ cả hai hoàn tất0 rồi chạy lại build→exit0→test56/56 exit0 tuần tự; chỉ lấy lần kiểm chứng này làm bằng chứng tuần tự cuối. Không có lỗi output collision quan sát được, không suy diễn điều đó thay kiểm chứng.

## Kiểm tra tài liệu/process/Git cuối — AI chạy/quan sát trực tiếp

PowerShell UTF-8 here-string → `python -B -` read-only checker14docs:86 local links tồn tại,88 changed/untracked source/doc files quét mẫu credential rõ,0issue; JSON/status TASK-003–010 và guard read/commit/predicate nhất quán; exit0. Không in giá trị credential, pattern scan không là security audit toàn diện. `git diff --check` sau report sync exit0; `git diff --quiet --` riêng TASK-003/004/005/UAT-TestReport005/TASK-010 exit0, giữ lịch sử/task đó nguyên so HEAD. `git rev-parse --short HEAD` riêng exit0:c643fcb; `dotnet --version` riêng exit0:10.0.400. Probe riêng Get-CimInstance cuối exit0/backend0/W13 runner0, không stop process/secret mới. Final diff sau ghi evidence này kiểm lại trước kết thúc; không build/test chỉ vì doc edit.
