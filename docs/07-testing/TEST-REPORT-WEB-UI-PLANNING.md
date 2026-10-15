# Báo cáo kiểm tra tài liệu — kế hoạch trải nghiệm web

Ngày: 2026-10-15 (Asia/Saigon), working directory `D:\Dev\QuanLyTaiSan_DuyTien`. Phạm vi lượt này **chỉ lập kế hoạch** theo yêu cầu đính kèm, không thực hiện TASK-016 hoặc sửa hàng loạt UI.

## Nguồn và xác nhận

- **Người dùng xác nhận thủ công:** đã đăng nhập thành công bằng tài khoản hiện có → PASS_MANUAL riêng login. Ghi thêm vào report TASK-015; không suy ra AI auth-read/consent/schema/assignments hoặc các gate khác PASS. TASK-015 IN_REVIEW.
- **AI đọc/quan sát trực tiếp:** AGENTS, WORKFLOW/PLUGIN-ROUTING/templates, PROJECT-CONTEXT/INDEX/PLAN-001/002, REQ-001/002, permissions/thiết kế liên quan; routes/handlers/layout/assets và một số DTO/forms hiện có. Bản đồ source là sơ bộ để lập task, không kiểm kê/đối chiếu hoàn chỉnh của TASK-016 hoặc browser/runtime pass.
- **AI tìm nguồn:** filename HTML đúng tên trong toàn repo (hidden/ignored, loại .git) và attachments, không phân biệt hoa/thường → 0/0 file, command exit0. Không đọc HTML gốc/Drive/dataset. MISSING_REFERENCE/NOT_REVIEWED_HTML, thiết kế DRAFT theo màu/định hướng người dùng; không giả đã đối chiếu.
- `conductor:context-driven-development` và `ui-design:design-system-patterns` đọc SKILL.md thật, dùng cho đồng bộ context và tokens/components. Thực hiện tuần tự, không subagent/orchestrator hoặc phát sinh conductor/ thứ hai.

## Tài liệu đã tạo và trạng thái

[REQ-003](../01-requirements/REQ-003-web-experience.md), [PLAN-003](../02-architecture/PLAN-003-web-experience.md), [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md) và TASK-016–021 trong [INDEX](../06-tasks/INDEX.md). Requirement/Plan READY cho lập kế hoạch; thiết kế DRAFT. **Sáu task TODO**, mọi AC triển khai NOT_RUN; không lấy việc tạo file làm Task DONE.

Đồng bộ Plan tổng/context/index/navigation/source audit và login manual record. Copy ứng dụng dùng ngôn ngữ công việc nhưng data state/provenance giữ; plan tách backend gaps BE-01…05/MAP/REF khỏi thiếu UI. Source/rule/ngưỡng/OrgUnit/Location OPEN/TBD; Android tạm dừng/iOS DEFERRED_BY_USER. Giữ nguyên mọi code/script/auth/OAuth/data và thay đổi chưa commit.

## Cách kiểm tài liệu

Checker UTF8 inline được ghi đầy đủ tại [COMMANDS](../COMMANDS.md), `python -B -` qua PowerShell here-string (không tạo pyc). Kiểm ID/file mới, statuses/sections/AC/evidence, dependency chain/cycle, local links, HEAD/index và fingerprint toàn bộ baseline tracked/untracked; `git diff --check` riêng. Chỉ xuất metadata, không secret/hash password/dữ liệu runtime. Kết quả thực tế ghi dưới sau khi chạy, không suy diễn exit code.

Build/test ứng dụng, browser/Google live/real network/performance trong lượt này: **NOT_APPLICABLE** vì không code/runtime; các gate triển khai tương ứng vẫn **NOT_RUN/đã hoãn theo lịch sử**, không PASS. Không yêu cầu UAT thủ công cũ/secret, không restart backend/timeout, không commit/push/deploy.


## Kết quả thực tế — AI chạy/quan sát trực tiếp

| Kiểm tra | Expected | Actual / evidence |
|---|---|---|
| ID và phạm vi mới | Không overwrite task/req/plan có sẵn | PASS_DOCS: IDs016–021, REQ003/PLAN003 chưa tồn tại trước tạo; 6Task TODO,10docs mới |
| Task fields/AC/evidence | Đủ scope/exclusions/dependencies/screens/AC/checks, chưa triển khai | PASS_DOCS:29AC mở, evidence NOT_RUN, không đánh dấu task DONE |
| Trạng thái/lịch sử |003 IN_REVIEW,004 DONE,005–012/014/015 IN_REVIEW,013 TODO/deferred; mới TODO | PASS_DOCS:19status checks; history/gates không đóng |
| Dependencies/links | Chain016→021 không cycle, local links có đích | PASS_DOCS: DAG acyclic,224local links,0issue |
| Bảo toàn worktree | Không đổi code/test/script/auth/data hoặc stage | PASS_DOCS: baseline470files,12docs có sẵn thay đổi trong allowlist,10docs mới, tất cả file còn lại cùng fingerprint; indexempty/HEADd8a508d không đổi |
| HTML source | Có đúng file mới được tuyên bố đã đối chiếu | MISSING_REFERENCE/NOT_REVIEWED_HTML:0workspace+0attachments, không tự PASS; thiết kế DRAFT |
| Login cũ | Bằng chứng đúng người thực hiện | PASS_MANUAL riêng login, do người dùng xác nhận; không AI Google auth-read hoặc gate khác PASS |

Lệnh kiểm docs UTF8 qua `python -B -` theo COMMANDS: lần dựng lệnh đầu **exit1**, Python SyntaxError do here-string lồng nhau bị kết thúc sớm, **checker chưa chạy**, không thay source. Sửa cách ghép delimiter; lần sau **exit0**, rồi bổ sung kiểm cycle/whitespace và lần cuối **exit0** với metadata như bảng. Không xóa/giảm tiêu chí để đạt. `git diff --check` **exit0**, không stdout lỗi; không coi đây là build/test ứng dụng.

Self-review docs theo yêu cầu và templates: tách branding khỏi trạng thái dữ liệu; giữ REQ002v2; gaps backend rõ (bulk/KPI/import cancel chưa thấy handler/offline coverage/rule); nguồn HTML thiếu không thành đã review. Không independent reviewer hoặc audit bảo mật. Bước đầu tiếp theo là **TASK-016**, có thể kiểm kê code độc lập nhưng cần HTML đúng nguồn để chốt phần đối chiếu; chưa triển khai task này trong lượt hiện tại. Không tiếp tục Android/iOS.
