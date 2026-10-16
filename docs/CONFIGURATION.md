## Login Google DEV — lưu ý 15/10/2026

Launcher hiện hành dùng Google/Development và đặt cả DOTNET_ENVIRONMENT để không bị môi trường Production kế thừa làm mất user-secrets. Technical status cho biết buildId/environment/storage, không public resource IDs. `invalid_grant` là Google từ chối refresh token, không là sai password admin/mất Internet; chỉ consent lại cùng Desktop app/drive.file sau diagnostic chứng minh. Dùng `scripts/Reconnect-GoogleLogin.ps1` từ PowerShell desktop khi cửa sổ do agent mở không xuất hiện. Script không init/bootstrap/reset hoặc đổi resource. Xem COMMANDS và Test Report v2; success của consent vẫn cần auth-read rồi login form hiện có.

> Bổ sung v2 — scope/coherence: observed adapters chỉ lấy checkpoint từ state trung tâm đã đọc, không thêm RPC hoặc thay guard. Cache lưu dependency nội bộ, purge query/bytes khi checkpoint đã quan sát đổi, không cho revision cũ quay lại. Sang connection epoch mới phải bỏ corpus cũ trước refresh; mixed-version/cross-epoch read409, không publish/cache dưới nhãn current. Không public global revision counters. Ticket bind credential digest; ciphertext bytes và RAM index bind phiên+snapshot. Cache prototype chưa có binding cần online xác minh lại, không dùng để cấp quyền offline; không reset admin hoặc đụng workspace LocalOffline v1.

> Tác vụ HTTP/CLI có operation lease: sau vòng mất/kết nối lại, RPC cũ dừng trước OAuth/transport (kiểm lại sau pacing), giữ UNKNOWN/key nếu business đã bắt đầu; request mới cần revalidate+fresh data/reconcile. Người có proof/cookie còn hạn mở launcher offline được chuyển từ Login tới /Offline, không tạo tài khoản hoặc xác thực password local.

> Hardening v2: probe hosted background, known-offline snapshot reads không chờ network timeout. Request và write-ready bind connection epoch; timestamp proof lấy trước validation, SecurityVersion cũ không ghi đè mới. Memo Sheets properties chỉ trong request GET/HEAD, invalidate sau ghi và không cache values/file/auth xuyên request. Intent bền sau auth, trước business handler; auth chưa gửi nghiệp vụ không tự thành UNKNOWN.

# Cấu hình hiện hành — REQ-002 v2 (2026-10-15)

Runtime mặc định `Storage:Mode=Google`, `ConnectedCache:Enabled=true` là guard bắt buộc; false hoặc LocalOffline ở Program sẽ fail closed, không fallback. Tài khoản admin và dữ liệu trung tâm dùng lại cấu hình Google DEV hiện có; không init/bootstrap/reset hoặc copy auth của workspace v1. OAuth Desktop/drive.file không đổi; không có Google OAuth token ở client/cache. Chính sách password min8/session giữ.

`ConnectedCache:Path` mặc định `%LOCALAPPDATA%\DuyTienAssets\ConnectedCache`, absolute local/outside Git, không UNC/reparse; root private Windows owner+SYSTEM. Subdir central identity hash từ endpoint và resource IDs; proof/token hash/response bytes dùng DataProtection, key DPAPI CurrentUser tại keys/. Cookie dt_session HttpOnly/Secure/SameSite vẫn của trung tâm; dt_local_* cũ không được đọc. Không lưu password/hash đăng nhập/token OAuth; các khóa/idempotency intent kỹ thuật mã hóa ngoài Git.

Cache tối đa200query/64MiB mỗi phiên; bytes mỗi response26MiB, RAM bytes/index giới hạn128MiB. Ticket hết hạn theo min idle/absolute/grant end đã xác minh, không touch/extend offline. Mỗi snapshot có asOf riêng; không cache toàn store hoặc phê duyệt nguồn. Logout tombstone và UNKNOWN intent theo user vẫn bền qua restart; central user khác không dùng được proof/query của user trước. Quyền thay đổi xóa views/bytes được tham chiếu cũ. Cache corrupt/thiếu không thành dữ liệu rỗng/0 hoặc quyền ghi.

