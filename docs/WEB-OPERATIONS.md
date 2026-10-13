# Vận hành web dev — checkpoint 2026-10-14

Nguồn bằng chứng: [Test Report](07-testing/TEST-REPORT-TASK-014-WEB.md). Tài liệu mô tả phần local; chưa có bằng chứng deploy production hoặc backup/restore thật.

- Giữ .NET 10, Razor, Google Sheets/Drive và drive.file. Credentials nằm ngoài repo; không đưa vào client, tài liệu hoặc log. Không chạy lại provisioning/init/bootstrap/PoC đã đạt.
- Một backend/process lease dùng bộ Google resources. Dừng web trước CLI ghi dữ liệu; không bypass lease hoặc xóa lock để ép writer. Dùng [COMMANDS](COMMANDS.md), kiểm PID/port trước khi start; chỉ dừng PID đã xác minh, không dừng theo tên process.
- Tại checkpoint, launcher PID 13756 và backend PID 23376 đang lắng nghe HTTPS 7008/HTTP 5211. HTTP trả 307 đến HTTPS. Client probe kiểm TLS mặc định/TLS 1.2 thất bại; certificate chain báo UntrustedRoot. Chưa thay trust store hoặc tắt certificate validation. Việc tin cậy chứng chỉ còn là gate runtime, không được báo HTTPS PASS.
- `/api/system/status` chỉ kiểm sự hiện diện cấu hình: WEB_DEMO_LOCAL và liveVerified=false. Endpoint không gọi Google, không thể chứng minh kết nối, ACL, resource, drive.file hoặc schema hợp lệ. Các nội dung này cần bằng chứng riêng khi được phép.
- Tree mutation đối soát Categories/Assets/Inventory/Media. Schema lạ/thiếu, owner mồ côi hoặc ledger không khớp phải dừng. Không sửa Sheet ngoài bảo trì/đối soát hoặc xóa tab/file để mở guard. TreeRegistryDetails và các marker V1_TREE mới chỉ có code local, chưa áp dụng Google thật. Runtime cũ không đọc được action mới phải fail-closed; chưa xác nhận rollback an toàn sau khi có dữ liệu mới.
- Giữ một batch và giới hạn 1800 KiB/45000 ký tự mỗi cell/các row limits. Plan quá lớn dừng trước write, không publish một phần. DRAFT/demo chưa được duyệt. Hoàn tất import job trước tree version mới; retry giữ payload/key/progress.
- Log startup ở LocalAppData/DuyTienAssets/Task014Web, ngoài repo. Không in/copy log chứa dữ liệu thật hoặc token. Fixtures trong test là dữ liệu giả/RAM, không phải tài nguyên Google thật.
- Backup/restore dev thật, retention, host/domain, nguồn/rule/OrgLocation còn OPEN/TBD hoặc NOT_RUN. Không ghi đã diễn tập nếu chưa chạy, không deploy/commit/push từ checkpoint này.

Ưu tiên Web → Android → iOS hoãn. Gate mobile không chặn pha Web và không thay gate Web thiếu bằng chứng. TASK-003 IN_REVIEW, TASK-004 DONE, TASK-005–012 IN_REVIEW; trạng thái hoãn và bằng chứng cũ giữ nguyên.
