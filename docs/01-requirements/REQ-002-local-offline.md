# REQ-002 — Web LocalOffline và hiệu năng

Ngày 2026-10-14. READY theo yêu cầu người dùng, bổ sung/thay thế phần online-first của REQ-001 cho mode LocalOffline. Google mode và bằng chứng cũ giữ nguyên. Web trước; Android tạm dừng, iOS DEFERRED_BY_USER.

LocalOffline là backend Razor/.NET10 hiện có với adapter filesystem/JSON thật, dữ liệu/auth/journal/bytes ngoài Git, không SQL/SQLite. Workspace tách biệt Google; không fallback khi mạng lỗi, không tự sync. Login/quyền/scope/revoke, import/CRUD/media/report phải hoạt động không Internet. Hash framework, password minimum8 và chính sách phiên không đổi. Phiên Google không được mang sang local.

Snapshot RAM đã kiểm schema/journal/references; request đọc snapshot nhất quán, không chờ writer không liên quan. Commit bền vững trước response, giữ ID/mã/counter/version/idempotency/audit; tree commit cùng generation tất cả registry. UI chỉ cache dữ liệu đã scope, paging và tài nguyên local, giữ form/key khi lỗi và trạng thái lưu rõ.

Reference do người dùng cung cấp: QuanLyVatTu_DuyTien_v4_Offline.html, Drive file 1ilMyTYJnKNbsvhie919cAElKZxBB9hh_. Phân tích tĩnh người dùng: HTML~7,19MB tự chứa, 20744x12, RAM Map/searchIndex/memoized/deferred search, render60 tăng dần, IndexedDB application/state-v1 debounce900ms; Apps Script login/mutations, cached-user offline chỉ đọc. Không tải HTML/dataset vào Git. Không phải benchmark hoặc nguồn nghiệp vụ được duyệt. Contract hiện tại VTHH20 cột giữ riêng; provenance/rule/nguồn OPEN/TBD.

AC: adapter/runtime độc lập Google; restart và zero outbound; atomicity/failure/retry/stale/scope/revoke; blob size/hash; fixture GIẢ cây665(15/98/552), Items20744; đo startup/cold/warm p95 GET mục tiêu<500ms, không tự ghi đạt. Launcher Windows một instance, không build/restore/download mỗi lần. Export/import chủ động phải manifest/schema/hash/IDs, auth/session không tự chuyển. Chưa triển khai sync hai chiều hoặc phê duyệt dữ liệu công ty.