Probe HTTPS HEAD Microsoft/Google, fallback public Sheets HEAD không OAuth (không OAuth/cookie), timeout3s/host, coalesce; connected15s, disconnected/unknown backoff2–60s. HTTP401/403/429/503 của probe chứng minh transport, không chứng minh phiên/trung tâm. Tất cả probe không trả HTTP và có ít nhất hai lỗi network-unreachable, hoặc ba timeout độc lập (gồm public Sheets) hoặc tất cả probe thất bại kèm mất link mới cho offline; DNS/TLS/timeout đơn lẻ hoặc bằng chứng không đủ là SERVICE_UNAVAILABLE. navigator.onLine không quyết định quyền. Khi offline chỉ các probe nhỏ này được outbound; business/CLI qua Google executor bị chặn trước OAuth/pacing/transport. Google budget1250ms/cooldown60s giữ nguyên.

Workspace `%LOCALAPPDATA%\DuyTienAssets\LocalOffline\default` v1 bảo toàn, không nhập vào trung tâm. Launcher cũ CMD chuyển sang DuyTien-Web.cmd; script Start-LocalOffline.ps1 cũ bị chặn rõ ràng. Các cấu hình/commands LocalOffline phía dưới là lịch sử v1, không luồng hiện hành.

# Cấu hình môi trường

Ứng dụng dùng cấu hình phân cấp của ASP.NET Core. File trong repo chỉ chứa giá trị
không nhạy cảm. Secret và token phải được cấp từ user-secrets trong môi trường dev
hoặc secret store/biến môi trường của host; không đưa vào appsettings, tài liệu,
log, APK hay IPA.

## Google Workspace

Backend dùng OAuth 2.0 Desktop app của một tài khoản vận hành công ty để truy cập
Google Sheets và thư mục trong My Drive. Đây là danh tính tích hợp backend, tách
biệt hoàn toàn với tài khoản đăng nhập ứng dụng của admin/người dùng.

| Khóa cấu hình | Biến môi trường tương đương | Loại | Trạng thái |
|---|---|---|---|
| `GoogleWorkspace:OAuthClientId` | `GoogleWorkspace__OAuthClientId` | Secret/config nhạy cảm | Có user-secrets; không in giá trị |
| `GoogleWorkspace:OAuthClientSecret` | `GoogleWorkspace__OAuthClientSecret` | Secret | Có user-secrets; không in giá trị |
| `GoogleWorkspace:OAuthRefreshToken` | `GoogleWorkspace__OAuthRefreshToken` | Secret/token | Có user-secrets; refresh trả đúng drive.file ngày 2026-10-09 |
| `GoogleWorkspace:SpreadsheetId` | `GoogleWorkspace__SpreadsheetId` | Resource ID | Có user-secrets; bằng chứng TASK-004 giữ nguyên |
| `GoogleWorkspace:SystemSpreadsheetId` | `GoogleWorkspace__SystemSpreadsheetId` | Resource ID spreadsheet auth riêng | Đã provisioning DEV riêng/init schema 1 ngày 2026-10-09; ACL chỉ owner; bootstrap/no-op đã đạt |
| `GoogleWorkspace:DriveFolderId` | `GoogleWorkspace__DriveFolderId` | Resource ID My Drive | Có user-secrets; không kiểm lại PoC TASK-004 |
| `GoogleWorkspace:AccessDeniedProbeFileId` | `GoogleWorkspace__AccessDeniedProbeFileId` | Resource ID control chỉ dùng live test | Có user-secrets; không chạy lại TASK-004 |

Không nhập giá trị trực tiếp vào command history. Dùng script prompt ẩn; giá trị
để trống giữ nguyên cấu hình hiện có:

```powershell
.\scripts\Set-GoogleWorkspaceSecrets.ps1
```

Bootstrap/recovery admin dùng script prompt ẩn riêng, không yêu cầu gửi mật khẩu
trong chat hoặc đặt mật khẩu vào command line:

