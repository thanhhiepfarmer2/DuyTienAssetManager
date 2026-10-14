# TASK-015 — Web LocalOffline và tối ưu snapshot

Trạng thái: IN_REVIEW. 2026-10-14. Requirement REQ-002, ADR-002, PLAN-002. Người dùng cho phép LocalOffline filesystem/JSON và bắt đầu khi task trước IN_REVIEW; giữ mọi gate hoãn, Android tạm dừng/iOS DEFERRED_BY_USER.

Phạm vi: đo trước/sau; adapter runtime thật, auth/business/blobs ngoài Git, atomic manifest + generation và single-writer; pin read snapshot, indexes; Razor/config/launcher local; tests restart/concurrency/retry/stale/scope/revoke/zero outbound, fixture665/20744. Không SQL/SQLite/React rewrite/Google sync/real data/mobile/deploy/commit/push. Không phê duyệt nguồn/rule.

AC theo REQ-002; DONE chỉ khi đầy đủ bằng chứng, browser/runtime chưa có ghi NOT_RUN. Skill đã đọc: conductor:workflow-patterns (workflow chính, test trước); dotnet-contribution:dotnet-backend-patterns (DI/async/errors); session-guard giữ ràng buộc. Tự thực hiện tuần tự, không subagent. Quyền user không commit/push thay gợi ý commit của skill.

## Hoàn tất local / bằng chứng — 2026-10-14

Adapter filesystem/JSON thật, manifest+module generations+auth+blobs, private ACL/lease, read pinning/index và write durability/atomic joint registry đã triển khai. Runtime mode/cookie/workspace tách Google, không fallback/sync. Razor setup/demo explicit, local assets, bounded tree60/Items paging, keep form/key khi503/409, Windows launcher đã kiểm start/reuse/stop/restart HTTPS thật. Metadata guard/reference immutability/CAS và password/session policy giữ.

[Report](../07-testing/TEST-REPORT-TASK-015.md): Release build sạch exit0;21/21local+budget và207/207affected regression exit0 (có overlap); final actual Release HTTPS128/128assertions exit0. Warm p95 fixture665/20744các GET đều<500ms, số cụ thể/phase/baseline limits ghi report. Default backend riêng vẫn chạy, không auto seed/bootstrap.

Gate mở: NOT_RUN_BROWSER (provider unavailable), protected Google before-baseline chưa có credential/process, source/rule/OrgLocation/ngưỡng/workbook OPEN/TBD, hardware-failure/packet capture/actual restore chưa thử. Không coi local fixture là nguồn công ty hoặc đóng task trước. iOS DEFERRED_BY_USER/Android tạm dừng. Không commit/push/deploy/mobile, Google DEV nguyên vẹn.
