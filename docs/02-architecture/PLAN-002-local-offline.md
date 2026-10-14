# PLAN-002 — Web LocalOffline

2026-10-14, theo REQ-002 / ADR-002. TASK-015, ngoại lệ bắt đầu khi TASK-005–014 còn gate. Không đổi trạng thái task trước hoặc gate hoãn.

1. Đo baseline và đọc các đường auth/store/coordinator; ghi rõ giới hạn credential/môi trường.
2. Runtime LocalOffline: snapshot/manifest durable, auth riêng, blobs, single writer, consistent reads.
3. Tích hợp Razor/config/launcher/UI state; giữ Google explicit và guard.
4. Test filesystem thật, failure/restart/idempotency/scope/revoke/zero outbound và benchmark fixture665/20744.
5. Đồng bộ báo cáo/lệnh/giới hạn; không commit/push/deploy/mobile/UAT dài.

## Checkpoint thực hiện

TASK-015 IN_REVIEW: phần adapter/services/Razor/launcher hoàn tất local;21checks+207regression và actual128assertions pass. Quantitative warm p95 đạt fixture mục tiêu. Baseline Google protected-before/browser/nguồn/actual restore vẫn thiếu bằng chứng; không DONE/gate giả. Google mode giữ cho thao tác chủ động tương lai; migration/sync không được chạy. Xem Test Report015 cho các failures đã sửa và giới hạn.