```powershell
.\scripts\Set-AuthOperatorSecret.ps1 -Purpose Bootstrap
.\scripts\Set-AuthOperatorSecret.ps1 -Purpose Recovery
```

OAuth bootstrap dùng PKCE và loopback chính xác
`http://127.0.0.1:53682/authorize/`, sau đó tự lưu refresh token vào user-secrets
mà không in token. Code chỉ request đúng `drive.file` cho cả Sheets/Drive, không
bật incremental grant và từ chối token response thiếu/thừa scope. Resource mới
được tạo bằng lệnh provisioning chủ động; resource có sẵn phải được Google Picker
trả về. `SpreadsheetId`/`DriveFolderId` chỉ là địa chỉ, tự nhập ID không phải bằng
chứng đã cấp quyền; chọn folder không cấp quyền file con có sẵn. Chi tiết Data
Access, revoke grant/token rộng, Picker/provisioning và live evidence:
[GOOGLE-DEV-SETUP](GOOGLE-DEV-SETUP.md).

TASK-004 local đã thay readiness-only bằng REST implementation. Người dùng đã
cung cấp kết quả chạy live trên Google thật; fake/stub và
`LiveConnectionVerified=false` vẫn không tự là bằng chứng live. Credential và
resource ID tiếp tục ở ngoài repo. Lượt review không tạo project/client/resource,
không đổi sharing và không chạy lại PoC có ghi.

## Auth ứng dụng cho TASK-005 — `ACCEPTED`

Các tên khóa dưới đây đã được duyệt; không chứa giá trị thật:

| Khóa cấu hình | Biến môi trường tương đương | Loại | Giá trị đề xuất |
|---|---|---|---|
| `Auth:Bootstrap:AdminPassword` | `Auth__Bootstrap__AdminPassword` | Secret dùng một lần | Không có default; xóa/rotate sau bootstrap |
| `Auth:Recovery:AdminPassword` | `Auth__Recovery__AdminPassword` | Secret phục hồi dùng một lần | Chỉ cấp trong maintenance mode rồi xóa/rotate |
| `Auth:Password:MinimumLength` | `Auth__Password__MinimumLength` | Cấu hình cố định demo | 8; không có max/complexity/blocklist/username rule |
| `Auth:Lockout:MaxFailedAttempts` | `Auth__Lockout__MaxFailedAttempts` | Cấu hình | 5 |
| `Auth:Lockout:DurationMinutes` | `Auth__Lockout__DurationMinutes` | Cấu hình | 15 |
| `Auth:RateLimit:LoginAttempts` | `Auth__RateLimit__LoginAttempts` | Cấu hình | 10 |
| `Auth:RateLimit:WindowMinutes` | `Auth__RateLimit__WindowMinutes` | Cấu hình | 15 |
| `Auth:Sessions:WebIdleMinutes` | `Auth__Sessions__WebIdleMinutes` | Cấu hình | 30 |
| `Auth:Sessions:WebAbsoluteHours` | `Auth__Sessions__WebAbsoluteHours` | Cấu hình | 8 |
| `Auth:Sessions:RememberIdleHours` | `Auth__Sessions__RememberIdleHours` | Cấu hình | 24 |
| `Auth:Sessions:RememberAbsoluteDays` | `Auth__Sessions__RememberAbsoluteDays` | Cấu hình | 7 |
| `Auth:Sessions:MobileAccessMinutes` | `Auth__Sessions__MobileAccessMinutes` | Cấu hình | 15 |
| `Auth:Sessions:MobileRefreshIdleDays` | `Auth__Sessions__MobileRefreshIdleDays` | Cấu hình | 14 |
| `Auth:Sessions:MobileRefreshAbsoluteDays` | `Auth__Sessions__MobileRefreshAbsoluteDays` | Cấu hình | 30 |
| `Auth:Sessions:MaxActivePerUser` | `Auth__Sessions__MaxActivePerUser` | Cấu hình | 5 |
| `Auth:SystemWriterLockFilePath` | `Auth__SystemWriterLockFilePath` | Đường dẫn local trên host | Mặc định dưới thư mục binary đang chạy; mọi process dùng cùng system sheet phải dùng đúng cùng đường dẫn tuyệt đối |
| `Auth:MaintenanceMode` | `Auth__MaintenanceMode` | Cờ vận hành | `false`; recovery chỉ chạy khi `true` |

