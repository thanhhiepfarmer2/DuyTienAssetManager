# TEST-REPORT — TASK-014 Google DEV / HTTPS

Ngày 2026-10-14 (Asia/Saigon), working directory D:\Dev\QuanLyTaiSan_DuyTien. Nguồn **AI chạy/quan sát trực tiếp**, trừ thao tác người dùng lưu credential qua prompt ẩn. HEAD đầu lượt 419662308a8395c37d45bafe7a281c1cb52748bc; worktree sạch. Không commit/push/deploy/mobile hoặc dữ liệu thật trong lượt.

## Quyền và giới hạn

Người dùng cho phép nối các module web với Google DEV, init/migration idempotent và dữ liệu hoàn toàn giả. Giữ dữ liệu/auth/PoC, scope/guard/single-writer. Android tạm dừng, iOS DEFERRED_BY_USER. TASK-014 IN_REVIEW, không tự đóng gate source/browser/restore hoặc nghiệm thu cũ đã hoãn.

## Kết quả đã quan sát

| Gate | Expected | Actual / nguồn |
|---|---|---|
| Cert | Đúng cert/backend Windows owner, trust và validation bật | `--check --trust` ban đầu exit 1; `--trust` exit 0; kiểm lại exit 0. TLS handshake/HTTPS response thực, cert phục vụ khớp CurrentUser/My, backend owner MAY03\May 03. Không -k/callback bỏ verify |
| DEV | Ba resource đúng cấu hình, owner-only, distinct, exact drive.file | PASS_GOOGLE_READ. Business legacy tên DuyTien Assets DEV, folder DuyTien Assets DEV Files, system tên provisioning hiện có. Verifier tên mặc định ban đầu dừng; sửa theo metadata legacy vừa quan sát, không đổi resource/ACL |
| Backup | Trước write, private/auth ở ngoài Git | PASS_ENCRYPTED_BACKUP: hai Sheets values/formula text+metadata và Drive manifest, DPAPI CurrentUser, decrypt round-trip true; 16 bảng trước init. Không backup Drive bytes/format đầy đủ hoặc restore thật |
| Init | Chỉ tạo schema vắng, bảo toàn rows/revision cũ, repeat no-op | PASS_GOOGLE: 23 bảng Assets/Inventory/Media/reference headers, exit 0; lần hai 0 bảng/noOp true, exit 0. Category/receipt tạo bởi explicit operations; không marker Category rỗng sai pointer |
| Legacy guard | Giữ Sheet1/PoC, không bypass unknown registry | PASS_LOCAL + GOOGLE_READ: Sheet1 phải full-range rỗng, PoC phải đúng header/row kind/key/state. Không PoC write/init/run; tab lạ/partial/corrupt vẫn chặn |
| Auth/restart | Login API/Razor hiện có, không reset tài khoản | PASS_HTTP_GOOGLE: API 200, Razor 302, session /api/me 200 sau restart. Credential UAT xóa trước checks/finally, boolean true; session resume DPAPI ngoài Git, cần xóa khi hoàn tất |
| Category/reference | Dữ liệu giả đúng staging, replay giữ ID, seed demo không duplicate | PASS_HTTP_GOOGLE: 3 nút GIẢ (2 F1/1 F2), DRAFT; replay cùng tree ID. Seed hai root GIẢ có definition marker, Org/Location/WEBDEMO rule; lần hai no-op. Không là cây công ty 665 nút/rule chính thức |
| Module integration | Asset/media/Item/snapshot/report/tree/scope thật | PASS_HTTP_GOOGLE cho fixture demo: CRUD/replay/conflict, ảnh/DOCX/PDF/gateway, Item chunk, zero/low/UNKNOWN, R01–R04/CSV, joint tree/replay và Viewer scope. Browser/cookie mới/restore/source vẫn còn gate |
| Browser | Provider khả dụng mới thực hiện UI | NOT_RUN_BROWSER: cua apps=0/browsers=0, IAB báo Browser is not available. HTTP rendered không browser pass |
| Restore | Đích DEV riêng, giữ logical IDs/roles/scope/links | PLAN_INPUT_VALIDATED_LOCAL, exit 0; không Google writes, không overwrite source. Actual restore NOT_RUN; typed cells/formulas/Drive bytes/active-session handling còn gate |

## Lệnh / local evidence

