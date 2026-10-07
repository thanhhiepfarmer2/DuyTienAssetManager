# TEST-REPORT — TASK-005

- Ngày/môi trường: 2026-10-07; Windows; .NET SDK 10.0.400; target `net10.0`.
- Phạm vi: implementation local đăng nhập, session, admin, RBAC/scope, Razor
  Pages, Google system adapter và lệnh vận hành theo AC-03/AC-04 của REQ-001.
- Dữ liệu test: fixture hoàn toàn giả; fake auth store chỉ nằm trong project test.
- Google: không tạo, chọn hoặc ghi system spreadsheet; không chạy lại PoC cũ.

| AC | Loại test | Case / lệnh | Expected | Actual | Kết quả |
|---|---|---|---|---|---|
| AC-03a | Unit | Bootstrap hai lần | Tạo admin một lần; lần hai no-op, không reset | Admin/assignment/ba ALL giữ nguyên; active-admin = 1 | PASS_LOCAL |
| AC-03b | Unit/source review | Password 7/8 ký tự, chuỗi đơn giản/username/space; framework hasher; scan logging | Chỉ min 8; hash framework; không log/response plaintext/hash | Policy đúng; hasher verify pass; API user không trả hash; log chỉ nói loại lỗi/trạng thái | PASS_LOCAL |
| AC-03c | Unit | Sai password 5 lần; 6 phiên; đổi password; mobile rotate/reuse | Lock 15 phút; giữ 5 phiên; revoke; marker rotate tồn tại và reuse revoke family | Đúng expected | PASS_LOCAL |
| AC-03d | Unit | Future/revoked/expired Admin assignment; thiếu một ALL; khóa admin cuối | Chỉ assignment hiệu lực đủ ba ALL được tính; không về 0 | Đúng expected | PASS_LOCAL |
| Writer | Unit + subprocess | Hai lease manager và auth CLI process riêng dùng cùng lock file | Writer/process thứ hai fail; sau release lấy được | Đúng expected; không dùng lock RAM làm bằng chứng liên tiến trình | PASS_LOCAL |
| AC-04a | Unit + HTTP integration | Catalog permission; Viewer gọi API admin | Role đúng catalog; Viewer 403 | Kestrel integration trả 403 | PASS_LOCAL |
| AC-04b | Unit | `Location.ParentId`, missing parent/cycle, exact/descendant/sibling, move nguồn+đích | Validate cây; fail-closed; cùng assignment phải match cả nguồn/đích | Đúng expected | PASS_LOCAL |
| AC-04c | Unit | AssetEditor scope A + DocumentManager scope B | Union permission, không cross-combine scope | Đúng expected | PASS_LOCAL |
| API | HTTP integration | Login mobile; admin/list user bằng bearer; cookie gọi API admin | Token opaque; admin 200; cookie bị chặn; không lộ PasswordHash | Đúng expected | PASS_LOCAL |
| Google adapter | HTTP stub | Init schema/catalog và persist mutation | Dùng `SystemSpreadsheetId`; có `RefreshTokens`; mutation một batch; không plaintext | Đúng expected | PASS_LOCAL_STUB |
| Razor | Build/review | Login/change-password/logout/users/authorization/events | Authorize server-side; POST anti-forgery; validation/empty/access denied | Compile; PageModel dùng cùng application service; password tạm `no-store` | PASS_LOCAL_BUILD |
| PowerShell | Parser | `Set-AuthOperatorSecret.ps1` | Prompt ẩn; syntax hợp lệ | Parser 0 lỗi | PASS_LOCAL |
| Live gate | Google + browser/manual | Provisioning/Picker, ACL, init/bootstrap/restart/UAT | Evidence trên system spreadsheet dev thật | Chưa được phép thực hiện trong lượt này | NOT_RUN |

Các endpoint CRUD/import/report/file của TASK-006 trở đi không được tạo. Scope
authorization core đã có unit test để các Task sau gọi; chưa giả vờ có integration
test trên endpoint nghiệp vụ chưa tồn tại.

## Lệnh và kết quả cuối

```text
dotnet format .\QuanLyTaiSan.DuyTien.sln --no-restore --verbosity minimal
exit 0

dotnet format .\QuanLyTaiSan.DuyTien.sln --no-restore --verify-no-changes --verbosity minimal
exit 0

dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal
exit 0; warnings 0; errors 0

dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal
exit 0; total 57; passed 57; failed 0; skipped 0
```

Một lần build trước bằng chứng cuối được chạy song song với test và có warning
retry copy `apphost.exe` do hai tiến trình cùng dùng file. Build tuần tự cuối đã
pass sạch 0 warning/0 error; đây là bằng chứng authoritative.

PowerShell parser cho script auth: exit 0, `PASS: PowerShell syntax`.
`git status`: exit 1 với `not a git repository`; không init/commit/push.

## Review quyền, scope, session và logging

- API admin yêu cầu session hợp lệ + role Admin + bearer; cookie bị cấm trên API
  để Razor form/anti-forgery là kênh web thay đổi trạng thái.
- Role/scope được tính từ assignment hiệu lực trên mỗi request. Scope ba chiều
  match trong cùng assignment; dữ liệu thiếu scope fail-closed; move yêu cầu cùng
  assignment match cả nguồn và đích.
- Lock/reset/password/role-scope tăng `SecurityVersion` và revoke session. Web
  idle/absolute/remember, mobile access/refresh, max 5 và reuse family đã test.
- Refresh marker đã rotate không bị xóa/ghi đè thành active; reuse để lại dấu
  `RotatedAt`, revoke token đang active và session family.
- Runtime không có fake auth adapter. Password/token nguyên văn không được ghi
  vào store/log; Google store lưu hash. Mật khẩu tạm chỉ trả một lần với
  `Cache-Control: no-store`.
- Web và auth CLI dùng file lock liên tiến trình; semaphore hiện có chỉ tuần tự
  hóa mutation bên trong một process.

## Chưa kiểm tra

- `GoogleWorkspace:SystemSpreadsheetId` chưa có; chưa provisioning/Picker, kiểm
  owner/ACL, init schema, bootstrap/recovery/restart hoặc UAT trên Google thật.
- Chưa tương tác Razor Pages trong browser với live system store. Compile và core
  HTTP integration pass, nhưng không thay cho live UI/UAT.
- Không chạy deploy, Git init/commit/push hoặc PoC TASK-004.

## Quyết định

Local gate đạt; TASK-005 chuyển `IN_REVIEW`. Chưa đủ điều kiện `DONE` vì live
Google và UAT bắt buộc đang `NOT_RUN`. TASK-003 giữ `IN_REVIEW`, TASK-004 giữ
`DONE`.