- Username bootstrap cố định là `admin`, không cần secret/config riêng.
- Không đưa mật khẩu bootstrap/recovery vào `appsettings*.json`, tài liệu, command
  history, log, client, Sheet plaintext hay build artifact.
- Lệnh `auth-bootstrap-admin`/`auth-recover-admin` đọc secret qua
  ASP.NET Core configuration. User-secrets dev có thể được lệnh xóa sau thành
  công; env/secret manager phải do người vận hành xóa hoặc rotate.
- `SystemSpreadsheetId` được lưu ngoài client và chỉ có sau khi người vận hành
  chủ động provisioning hoặc Picker. Lượt thiết kế 2026-10-07 không tạo/chọn;
  lượt 2026-10-09 đã provisioning DEV riêng, xác minh ACL/init/bootstrap/no-op; khóa bootstrap đã xóa.
- Chỉ yêu cầu mật khẩu có tối thiểu 8 ký tự. Không áp dụng bất kỳ điều kiện nội
  dung/độ mạnh nào khác cho create/bootstrap/reset/change.
- Chi tiết chính sách tại
  [AUTH-DESIGN-PROPOSED](02-architecture/AUTH-DESIGN-PROPOSED.md).

## Host và HTTPS

- Local dùng launch profile HTTPS của ASP.NET Core. Checkpoint 2026-10-14 đã trust
  development cert của tài khoản Windows vận hành; HTTPS validation bật và cert
  backend phục vụ đã được đối chiếu với CurrentUser/My. Bằng chứng cũ chưa trust
  bên dưới là lịch sử; không suy thành trust cho máy/client khác.
- Môi trường sử dụng phải công bố một HTTPS endpoint dùng được từ mạng công ty và
  Internet. Nhà cung cấp host, domain, TLS/reverse proxy và `ASPNETCORE_URLS`
  chưa được xác lập.
- Demo chạy đúng một backend instance. Web giữ process lease bằng file lock hệ
  điều hành; init/bootstrap/recovery lấy cùng lease và fail nếu web/CLI khác đang
  giữ. Lock RAM chỉ tuần tự hóa trong process; không được scale-out trước khi có
  coordinator dùng chung và ADR bổ sung.
- Mật khẩu bootstrap/recovery admin sau này phải cấp ngoài repo theo proposal
  TASK-005. TASK-003 chưa triển khai đăng nhập và không tạo khóa/mật khẩu mẫu.

## Mobile

Mobile gọi API chung qua HTTPS; không chứa Google credential. Base URL, signing
Android/iOS và cấu hình phân phối sẽ được thêm khi hosting/domain và thiết bị mục
tiêu được xác lập. Build target hiện tại chỉ gồm Android và iOS.

## Checkpoint TASK-005 ngày 2026-10-09

Kiểm tra cấu hình chỉ xuất tên khóa và boolean có/không. OAuth client/secret/
refresh và resource TASK-004 có trong user-secrets, không có override environment
cho các khóa đã kiểm tra. `SystemSpreadsheetId` được flow provisioning lưu ngoài
repo; Google xác minh DEV System thuộc tài khoản OAuth hiện tại, không trashed,
canEdit, chỉ một permission owner/user, shared=false và khác CSDL nghiệp vụ.
Scope token refresh đúng duy nhất drive.file; không chạy consent hoặc đổi client.
Đây không phải kiểm tra Google Console về loại client mới; giữ setup Desktop
đã xác lập và implementation PKCE/loopback hiện có.