- `dotnet build ...Server.csproj --no-restore --nologo -v:minimal`: exit 0, 0 warnings/errors, 12.92 s; sau sửa fixture recognition build exit 0, 0 warnings/errors, 5.74 s.
- `dotnet test ...ArchitectureTests.csproj --filter "FullyQualifiedName~WebDevInitialization" --no-restore --nologo -v:minimal`: 11/11 passed, 0 failed/skipped, exit 0.
- Filter WebDevInitialization/TreeRegistry/WebIntegration/GoogleTreeMutationTests: **68/68 passed, 0 failed/skipped, exit 0, 17 s**, gồm test seed/no-op mới. Không full UAT task trước.
- Backup/restore-plan commands exit 0 khi đúng tên legacy DEV. [COMMANDS](../COMMANDS.md) có lệnh đầy đủ và điều kiện lease.
- Runner setup đầu exit 1 do JSON fixture sai dạng object (HTTP 400); sửa sang staging array/snake_case. Lần hai preview có issue provenance (source sheet/row), exit 1; sửa fixture theo F1-F3 Mã ngang/row>=2 và nhãn FAKE_TASK014_V1, không nới validator. Setup tiếp theo exit 0.
- Module runner đầu exit 1 do dùng nhầm /api/assets/choices (404); sửa thành /api/assets/form-choices hiện có, không thêm endpoint. Lần tiếp đang chạy. Các lần cũ giữ trong [evidence](TASK-014-WEB-GOOGLE-LIVE-EVIDENCE.json).

Fixtures hoàn toàn giả. Các stock minimum/source/unit sau này chỉ input fixture, không phê duyệt quy tắc công ty. Không có Google OAuth credential trên client; runner chỉ gọi backend qua HTTPS. Source/rule/OrgLocation/workbook thật vẫn OPEN/TBD.

## Lỗi quota phát hiện sau tree commit

