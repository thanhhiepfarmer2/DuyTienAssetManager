> Cập nhật15/10: **PASS_MANUAL** — người dùng xác nhận đã login thành công bằng tài khoản hiện có. Chỉ case login này, không AI Google auth-read hoặc các gate khác PASS. TASK-015 giữ IN_REVIEW; [bản ghi](../07-testing/TEST-REPORT-TASK-015-ONLINE.md). Kế hoạch UX kế tiếp [PLAN-003](../02-architecture/PLAN-003-web-experience.md), TASK-016–021 TODO; chưa triển khai. Các checkpoint dưới giữ lịch sử.

# TASK-015 v2 — Online mặc định / cache offline chỉ đọc

Trạng thái: IN_REVIEW (2026-10-15). Người dùng thay thế quyền ghi LocalOffline bằng REQ-002/ADR-002/PLAN-002 v2. Account trung tâm không reset; legacy workspace giữ nguyên. AC v2 theo REQ-002; task trước/gate hoãn giữ. Skill .NET backend patterns/session-guard; thực hiện tuần tự, không subagent. Không commit/push/deploy/mobile hoặc dữ liệu thật.

Bằng chứng v1 bên dưới và report cũ chỉ đúng phiên bản v1, không dùng để ghi PASS v2. Report mới: TEST-REPORT-TASK-015-ONLINE.md.

> Login fix 15/10: Google thật trả OAuth HTTP400/`invalid_grant` trước Sheets; config Google/Development và guard first-login đã kiểm. Bản sửa log an toàn/CLI read-only +78/78tests, Release sạch; đang chờ consent desktop cùng app `drive.file`, chưa Google auth-read/login PASS. Xem [báo cáo](../07-testing/TEST-REPORT-TASK-015-ONLINE.md). Giữ IN_REVIEW, không reset admin.

## Checkpoint v2 đã quan sát

Google online mặc định, encrypted scope-bound query/bytes cache read-only, probe/backoff và mutation/CLI guard, reconnect authority+fresh read, durable UNKNOWN/reconcile, một launcher đã triển khai. Local tests46/46exit0, hồi quy chọn lọc35/35exit0 và3/3logout; Release0warnings/errors exit0; actual executable strict HTTPS/public6checks, launcher start/reuse0/0. Một backend PID 24384 tại https://localhost:7008; legacy workspace nguyên36files. Không reset admin hoặc nhập legacy vào trung tâm.

[Report v2](../07-testing/TEST-REPORT-TASK-015-ONLINE.md) ghi rõ fixture/HTTP/filesystem khác Google/browser. Gate mở: authenticated Google v2 và restart/Internet transitions thật; browser provider unavailable; cache chỉ phủ query/bytes đã đọc, không full dataset mirror/revision batching/restore; sources/rules OPEN/TBD. Không DONE, không đóng gate cũ; Android tạm dừng/iOS DEFERRED_BY_USER. Không commit/push.



Scope/coherence cuối: store checkpoint/dependency private, purge cache khi dữ liệu đã quan sát đổi và khi epoch đổi; mixed/cross-epoch read409. Cipher/RAM/ticket bind phiên; operation lease dừng RPC cũ qua reconnect. Google/browser/mạng thật vẫn NOT_RUN.

---
## Lịch sử TASK-015 v1

# TASK-015 — Web LocalOffline và tối ưu snapshot

Trạng thái: IN_REVIEW. 2026-10-14. Requirement REQ-002, ADR-002, PLAN-002. Người dùng cho phép LocalOffline filesystem/JSON và bắt đầu khi task trước IN_REVIEW; giữ mọi gate hoãn, Android tạm dừng/iOS DEFERRED_BY_USER.

Phạm vi: đo trước/sau; adapter runtime thật, auth/business/blobs ngoài Git, atomic manifest + generation và single-writer; pin read snapshot, indexes; Razor/config/launcher local; tests restart/concurrency/retry/stale/scope/revoke/zero outbound, fixture665/20744. Không SQL/SQLite/React rewrite/Google sync/real data/mobile/deploy/commit/push. Không phê duyệt nguồn/rule.

AC theo REQ-002; DONE chỉ khi đầy đủ bằng chứng, browser/runtime chưa có ghi NOT_RUN. Skill đã đọc: conductor:workflow-patterns (workflow chính, test trước); dotnet-contribution:dotnet-backend-patterns (DI/async/errors); session-guard giữ ràng buộc. Tự thực hiện tuần tự, không subagent. Quyền user không commit/push thay gợi ý commit của skill.

## Hoàn tất local / bằng chứng — 2026-10-14

Adapter filesystem/JSON thật, manifest+module generations+auth+blobs, private ACL/lease, read pinning/index và write durability/atomic joint registry đã triển khai. Runtime mode/cookie/workspace tách Google, không fallback/sync. Razor setup/demo explicit, local assets, bounded tree60/Items paging, keep form/key khi503/409, Windows launcher đã kiểm start/reuse/stop/restart HTTPS thật. Metadata guard/reference immutability/CAS và password/session policy giữ.

[Report](../07-testing/TEST-REPORT-TASK-015.md): Release build sạch exit0;21/21local+budget và207/207affected regression exit0 (có overlap); final actual Release HTTPS128/128assertions exit0. Warm p95 fixture665/20744các GET đều<500ms, số cụ thể/phase/baseline limits ghi report. Default backend riêng vẫn chạy, không auto seed/bootstrap.

Gate mở: NOT_RUN_BROWSER (provider unavailable), protected Google before-baseline chưa có credential/process, source/rule/OrgLocation/ngưỡng/workbook OPEN/TBD, hardware-failure/packet capture/actual restore chưa thử. Không coi local fixture là nguồn công ty hoặc đóng task trước. iOS DEFERRED_BY_USER/Android tạm dừng. Không commit/push/deploy/mobile, Google DEV nguyên vẹn.