Bootstrap đã chạy hai lần exit 0/no-op và khóa bị xóa; recovery không chạy. `Auth:SystemWriterLockFilePath` dùng default
trong binary của cùng server project; web/CLI lấy cùng lease. Người dùng nói rõ
vẫn làm trên máy đã setup, chưa chuyển máy. AI không thấy server DuyTien hoặc
listener dự án trước CLI; các tiến trình dotnet quan sát là build infrastructure.
Web đã dừng sau bản sửa UI, process/port 0. Người dùng đã đổi mật khẩu/login lại;
credential UAT tạm đã xóa sau runner fail checkpoint công cụ. Không
bootstrap lại để đặt mật khẩu mới; xem checklist TASK-005. Không ghi giá trị secret/resource
ID vào report.

### Credential UAT tạm

`UatTask005:AdminPassword` chỉ phục vụ runner dev, không là policy hoặc backdoor
ứng dụng. Prompt `Set-Task005UatSecret.ps1` lưu ngoài repo; không in giá trị.
Runner `Test-Task005MobileLive.py --operator-secret --restart-checkpoint` đọc RAM,
xóa đúng khóa ở finally (kể cả fail) bằng atomic replace và giữ các khóa khác.
Nếu process bị kill trước finally, người vận hành xóa khóa ngoài repo. Người dùng
đã sửa File.Replace backup null bằng NullString.Value và xác nhận lưu thành công;
AI sửa pattern tương tự trong bootstrap helper, file giả probe pass. Không giữ
password/hash/token trong tài liệu hoặc output nghiệm thu.

Hiện tại credential UAT tạm không có (AI chỉ kiểm boolean False). Người dùng cần nhập lại prompt ẩn cho lần runner tiếp theo; không tái sử dụng khóa bootstrap đã xóa.

Checkpoint sửa quota: credential runner lần tiếp theo cũng đã xóa (AI booleanFalse), web dừng. Không đổi scope/Desktop/client/resources/password policy/timeouts; giảm API round-trips và no-op writes. Lỗi Google503 giữ cookie/session, phải retry sau hồi phục.


## Kết quả live sau sửa quota — 2026-10-09

Build sạch/78 tests local exit0; HTTP preflight12/12 exit0; runner
`python -u -B .\scripts\Test-Task005MobileLive.py --operator-secret` exit0,
173/173 assertion. Viewer/revoke/last-admin/mobile rotation-reuse/logout/audit/
max5/access15 phút thật/lockout15 phút thật/rate-limit đạt. Cookie/anti-forgery và
Razor scopes đã PASS_HTTP_GOOGLE, không là PASS_BROWSER. Cleanup khóa UAT/
bootstrap đều booleanFalse; read probe Google exit0 vẫn đúng DEV/owner-only/
drive.file/schema1/13tab/5role, Users4/AdminExistsTrue. Một backend đang chạy
HTTPS7008 để browser. G1 trước fix FAIL_MANUAL, retest đang chờ; G2/tương tác/
layout, W10 và W13 elapsed dài chưa đầy đủ. TASK-005 IN_REVIEW; không lặp setup.
TASK-003 IN_REVIEW, TASK-004 DONE, Task sau chỉ bắt đầu sau gate TASK-005.

## Checkpoint UAT hiện tại — 2026-10-10

G1a/G1b PASS do người dùng xác nhận thủ công. W10 PASS_HTTP_GOOGLE: fake backup
Active/Admin cùng assignment ba ALL hiệu lực, login API/Razor HTTP và phục hồi
primary đã quan sát. Baseline78/live173 không chạy lại. Thêm14 test boundary
TimeProvider PASS_LOCAL exit0, không đổi timeout; không thay bằng elapsed live.

Runner W10/W13 riêng PID18964:65 assertion quan sát PASS, chưa exit; credential
UAT xóa ngay sau setup và probe độc lập booleanFalse (bootstrap cũngFalse).
Bốn W13 thời gian thật PENDING đến10/10 14:44/22:15,11/10 14:15 và17/10 14:16
giờVN; cookie chỉ RAM, không rút ngắn policy. G2 browser còn chờ, W13 chưa PASS.
Xem UAT-CHECKLIST-TASK-005 và TASK-005-REMAINING-LIVE-EVIDENCE.json. Giữ TASK-005
IN_REVIEW, TASK-003 IN_REVIEW, TASK-004 DONE; không deploy/commit/push/TASK-006.

