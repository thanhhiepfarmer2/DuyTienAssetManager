> Cập nhật15/10: người dùng xác nhận login bằng tài khoản hiện có **PASS_MANUAL riêng case login**; TASK-015 vẫn IN_REVIEW, các gate tự động/Google read/real network/restart/browser khác không tự PASS. UX web được lập riêng tại [PLAN-003](PLAN-003-web-experience.md), TASK-016–021 TODO, không thay REQ-002/ADR-002 v2 hoặc mở quyền ghi offline. Checkpoints dưới giữ lịch sử.

# PLAN-002 v2 — TASK-015 online/offline read-only (2026-10-15)

1. Đọc code/giữ worktree và lịch sử; REQ-002/ADR-002 v2 thay thế v1.
2. Connection probe/state machine, protected session/query cache và mutation/Google transport boundary.
3. Reconnect authority/data freshness, logout/expiry/isolation, UNKNOWN write reconciliation.
4. Một launcher Google mặc định, UI badge/missing cache/search và bảo toàn legacy workspace.
5. Build/tests network fake+filesystem thật, benchmark riêng; môi trường thật/browser chưa chạy ghi NOT_RUN.
6. Đồng bộ report v2/Commands/Configuration/Context, giữ IN_REVIEW nếu thiếu gate. Không Git push/commit/mobile/data thật.

## Checkpoint v2

TASK-015 IN_REVIEW. Code/launcher/cache/guard/reconnect/reconcile local hoàn tất,46/46 mới,35/35 liên quan và3/3logout exit0, Release sạch. Executable/public/TLS/start-reuse đã quan sát; authenticated Google, mất mạng thật/browser và restore/coverage đầy đủ chưa đạt bằng chứng. Không dùng local fake benchmark để kết luận Google nhanh, không đóng gate/task trước.



Scope/coherence cuối: store checkpoint/dependency private, purge cache khi dữ liệu đã quan sát đổi và khi epoch đổi; mixed/cross-epoch read409. Cipher/RAM/ticket bind phiên; operation lease dừng RPC cũ qua reconnect. Google/browser/mạng thật vẫn NOT_RUN.

---
## Lịch sử plan v1

# PLAN-002 — Web LocalOffline

2026-10-14, theo REQ-002 / ADR-002. TASK-015, ngoại lệ bắt đầu khi TASK-005–014 còn gate. Không đổi trạng thái task trước hoặc gate hoãn.

1. Đo baseline và đọc các đường auth/store/coordinator; ghi rõ giới hạn credential/môi trường.
2. Runtime LocalOffline: snapshot/manifest durable, auth riêng, blobs, single writer, consistent reads.
3. Tích hợp Razor/config/launcher/UI state; giữ Google explicit và guard.
4. Test filesystem thật, failure/restart/idempotency/scope/revoke/zero outbound và benchmark fixture665/20744.
5. Đồng bộ báo cáo/lệnh/giới hạn; không commit/push/deploy/mobile/UAT dài.

## Checkpoint thực hiện

TASK-015 IN_REVIEW: phần adapter/services/Razor/launcher hoàn tất local;21checks+207regression và actual128assertions pass. Quantitative warm p95 đạt fixture mục tiêu. Baseline Google protected-before/browser/nguồn/actual restore vẫn thiếu bằng chứng; không DONE/gate giả. Google mode giữ cho thao tác chủ động tương lai; migration/sync không được chạy. Xem Test Report015 cho các failures đã sửa và giới hạn.
