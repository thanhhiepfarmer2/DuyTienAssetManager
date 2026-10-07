# TASK-005 — Đăng nhập, admin và phân quyền server/web

- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Design: [AUTH-DESIGN-PROPOSED](../02-architecture/AUTH-DESIGN-PROPOSED.md)
- Created: 2026-10-06; design accepted/implementation started: 2026-10-07
- Phụ thuộc/gate: TASK-004 `DONE`; auth/permission/scope đã được người dùng duyệt;
  live system spreadsheet chưa được tạo/chọn/ghi và vẫn là gate trước `DONE`
- AC liên quan: AC-03, AC-04

## Mục tiêu và phạm vi đã duyệt

Triển khai một lát đầy đủ backend/API + Razor Pages:

- system spreadsheet riêng cho Users/Roles/Permissions/Assignments/Scopes/
  Sessions/SecurityEvents;
- bootstrap/recovery admin bằng lệnh vận hành riêng, secret ngoài repo;
- login/logout/logout-all/change-password, web cookie và mobile token API;
- Admin tạo/sửa/khóa/mở/reset user, gán năm role cố định và scope;
- authorization server-side thống nhất cho list/detail/report/export/file/write;
- last-admin protection và revoke tức thời bằng `SecurityVersion`;
- Razor Pages login, đổi mật khẩu, access denied và quản trị user/quyền;
- test bằng adapter giả/dữ liệu scope giả, sau đó live evidence trên system
  spreadsheet dev trước khi xét `DONE`.

Ngoài phạm vi TASK-005: UI MAUI, SSO/MFA/email forgot-password, custom role,
permission trực tiếp, cơ cấu tổ chức thật và code nghiệp vụ TASK-006 trở đi.

## Đầu vào, phụ thuộc và giả định

- Các quyết định `ACCEPTED` tại AGENTS/REQ-001/ADR-001 được giữ nguyên.
- Ma trận role/permission và scope nằm tại
  [PERMISSIONS](../01-requirements/PERMISSIONS.md), đã `ACCEPTED`.
- Schema auth nằm tại [SHEETS-SCHEMA](../03-database/SHEETS-SCHEMA.md), phần
  TASK-005 đã `ACCEPTED`; chưa tạo/ghi resource Google.
- Tất cả OrgUnit/Location/Category/User dùng cho test thiết kế là fixture giả,
  không mô tả tổ chức Công ty Duy Tiến.
- Không yêu cầu người dùng cung cấp mật khẩu trong chat; secret chỉ được nhập vào
  user-secrets/secret store khi bắt đầu implementation/live setup được cho phép.

## Acceptance criteria

- [x] AC-03a local: bootstrap bằng lệnh riêng tạo `admin` đúng một lần; chạy lại
  không reset hash, role, scope hoặc mật khẩu.
- [x] AC-03b local: plaintext password/token không xuất hiện trong repo, log, response
  thường, Sheet hay build artifact; bootstrap/reset buộc đổi mật khẩu lần đầu.
- [x] AC-03c local: login/logout/change-password/lockout/rate limit/web cookie/mobile
  refresh hoạt động theo timeout đã duyệt; marker token rotate phát hiện reuse;
  reset/khóa/đổi quyền revoke tức thời.
- [x] AC-03d local: không thể khóa, hạ role hoặc bỏ ALL của admin hoạt động cuối cùng;
  recovery chỉ chạy khi không còn admin hoạt động và ở maintenance mode.
- [x] AC-04a local: Viewer bị chặn API admin; AssetEditor không có
  `USERS_MANAGE`/`AUTHZ_ASSIGN`; server là gate. Endpoint tài sản chưa tồn tại và
  không được tạo sớm trong TASK-005.
- [x] AC-04b local core: `Location.ParentId` được validate; exact/descendant/
  sibling, thiếu scope và source+destination move dùng cùng authorization service.
- [x] AC-04c local: nhiều role cộng permission nhưng không cross-combine scope giữa hai
  assignment; permission trực tiếp và deny không tồn tại trong demo.
- [x] AC-04d contract/core: resource thiếu/ngoài scope fail-closed; contract quy
  định 404/403 và lọc trước count. Integration từng endpoint nghiệp vụ sẽ thuộc
  Task tạo endpoint đó, không triển khai CRUD/report sớm.
- [x] AC-04e local build/review: Razor Pages login/change-password/admin dùng cùng
  service với API, POST có anti-forgery mặc định, validation/empty/access-denied.
- [ ] UI/live UAT: tương tác Razor Pages với system spreadsheet dev thật.
- [ ] Live gate: system spreadsheet dev được provisioning/Picker, ACL hạn chế,
  init/bootstrap/restart/revoke kiểm chứng trên Google thật. Fake không thay live.

## Routing/skill đã dùng

- `conductor:context-driven-development`: Context → Spec/Plan, giữ Requirement,
  Plan, Task và tài liệu liên quan đồng bộ; không tạo thư mục context song song.
- `developer-essentials:auth-implementation-patterns`: phân tách authn/authz,
  cookie/token, password hash, rate limit, RBAC và revoke/session.
- `dotnet-contribution:dotnet-backend-patterns`: DI, API, Razor và framework
  password hasher; `ui-design:accessibility-compliance` cho label, validation,
  status/access-denied và form có keyboard semantics.
- Không dùng subagent vì người dùng/skill không yêu cầu.

## Kế hoạch thực hiện local

1. Đồng bộ quyết định đã duyệt, gồm password min 8, `Location.ParentId`, active
   admin theo assignment hiệu lực và process lease liên tiến trình.
2. Tạo abstraction/domain auth và fake store; test password/session/RBAC/scope
   trước khi nối Google.
3. Triển khai system Sheets adapter, command provisioning/Picker/init/bootstrap/
   recovery; không tự chạy command ghi khi start web.
4. Triển khai API/cookie/mobile token và authorization service.
5. Triển khai Razor Pages login/change-password/admin; dùng chung service.
6. Chạy build/test/format, review secret/log/API/scope và cập nhật report.
7. Chỉ khi được phép mới provisioning/Picker system spreadsheet dev và chạy live
   evidence; không đụng production hoặc resource TASK-004 đã hoàn tất.

## Bằng chứng implementation local

| Kiểm tra | Kết quả |
|---|---|
| Format | PASS — `dotnet format` và `--verify-no-changes`, exit 0 |
| Build server | PASS — exit 0; 0 warnings; 0 errors |
| Test | PASS — 57 passed; 0 failed; 0 skipped |
| Google provisioning/Picker/init/bootstrap | NOT_RUN — không được thực hiện trong lượt local |
| Git/deploy/commit/push | NOT_RUN — thư mục không phải Git worktree và không được yêu cầu |
| Chi tiết | [TEST-REPORT-TASK-005](../07-testing/TEST-REPORT-TASK-005.md) |

## Checkpoint / Review

- TASK-004 giữ `DONE`; TASK-003 giữ `IN_REVIEW`; không mở lại test Google cũ.
- TASK-005 chuyển `IN_REVIEW` vì local build/test/review đạt; chưa `DONE` trước
  live Google và UI/UAT.
- Ba điểm nghiệp vụ ở mục 9 và phương án demo đã được người dùng duyệt ngày
  2026-10-07; các điều chỉnh cùng ngày là authoritative.