Checkpoint bổ sung2026-10-10: W13 defaultidle30ph PASS_REAL_ELAPSED HTTP302 login
lúc14:44:57 giờVN; 8h/24h/7ngày vẫn pending. G2a bốn bước và G2b PASS_MANUAL;
G2c chưa chạy vì mất password tạm, hướng dẫn reset đúng user giả qua browser.
Chưa xác nhận reset thành công; TASK-005 vẫn IN_REVIEW.

## Checkpoint hiện hành 2026-10-10

TASK-005 UAT còn lại dừng theo người dùng: runnerW13 PID18964 không còn; credential UatTask005:AdminPassword và bootstrap absent đã kiểm chỉ xuấtboolean, không yêu cầu nhập lại. Các đoạn pending/17-10 phía trên là lịch sử trước quyết định dừng. TASK-006 dùng GoogleWorkspace:SpreadsheetId cho business, khác SystemSpreadsheetId; OAuth Desktop/drive.file/lease giữ nguyên. Không tạo config secret mới, không auto init/import ở startup. Chưa ghi schema/data TASK-006 vào Google thật; testfake không phải cấu hình chạy ứng dụng. Backend workspace đã dừng để build.

## TASK-007 local

Không thêm credential hoặc scope OAuth; dùng GoogleWorkspace business SpreadsheetId khác SystemSpreadsheetId, drive.file/executor và process lease hiện có. Không startup init/seed. Rule/OrgUnits/Locations phải có header/nguồn hợp lệ trong business Sheets; thiếu thì create bị chặn. Code rule DEMO/ApprovedForDemo và required fields công ty vẫn chưa phê duyệt, fixture trong tests không là config production. Không bật SQL/SQLite/in-memory adapter ở runtime. Không yêu cầu credential UAT.

Sáu tab AssetSchemaVersions/Assets/IdCounters/LegacyAliases/AssetOperations/AssetAuditEvents chỉ tạo ở explicit CRUD write, chưa apply/verify trên Google. Headers actual theo SHEETS-SCHEMA và GoogleAssetCatalogStore; dữ liệu cũ/header khác dừng để migration có phạm vi riêng. Không đổi password min8 hay policy phiên.

## TASK-008 local — 2026-10-13

Không thêm secret/config/scope; dùng business SpreadsheetId khác SystemSpreadsheetId, OAuth Desktop/drive.file/process lease/shared writer hiện có. Hai tab TreeChanges/TreeChangeDetails chỉ tạo ở explicit mutation đã preview/confirm, chưa apply trên Google trong lượt. Không init/bootstrap/provision lại. CodeRules/OrgUnits/Locations chỉ đọc, không tự seed hoặc phê duyệt; source/rule công ty OPEN/TBD. Guard từ chối tab module chưa hỗ trợ và schema/journal không xác định. Không dùng credential UAT, đổi policy phiên/password hoặc thêm fake persistence production.

## TASK-009 local — 2026-10-13

Dùng business GoogleWorkspace:SpreadsheetId khác SystemSpreadsheetId và DriveFolderId My Drive operator/app qua provisioning/Picker hiện có; chưa kiểm target thật trong lượt. Không thêm credential/OAuth scope, không init/bootstrap/provision hoặc seed startup. Runtime DI GoogleMediaCatalogStore/GoogleManagedMediaContentStore; fake chỉ trong tests.

MediaUpload:MaxPhotoBytes mặc định10485760 (10MiB), MaxDocumentBytes26214400 (25MiB), configurable1–25MiB kỹ thuật subset; chưa là policy vận hành được phê duyệt. Request cap26MiB gồm multipart; labels UI theo config. Bytes/type validate server. File>5MiB resumable + explicit PUT308/status/range, nhỏ multipart; session URI chỉ request memory và endpoint authority validation, không secret/client/log/config repo. Retry/reconcile giữ pre-generated Drive ID qua durable registry, không ghi đè file.

