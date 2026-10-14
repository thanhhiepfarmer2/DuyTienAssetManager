# Vận hành web dev — 2026-10-14

## Trạng thái Google DEV hiện hành

HTTPS đã trust ở tài khoản Windows vận hành và kiểm bằng TLS validation bật; chứng chỉ backend phục vụ đã đối chiếu. Ba resource DEV cũ đúng owner-only/exact drive.file. Init explicit đã tạo 23 bảng mới và repeat no-op; cây/reference giả đã import, không đổi auth/PoC hoặc resource IDs cấu hình. [Report Google/HTTPS](07-testing/TEST-REPORT-TASK-014-GOOGLE-DEV.md) là bằng chứng mới, các mô tả TLS blocked bên dưới là checkpoint trước.

Khởi động bằng `dotnet run --project src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj --no-build --no-restore --launch-profile https`, sau kiểm port/PID; không tạo backend thứ hai. `https://localhost:7008` là endpoint dev, chưa host/domain công ty. CLI `google-web-dev-init` chỉ chạy khi web dừng và đã backup; schema lạ/partial không overwrite. Không init/bootstrap/PoC lại.

429/503: giữ form/key, không bấm ghi bằng key mới. Executor pace reads/writes riêng và cooldown 429; full registry có thể tải chậm, chưa có benchmark tải công ty. Cursor operational ở cạnh process lease, không chứa business data/secret; kiểm permission/integrity khi lỗi, không tắt guard hoặc sửa Sheets để tránh quota.

Backup dùng `Backup-Task014GoogleDev.ps1 -Backup`: DPAPI CurrentUser ngoài Git; cần cùng Windows profile để giải mã. Đã có Sheets values/metadata và Drive manifest, chưa có bytes/format đầy đủ hoặc restore thật. `Test-Task014RestorePlan.ps1` chỉ kiểm đầu vào/coverage. Restore cần hai đích DEV mới, owner-only, kiểm typed cells/ledger/IDs/roles/scope/Drive links/active sessions trước switch; không ghi đè source hoặc đổi user-secrets IDs đang dùng.

Browser chưa có provider. Login cookie ban đầu đạt; một cookie cũ về sau trả 302, không gán thành browser/session UAT PASS. GET Razor qua bearer hợp lệ là HTTP rendered evidence riêng. Android tạm dừng, iOS DEFERRED_BY_USER.

## Checkpoint local trước live

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
