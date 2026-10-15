> Bổ sung v2 — scope/coherence: observed adapters chỉ lấy checkpoint từ state trung tâm đã đọc, không thêm RPC hoặc thay guard. Cache lưu dependency nội bộ, purge query/bytes khi checkpoint đã quan sát đổi, không cho revision cũ quay lại. Sang connection epoch mới phải bỏ corpus cũ trước refresh; mixed-version/cross-epoch read409, không publish/cache dưới nhãn current. Không public global revision counters. Ticket bind credential digest; ciphertext bytes và RAM index bind phiên+snapshot. Cache prototype chưa có binding cần online xác minh lại, không dùng để cấp quyền offline; không reset admin hoặc đụng workspace LocalOffline v1.

> Tác vụ HTTP/CLI có operation lease: sau vòng mất/kết nối lại, RPC cũ dừng trước OAuth/transport (kiểm lại sau pacing), giữ UNKNOWN/key nếu business đã bắt đầu; request mới cần revalidate+fresh data/reconcile. Người có proof/cookie còn hạn mở launcher offline được chuyển từ Login tới /Offline, không tạo tài khoản hoặc xác thực password local.

> Hardening v2: probe hosted background, known-offline snapshot reads không chờ network timeout. Request và write-ready bind connection epoch; timestamp proof lấy trước validation, SecurityVersion cũ không ghi đè mới. Memo Sheets properties chỉ trong request GET/HEAD, invalidate sau ghi và không cache values/file/auth xuyên request. Intent bền sau auth, trước business handler; auth chưa gửi nghiệp vụ không tự thành UNKNOWN.

# ADR-002 v2 — Cache ngoại tuyến chỉ đọc cho một backend online

Accepted 2026-10-15 theo REQ-002 v2. Thay thế runtime LocalOffline ghi độc lập; giữ nguyên implementation/workspace v1 để bảo toàn, không tự chạy/migrate.

Backend mặc định Google. Thêm connection gate trước auth/application HTTP boundary và trước Google executor (bao gồm CLI). Probe HTTPS nhỏ Microsoft/Google và public Sheets fallback không OAuth, TLS validation, không cookie/OAuth; response HTTP bất kỳ chứng minh có kết nối, chỉ lỗi transport của các probe độc lập cùng bằng chứng route/link mới cho offline; kết quả không đủ là UNKNOWN/SERVICE_UNAVAILABLE, fail closed. Probe backoff và coalesce, không business requests khi offline.

Cache **response đã phân quyền**, không sao chép raw auth hoặc store toàn cục. Chỉ allowlist GET nghiệp vụ, không trang form/admin/auth/staging/reconcile. Mỗi query+content negotiation+session có snapshot bytes/headers/asOf nguyên tử; tìm trong index snapshot đã cache. Query/file chưa cache báo thiếu. Online luôn chạy service trung tâm; không dùng response cache cho online nên không tuyên bố cache còn current qua TTL. Current inter-module read consistency tiếp tục theo single-writer/coordinator/guard đã có. Các snapshot riêng có asOf riêng, không ghép thành một dataset thời điểm chung.

Cache lưu ticket proof không raw token/password/hash: hash credential, user/session, SecurityVersion, authority fingerprint và expiresAt = min(idle, absolute, thời điểm grant hết hiệu lực). DataProtection + DPAPI Windows/private ACL, root ngoài repo, exclusive cache lease. Quyền đổi xóa entries cũ; proof cũ trong request cạnh tranh không được phục hồi snapshot sau proof mới. Cache logout/revoke có tombstone bền vững; không local login/admin.

Write intent kỹ thuật giữ method/path/key/payload hash trước khi gửi, không lưu body/password và không phải queue. Thành công/validation xác định thì clear; lỗi/đứt kết nối giữ UNKNOWN. Reconnect đọc journals theo user/key dưới coordinator; chỉ operation hoàn tất và request retry cùng fingerprint mới được tiếp tục replay; thiếu journal bằng chứng vẫn block. Media pending cần đối soát contract có quyền khi online; không tự upload/sync.

Mặc định launcher duy nhất Google+cache, legacy launcher chuyển tới launcher mới; không build/download mỗi lần. Cookie trung tâm dt_session giữ, không dùng dt_local. Đổi môi trường central identity phải đổi cache namespace. Hạn chế query coverage/thiết bị/mạng thật/Google live phải báo đúng trong report v2.

---
## Lịch sử ADR v1 — superseded

# ADR-002 — LocalOffline snapshot và commit filesystem

Ngày 2026-10-14. ACCEPTED theo yêu cầu trực tiếp người dùng. Bổ sung ADR-001, không thay stack Razor/.NET10. Hai mode explicit Google và LocalOffline, không tự fallback. LocalOffline không resolve Google credentials/clients; outbound HTTP bị chặn tại DI và kiểm tự động.

Workspace ngoài repository, owner Windows ACL; manifest version1 + WorkspaceId riêng. Business descriptor JSON chứa checksum/reference tới bốn module JSON bất biến Categories/Assets/Inventory/Media và journals. Chỉ module đổi mới ghi file; thao tác ảnh/tài sản không rewrite20744Items. Auth JSON riêng dùng framework hash và policy hiện có, không copy Google sessions. Load và validate một lần trước phục vụ. Các state không được trả mutable RAM nội bộ; đọc clone snapshot, index thuộc generation. Coordinator có ReadAsync pin generation xuyên các store, không lấy write semaphore; Google dùng default coordinator vẫn giữ consistency cũ. Auth gate riêng; revoke được kiểm trước mỗi request.

Business writes validate candidate rồi serialize module đổi và business descriptor thành generation bất biến, Flush(true), rename rồi thay manifest atomically. Publish RAM sau manifest commit; trước commit failure giữ generation cũ. Tree mutation commit cả bốn registry trong một generation, giữ metadata/journal guard. Auth ghi generation nhỏ riêng rồi merge manifest dưới gate ngắn, không serialize20744Items mỗi login. Phiên pinned cũ chỉ tồn tại cho request đã bắt đầu; request mới thấy revoke. Blob immutable flush+hash trước metadata AVAILABLE; PENDING/FAILED giữ reconciliation hiện có. Không báo file có khi thiếu bytes. Giữ generations trước cho backup/recovery explicit, không auto reset/fallback khi corrupt. Atomic rename phụ thuộc filesystem local NTFS, không claim chịu mọi kiểu mất điện/ổ mạng.

Launcher dùng output Release chuẩn bị trước, cấu hình workspace/mode/HTTPS, không tải/build lúc chạy; process lease tại workspace tránh hai writer. Workspace cùng mode dùng cookie riêng. Launcher không dừng process lạ. Google backend cũ chỉ dừng đúng PID đã xác minh nếu cần phục vụ localhost7008 cho LocalOffline; không ghi Google.

Không tự migration Google ở lượt này. Local transfer package explicit có schema/version/provenance/checksums và bytes đã kiểm; không token Google hoặc phiên auth. Drive manifest không bytes phải báo thiếu. Dữ liệu fixture GIẢ chỉ qua lệnh seed explicit trên workspace rỗng, không mặc định production. Benchmark/reference khác nguồn không phê duyệt rule.