Registry6tab MEDIA_V1 chỉ tạo trong explicit write, không Google apply trong lượt. Guard metadata TASK-008 nguyên vẹn: registry mới làm tree mutation fail-closed, cần tác vụ đối soát module riêng trước khi mở lại. Không rename tab/đổi spreadsheet để tránh guard. Mẫu nguồn/retention-cleanup/rule/workbook/Org-Location còn OPEN/TBD/chưa xác minh; không hard-delete Drive hoặc đổi password/session policy.

## Android TASK-012 local — backend và lưu phiên

URL môi trường nhập tại màn hình login MAUI, chỉ HTTPS (không userinfo/query/fragment), lưu Preferences key duytien.backend.url. Không URL công ty mặc định/Google credential/client secret/SQL/offline store. URL là cấu hình không bí mật; endpoint/TLS phải được thiết bị tin cậy, host/domain vẫn TBD. Không bypass certificate hoặc bật cleartext. App dùng bearer headers, không tự theo redirect hoặc cookie. Token pair và refresh-in-flight marker lưu một SecureStorage entry duytien.mobile.session.v1 ràng buộc URL; không lưu password/role/scope authority. Backup Android tắt, logout/revoke xóa entry qua adapter; OS storage/delete/restore chưa kiểm thực. Thay backend yêu cầu logout trước, data/form/photo RAM không chuyển qua tài khoản/môi trường. [Thiết kế](02-architecture/MOBILE-ANDROID-DESIGN-TASK-012.md).

## TASK-014 Google DEV / quota — 2026-10-14

Ba resource hiện có đã được xác minh đúng tên legacy DEV, owner-only, distinct, token exact drive.file; không tạo resource trùng/đổi ACL. Init mới chỉ explicit CLI và cùng lease; Category/receipt qua operations đã kiểm quyền. Backup DPAPI ngoài Git, Drive mới có manifest, chưa restore.

Production GoogleApiRequestExecutor dùng GoogleSheetsRequestBudget tại `Auth:SystemWriterLockFilePath + .sheets-budget.json`, chỉ timestamp operational, không business/auth rows hoặc secret. Read/write pacing riêng 1250 ms và cooldown42960s, giữ qua restart. Không chỉnh file khi backend đang chạy. Cursor lỗi fail-closed; kiểm permission/file integrity, không sửa Sheets/guard để tránh429. Độ trễ có thể tăng do full registry reads; chưa là bằng chứng throughput/performance công ty.

[Report hiện hành](07-testing/TEST-REPORT-TASK-014-GOOGLE-DEV.md). Mật khẩu UAT chỉ prompt/ngoài repo, runner xóa khóa ngay khi đọc/finally. Session resume DPAPI ngoài Git, xóa sau hoàn tất; không đặt credential vào args/log.

## LocalOffline được duyệt — TASK-015 / ADR-002

`Storage:Mode` chỉ nhận `Google` hoặc `LocalOffline` (case-sensitive). appsettings giữ Google cho tương thích; launcher đặt `Storage__Mode=LocalOffline` và `Storage__WorkspacePath` rõ ràng. Không tự fallback khi lỗi mạng, không sync hai chiều. User-secrets `secrets.json` không nạp trong LocalOffline; không resolve Google providers/clients. DI HttpClient mặc định từ chối outbound; không là chứng cứ packet capture browser/OS.

Workspace mặc định `%LOCALAPPDATA%\DuyTienAssets\LocalOffline\default`, local NTFS ngoài Git, không UNC/reparse point. Chủ tài khoản Windows và SYSTEM có ACL; không phục vụ workspace dưới wwwroot. `manifest.json` schema1/WorkspaceId/generation/checksums trỏ business descriptor và auth; descriptor trỏ bốn module JSON bất biến trong `generations`. Dữ liệu đã materialize+journal được validate lúc startup và commit, không replay mỗi GET. RAM read pinning không chờ unrelated writer. Chỉ module thay đổi được ghi mới; manifest File.Replace sau Flush(true), RAM publish sau commit. Old generations được giữ, không tự reset/compact khi corrupt.

