# TASK-014 — Nghiệm thu, backup/restore và hướng dẫn

- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate hiện hành: pha Web TASK-005–011; mobile tách pha, không chặn Web. Gate gốc trước14/10: TASK-008,009,011,012,013 (lịch sử).
- AC liên quan: AC-01..AC-14

## Phạm vi
Chạy acceptance toàn bộ, test tải/import quota theo nguồn thực, kiểm tra bảo mật scope/file, backup/restore dev, hướng dẫn user và vận hành.

## Acceptance criteria của Task
- [ ] AC có evidence thật; UAT người dùng được lưu
- [ ] Restore dev giữ IDs/mapping/quyền và Drive links
- [ ] Hạn chế iOS/Google/live tests nêu rõ; không tự deploy/publish stores

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra ban đầu — lịch sử trước 2026-10-14
NOT_RUN — gói tài liệu chưa thực thi Task trên máy người dùng. Trước DONE, ghi lệnh/thao tác, working directory, exit code, test counts, thiết bị và link evidence theo COMMANDS thực tế.
Đối với DOCS, build/test app NOT_APPLICABLE nếu chưa có code, nhưng link/nguồn/quyết định vẫn phải kiểm tra.

## Checkpoint / Review ban đầu — lịch sử trước 2026-10-14
Chưa thực hiện. Ghi phần đã làm, findings/self-review/independent review, phần chưa xác minh và cách tiếp tục. Không chuyển DONE nếu test bắt buộc chưa chạy hoặc fail.

## Phân pha authoritative — 2026-10-14

Chỉ đạo mới thay yêu cầu iOS trước đó; không chạy acceptance toàn bộ hoặc timeout/Google đã hoãn.

| Pha | Phạm vi | Gate |
|---|---|---|
| Web — IN_REVIEW/local + Google DEV fixture complete; strict HTTPS đạt | Rà/sửa tích hợp TASK-005–011, menu/navigation/luồng/validation/loading/empty/409/503/giữ form, full registry tree guard, hướng dẫn web/vận hành và checks tự động/browser khả dụng | Không phụ thuộc Android/iOS; nguồn/rule/Google/backup-restore thật chưa đạt vẫn ghi đúng |
| Android — DEFERRED_BY_USER | Giữ local TASK-012; phát triển/runtime tiếp sau tổng kết Web rõ | Thiết bị/runtime/OS/TLS/Google chưa chạy |
| iOS — DEFERRED_BY_USER | Giữ target/code TASK-013, chưa môi trường/thiết bị | Mac/Xcode/signing/device NOT_RUN, phân phối PROPOSED/TBD |

[Thiết kế trước triển khai](../02-architecture/WEB-COMPLETION-DESIGN-TASK-014.md). Giữ ID/mã/history/version/idempotency/scope, metadata guard/single-writer/DRAFT và min8; không SQL/rule mới/migration Google/real writes/commit/push/deploy hoặc giao UAT thủ công.

### Acceptance pha Web

- [x] Local HTTP/rendered: các menu/pages đã có nối thành luồng, login/role/errors/context có kiểm tra.
- [x] Local fakeHTTP/core: tree registry đầy đủ: source/destination permission, orphan/partial schema/stale/digest, một batch/retry, không bypass guard.
- [x] Build/test local đạt; ghi browser NOT_RUN do IAB/Chrome unavailable, không đánh dấu browser PASS.
- [x] Hướng dẫn và Task/Report/INDEX/Context/gates đồng bộ; mobile hoãn không thành PASS.

## Checkpoint local / gate — 2026-10-14

Build exit 0, 0 warnings/errors, 4.17 s; 126/126 selected tests pass, exit 0, 0 failed/skipped. Đã sửa menu/hướng dẫn, full registry reconciliation, stock locale/current values, Media delta và config-only status. [Report](../07-testing/TEST-REPORT-TASK-014-WEB.md), [guide](../WEB-USER-GUIDE.md), [ops](../WEB-OPERATIONS.md).

Một backend PID 23376 lắng nghe HTTPS 7008/HTTP 5211; HTTP trả 307. HTTPS client chain báo UntrustedRoot nên BLOCKED_TLS_TRUST; chưa thay trust hoặc tắt kiểm TLS. Browser/Google/schema mới/nguồn/rule/backup-restore chưa nghiệm thu. Overall IN_REVIEW, chưa DONE.

Android/iOS DEFERRED_BY_USER; không tự chuyển mobile. Giữ worktree, HEAD, gates và bằng chứng lịch sử; không commit/push/deploy/dữ liệu thật/gate Google đã hoãn hoặc timeout mới.

## Tiếp tục Google DEV — 2026-10-14

Người dùng cho phép live DEV và migration/dữ liệu hoàn toàn giả trong pha Web; Google DEV của pha này không còn bị hoãn. Không mở lại timeout/UAT cũ, không chuyển mobile. [Report HTTPS/Google](../07-testing/TEST-REPORT-TASK-014-GOOGLE-DEV.md) bổ sung bằng chứng mới; report local phía trên là lịch sử checkpoint trước. Overall giữ IN_REVIEW khi browser/source/restore chưa đủ.

## Checkpoint cuối Google DEV — 2026-10-14

[Report](../07-testing/TEST-REPORT-TASK-014-GOOGLE-DEV.md): build0warnings/errors, selected78/78 local exit0; finish57/57 live exit0, full history giữ. HTTPS200, GoogleDEV schema/replay/module/scope/tree/bytes/restart đã có bằng chứng fixture. Sửa429 bằng pacing/cooldown, giữfreshguard/version/scope và single-writer. Credential/session checkpoint xóa đã probe. Backend13420/launcher9236 chạy một instance tại https://localhost:7008.

Backup49tab DPAPI ngoài Git/plan/decrypt/preservation đã đạt, không Drive bytes/actual restore. Browser unavailable; cookie cũ302 chưa retest bằng cookie mới; rendered bearer không browser/cookiePASS. Nguồn/rule/OrgLocation/ngưỡng/workbook/hosting/performance còn OPEN/TBD/NOT_RUN. Tổng thể IN_REVIEW, không DONE; Android tạm dừng/iOS DEFERRED_BY_USER, task trước/gates hoãn giữ. Không commit/push/deploy hoặc dữ liệu thật.


## Kế hoạch trải nghiệm web tiếp theo — 2026-10-15

[REQ-003](../01-requirements/REQ-003-web-experience.md) / [PLAN-003](../02-architecture/PLAN-003-web-experience.md) / [thiết kế DRAFT](../02-architecture/WEB-UI-DESIGN.md) bổ sung TASK-016–021 TODO theo thứ tự. Đây là lập kế hoạch UI cho web hiện có; không hoàn tất hoặc thay acceptance/restore/Google/browser gate TASK-014. Người dùng xác nhận login hiện có PASS_MANUAL riêng case đó, không các gate TASK-015 khác. Giữ IN_REVIEW, Android tạm dừng/iOS DEFERRED_BY_USER; không tự chuyển mobile. Lượt này không sửa UI/runtime hoặc chạy nghiệm thu.