Tree commit và replay thực đều 200, cùng tree ID; Asset read ngay sau đó trả 503. Safe logger chỉ trích method/status cho thấy ba GET 429 liên tiếp, không in URI/body/secret. Thêm GoogleSheetsRequestBudget vào production executor: read/write slots riêng 1250 ms, timestamp/lock operational cạnh process lease ngoài Git, cooldown 60 s khi 429. Không cache/bỏ guard/version/scope hoặc đổi auth/session policy. [Quota chính thức](https://developers.google.com/workspace/sheets/api/limits).

Build bản cuối sau format/cleanup exit 0, 0 warnings/errors, 6.39 s. Fake-clock quota+init 18/18 exit 0; selected regression cuối **78/78 passed, 0 failed/skipped, exit 0, 16 s**, gồm GoogleAdapterHttpTests thật về retry semantics. Fake clock không elapsed session UAT. Ba lệnh format scoped Google/Server/tests exit 0.

Phase finish dùng key/data cũ, không chạy lại asset/media/Item/snapshot đã đạt. Kind sai ARCHIVE (400) được sửa trong runner thành ARCHIVE_CATEGORY theo implementation; không đổi validator. Dữ liệu đã commit không xóa/reset. Retest quota/read/scope/Razor còn đang chạy; chưa suy PASS từ build.

## Kết quả cuối — AI chạy/quan sát trực tiếp

Phase finish cuối **57/57 assertions passed, 0 failed, exit 0**, `complete=true`. Các phase trước: setup 10 pass/1 fail/exit1; setup 3/1/exit1; setup 18/0/exit0; modules 1/1/exit1; modules 52/1/exit1; finish 12/1/exit1; finish 5/1/exit1. Giữ nguyên trong evidence, không cộng thành tổng test độc lập. Có 131 tên case distinct theo quan sát cuối, 130 pass và một expectation cookie cũ 302 chưa kiểm lại bằng cookie mới; không ghi 131/131 PASS.

- Google Assets: create 201/replay cùng ID, edit 200, stale 409, search/page total 1, DRAFT_DEMO; ObjectCode giữ sau relocation/restart (version 3).
- Google Drive: PNG AVAILABLE, retry cùng logical file ID, gateway trả đúng bytes; DOCX/PDF upload cùng version, PUBLISHED_DEMO và PDF bytes đúng. Sau chuyển scope, ảnh còn nguyên, Viewer gateway 404.
- Items: 3 rows GIẢ theo contract namespace MISA_VTHH/header20/row>=3, COMPLETE/retry processed=3, không thành 3 Assets. Qty 0→OUT, 2.5/min3→LOW, null/minnull→UNKNOWN; unit kg và null giữ. R01 AssetCount=1, R03/R04 mỗi MATCHES=1; UNKNOWN section=1, CSV UTF8BOM.
- Tree: ARCHIVE_CATEGORY/RELOCATE preview có ITEM/STOCK/DOCUMENT/PHOTO/DOCX/PDF; một joint commit 200, ItemCount=3/DocumentCount=1 và replay cùng tree ID. Owner IDs/code/state/stock/file bytes/published version giữ đúng; after-read 503 do429 đã retest 200 sau pacing, không tắt guard hoặc viết lại dữ liệu để sửa.
- Viewer mới có nhãn GIẢ, scope Org/Location/source root: Asset/Item/Document lists=0 sau chuyển sang target; report AssetCount=0; tree administration403, file gateway404. Logout chỉ phiên fixture và phiên API của runner; không logout-all user cũ.
- Bảy GET Razor Admin/Users, Categories, Assets, Documents, Items, Stock, Reports đều HTML200 bằng bearer principal hợp lệ, qua authorization server. Login cookie ban đầu và GET Assets đã đạt. Cookie cũ về sau trả302; chưa kiểm cookie mới, không chẩn đoán chắc nguyên nhân hoặc xem bearer rendered là cookie/browser UAT. Không đổi timeout hoặc yêu cầu secret lại.
- Server segment cuối có 124 Google operations trong safe log, 0 non-2xx; chỉ đếm method/status, không log body/URI/secret. Không suy thành benchmark/quota/throughput đã đạt toàn diện.

## Backup, bảo toàn và runtime cuối

Backup sau nghiệm thu khi backend đã dừng: business35/system14=49 bảng, values/formula text+metadata và Drive manifest; DPAPI CurrentUser ngoài Git, decrypt round-trip true, command exit0. Restore-plan kiểm49bảng/auth/roles/scopes, exit0, GoogleWrites0; **actual restore NOT_RUN**, chưa backup Drive bytes/format đầy đủ hoặc giải quyết typed formula/session restoration. Hai backup nằm dưới `%LOCALAPPDATA%/DuyTienAssets/Task014Backup`, không ở Git.

Đối chiếu hai backup trong RAM: PoC rows unchanged=true; existing user password/state/auth fields và roles/assignments/scopes preserved=true; Users12→13 chỉ thêm Viewer GIẢ. Comparator exit0, không in values/hash/token. Không reset spreadsheet, đổi resource IDs cấu hình, provisioning trùng hoặc chạy lại PoC.

Sau backup đã start lại một backend: launcher9236/backend13420; bốn listener IPv4/IPv6 7008/5211 cùng backend. Strict HTTPS `/`, `/Help`, `/api/system/status` đều200; HTTP307→HTTPS. Config-only status WEB_DEMO_LOCAL, SingleWriterMode=true, Sheets/Drive configurationPresent=true; không coi endpoint config-only là Google readiness. Launcher command exit0; server còn chạy, chưa có server exitcode.

Probe độc lập: UatTask005 password key absent=true và session DPAPI checkpoint absent=true. Không có password/token/hash trong report/evidence. Browser provider vẫn không khả dụng. Không commit/push/deploy/mobile.

TASK-014 **IN_REVIEW**. HTTPS và tích hợp Google DEV của fixture đã có evidence; browser/DOM/layout/download/cookie mới, company workbook/rules/OrgLocation/ngưỡng, backup bytes/actual restore/performance/hosting vẫn chưa đạt. Các gate manual/timeout task trước giữ DEFERRED_BY_USER/STOPPED_BY_USER, không tự chạy lại. Android tạm dừng, iOS DEFERRED_BY_USER.

## Đối chiếu cuối

AI checker26paths modified/untracked,103local links: 0 UTF8/JSON/Python AST/link/status/credential-pattern/artifact issue, exit0. Ba PowerShell scripts parse0errors, exit0. `git diff --check`, index-empty, quiet auth/mobile và historical reports TASK-005/010/011/012 từngsubprocess exit0; HEAD4196623 giữ. Chỉ source/tests/docs thuộc phaWeb mới chưa commit. Pattern scan không là audit bảo mật toàn diện; self-review giữ resource/guard/scope/cancellation/cleanup, không independent reviewer/subagents. Sau ghi doc/evidence không build/test lại vì không codechange.