Auth dùng framework hash/policy TASK-005, cookie `dt_local_<WorkspaceId>` tách `dt_session` Google và workspace khác. DataProtection keys trong `keys`, DPAPI CurrentUser trên Windows; sessions/auth chỉ của workspace mới. Không copy password hash/session/token Google. App không auto tạo/reset admin. Khởi tạo trên `/Account/LocalSetup` chỉ loopback và khi chưa có user; mật khẩu tối thiểu8, lần đầu phải đổi theo policy. Sau đó Admin→Demo GIẢ local là thao tác chủ động để tạo3nút DRAFT + Org/Loc/rule LOCAL GIẢ; không dataset công ty hoặc rule chính thức. Lặp lại giữ dữ liệu; cây/reference khác bị chặn.

File bytes ở `blobs/LOCAL-<uuid>`, size/SHA256 kiểm trước upload và mỗi gateway download; AVAILABLE thiếu/corrupt bytes làm startup fail closed. Không dựa Drive manifest để giả bytes. Metadata wire giữ contract TASK-009; physical local ID không thành quyền truy cập trực tiếp. Form lỗi đĩa503 giữ payload/key/version; uncertain commit retry cùng key, không báo lưu khi chưa bền vững.

Diagnostics tùy chọn `Diagnostics:Enabled=true`: Server-Timing chứa các phase đã hoàn tất trước headers. `Diagnostics:OutputFile` absolute dưới `%TEMP%` dùng ghi timings riêng tư, không body/query/user/token. Startup phases deserialize/snapshot-load/journal-replay/reconcile/index-build và warm auth/clone/render/request được benchmark riêng; các scope lồng nhau, không cộng thời gian một cách máy móc. Google operational budget bổ sung ReadCooldown/WriteCooldown timestamps để phân loại đúng pacing/cooldown, vẫn1250ms/60s và đọc schema cũ2field được; không auth policy change.


## TASK-021 runtime16/10 — cache giao diện và chẩn đoán

HTML cache được gắn `WebUi.CacheRevision` (tách schema/dữ liệu). Thiếu/khác revision: offline chỉ trình bày text đã encode, không chạy form/script/fingerprint cũ; link file chỉ từ ticket cùng phiên, có bytes thực. Không xóa toàn bộ cache, không reset admin. Khi đọc online, dữ liệu/quyền vẫn xác minh ở trung tâm và thay riêng trang đó; JSON/file bytes không cần đổi revision UI. Đổi contract markup/client resources phải bump revision. Workspace LocalOffline cũ giữ nguyên.

GoogleAuthSystemStore công bố capability snapshot-read cho core session validator: batch GET auth hiện hành mỗi request, không cross-request cache; <5 phút không chờ writer nghiệp vụ. Đến mốc touch5ph vẫn re-read trong writer, giữ idle/absolute/revoke/SecurityVersion. RequestAuthStore chỉ memo trong một request. Đây không phải nới TTL/quyền hay bật offline khi Google lỗi.

Diagnostics tùy chọn vẫn ngoài Git: web route-family/method/status/random request-id/phases, không raw route/query/body/user/cookie/token. Auth CLI `Diagnostics:AuthReadSamples` bounded1–5 chỉ đọc thực, có elapsed/phases/call count; không password check hoặc init. Số đo CLI không là browser/login/page latency.


Runtime16/10: UIrevision hiện `web-ui-2026-10-16.2` (revision `.1` là checkpoint trước sửa Razor). `MapPublicUiAssets` chỉ route public thuộc build static manifest và ShortCircuit ở routing; API/gateway/business không dùng cơ chế này. Diagnostic trace trước static/routing để đo0Google/auth thật cho public assets, route-family UiAsset/ConnectionStatus/SystemStatus không raw URL. Không giảm1250ms pacing hoặc đổi quyền/freshness. Final runtime Google/Development, dùng đúng script Start-Web của DuyTien-Web.cmd, không cấu hình LocalOffline hoặc user-secrets mới.
