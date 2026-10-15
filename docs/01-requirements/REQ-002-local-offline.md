# REQ-002 v2 — Online mặc định, ngoại tuyến chỉ đọc

**Authoritative từ 2026-10-15**, theo chỉ đạo mới của người dùng, thay thế quyền ghi và tài khoản LocalOffline độc lập ở v1 bên dưới. Web/.NET10/Razor/Sheets/Drive giữ nguyên; Android tạm dừng, iOS DEFERRED_BY_USER.

- ONLINE dùng auth/dữ liệu trung tâm hiện có, không reset admin. Mỗi response đọc phải qua xác thực/phân quyền và đọc trung tâm hiện hành; cache không được trả dưới nhãn mới nhất. Chưa có checkpoint đáng tin thì đọc lại, giữ pacing/cooldown.
- OFFLINE_READ_ONLY chỉ sau probe mạng có bằng chứng lỗi kết nối; navigator.onLine chỉ gợi ý. Cache chỉ sau phiên online hợp lệ trên máy, ràng buộc central workspace/user/session/SecurityVersion/scopes và hạn phiên trung tâm, không tự gia hạn offline. Không password/hash/OAuth credential trong cache.
- Chỉ đọc/tìm/lọc dữ liệu đã cache theo quyền; thiếu trang/query/bytes phải báo thiếu, không giả AVAILABLE hoặc đầy đủ. Hiển thị Ngoại tuyến — chỉ đọc, asOf riêng từng snapshot. Cache query có thể không phủ toàn bộ dataset; không coi nhiều snapshot khác thời điểm là một snapshot toàn công ty.
- Chặn mọi mutation tại server trước handler, cả admin/import staging/upload/reconcile/tree/CLI. Logout/thu hồi cache là thao tác kỹ thuật được phép; không hàng đợi ghi hoặc sync local lên trung tâm.
- RECONNECTING probe giới hạn/backoff, xác minh phiên/user/SecurityVersion/quyền rồi đọc lại dữ liệu mới, mới mở ghi. Revoke/lock/expiry loại cache và yêu cầu login online; quyền đổi/thu hẹp xóa snapshot cũ. 401/403/schema/429/503 không tự thành offline; trung tâm lỗi có SERVICE_UNAVAILABLE.
- Ghi mất kết nối giữ key và trạng thái UNKNOWN; journal trung tâm phải được đối soát trước retry cùng key/payload. Không có bằng chứng kết quả thì tiếp tục chặn, không suy diễn chưa ghi và không đổi key.
- Cache filesystem mã hóa và DPAPI key ngoài Git, private ACL/exclusive lease, snapshot nguyên tử, phân tách user/session/central ID. Logout làm cache phiên đó vô hiệu qua restart. Workspace LocalOffline v1 được bảo toàn, ngừng làm mặc định/khởi tạo admin; không nhập vào trung tâm.

AC v2: first login offline denied; online→offline readonly/direct write denied; reconnect validates+fresh data before write; revoke/lock/scope/expiry/user isolation/restart/missing bytes/UNKNOWN tests; đo online/offline riêng. Local fake transport không Google live/browser PASS. Không commit/push/deploy/data thật/mobile/UAT dài.

---
## Lịch sử v1 — superseded, không là hành vi hiện hành

# REQ-002 — Web LocalOffline và hiệu năng

Ngày 2026-10-14. READY theo yêu cầu người dùng, bổ sung/thay thế phần online-first của REQ-001 cho mode LocalOffline. Google mode và bằng chứng cũ giữ nguyên. Web trước; Android tạm dừng, iOS DEFERRED_BY_USER.

LocalOffline là backend Razor/.NET10 hiện có với adapter filesystem/JSON thật, dữ liệu/auth/journal/bytes ngoài Git, không SQL/SQLite. Workspace tách biệt Google; không fallback khi mạng lỗi, không tự sync. Login/quyền/scope/revoke, import/CRUD/media/report phải hoạt động không Internet. Hash framework, password minimum8 và chính sách phiên không đổi. Phiên Google không được mang sang local.

Snapshot RAM đã kiểm schema/journal/references; request đọc snapshot nhất quán, không chờ writer không liên quan. Commit bền vững trước response, giữ ID/mã/counter/version/idempotency/audit; tree commit cùng generation tất cả registry. UI chỉ cache dữ liệu đã scope, paging và tài nguyên local, giữ form/key khi lỗi và trạng thái lưu rõ.

Reference do người dùng cung cấp: QuanLyVatTu_DuyTien_v4_Offline.html, Drive file 1ilMyTYJnKNbsvhie919cAElKZxBB9hh_. Phân tích tĩnh người dùng: HTML~7,19MB tự chứa, 20744x12, RAM Map/searchIndex/memoized/deferred search, render60 tăng dần, IndexedDB application/state-v1 debounce900ms; Apps Script login/mutations, cached-user offline chỉ đọc. Không tải HTML/dataset vào Git. Không phải benchmark hoặc nguồn nghiệp vụ được duyệt. Contract hiện tại VTHH20 cột giữ riêng; provenance/rule/nguồn OPEN/TBD.

AC: adapter/runtime độc lập Google; restart và zero outbound; atomicity/failure/retry/stale/scope/revoke; blob size/hash; fixture GIẢ cây665(15/98/552), Items20744; đo startup/cold/warm p95 GET mục tiêu<500ms, không tự ghi đạt. Launcher Windows một instance, không build/restore/download mỗi lần. Export/import chủ động phải manifest/schema/hash/IDs, auth/session không tự chuyển. Chưa triển khai sync hai chiều hoặc phê duyệt dữ liệu công ty.
