# Màn hình dự kiến

Web: đăng nhập/đổi mật khẩu; dashboard report; cây danh mục (tree+detail); danh sách tài sản (filter+grid); form tài sản; chi tiết+ảnh+hồ sơ+lịch sử; import preview; snapshot số lượng; hồ sơ tài liệu/biểu mẫu+versions; admin users/roles/scopes; TXT export.
Ứng dụng Android/iOS riêng bằng .NET MAUI: đăng nhập; tìm/lọc/list; chi tiết; tạo/sửa trong scope; chụp ảnh/chọn thư viện/preview/retry; xem mẫu PDF/tải DOCX theo khả năng thiết bị; report cơ bản. Quản lý hàng loạt cây/import/user ưu tiên web Razor Pages, thêm vào mobile chỉ khi Task cần.
UI ẩn/disable thao tác theo quyền, nhưng API vẫn là nơi enforce. Có loading/empty/error/forbidden/conflict và trạng thái DRAFT/UNKNOWN/FAILED rõ.
Thao tác xóa/chuyển cây có màn preview đếm children/assets/docs và phạm vi; giữ ID hiện rõ, lý do bắt buộc cho bulk.
Mobile online-first, giữ draft form trong phiên khi lỗi mạng; không tuyên bố offline sync. File tạm được cleanup, không lưu Google secret/cache nhạy cảm tùy ý.
Ngôn ngữ: tiếng Việt; chưa có yêu cầu song ngữ cho ứng dụng này.

TASK-006 local đã có `/Categories` (tree/tìm/empty/error/TXT), `/Categories/Detail/{id}` (path chỉ trong scope/definition/provenance), `/Categories/Import` (upload→preview→diff/duplicate names→confirm DRAFT→commit; conflict/loading). Razor có antiforgery/no-store và cùng service API. HTML render/POST tự động pass; browser desktop/mobile/JS loading chưa nghiệm thu, không coi là browser UAT.

## TASK-007 local — Razor tài sản nháp

/Assets: scoped list/search mã hiện tại/mã cũ/alias/tên/serial, chọn danh mục gồm con/đơn vị/địa điểm/tình trạng/người quản lý/includeDeleted, pageSize1–100/paging. /Assets/Create và /Assets/Detail/{id}: một loại MACHINE giả; tạo/sửa/select scope, giữ ID/mã/nhóm cấp ban đầu, alias active/history, soft delete/restore với reason/expectedVersion. Banner DRAFT_DEMO/DRAFT, không coi form completed là approved.

CSRF mọi POST, service recheck permission/scope; Viewer chỉ xem, API direct write vẫn chặn. Binding validation400/service422, stale409 không overwrite và có reload; Google503 giữ input/key, empty/loading/access denied. Layout Bootstrap responsive/aria status; HTML HTTP tests đã chạy, browser/layout thật NOT_RUN. Không giao UAT checklist/secret mới trong lượt.

## TASK-008 local — 2026-10-13

`/Categories/Manage/{id?}` Admin/CatalogManager: chọn sửa tên/mã nháp/definition, move cây con hoặc ngừng với EMPTY_ONLY/RELOCATE/SOFT_DELETE; nguồn/đích chỉ scope, lý do, preview diff/count/Asset, confirm tác động và commit. `/Assets/Move/{id?}` Admin/AssetEditor: tìm/chọn tối đa100 Asset, danh mục/đơn vị/địa điểm đích, preview và confirm. Category detail có history scoped; cây/TXT/Asset list lọc theo snapshot mới, ID/mã đã cấp giữ nguyên. Không phải giao diện MAUI.

DRAFT/DRAFT_DEMO không được đổi approved. CSRF mọi POST; permission/scopes recheck ở service, cả nguồn/đích cùng assignment. Validation/no-op/cycle/F11/409/503/loading/empty/access denied, giữ reason/key/selected IDs/đích khi đọc lỗi. Commit đã xác nhận nhưng refresh lỗi nói rõ trạng thái thật. Bootstrap responsive/aria; compiled Razor HTTP và JS syntax có evidence local, browser interaction/layout/loading thật NOT_RUN. Không giao checklist UAT mới.

## TASK-009 local — 2026-10-13

`/Documents`: scoped search/list, empty/error, registry metadata create bởi Admin/DocumentManager. `/Documents/Detail/{id}`: metadata update/soft-archive, create version, slot DOCX/PDF riêng và trạng thái/counterpart missing, effective date UTC, confirm PUBLISHED_DEMO; published file immutable, old pairs giữ theo version. Viewer chỉ thấy published demo có hiệu lực trong scope, không coi DRAFT là mẫu chuẩn công ty.

`/Assets/Photos/{id}`: PNG/JPEG upload/preview/cancel lựa chọn, mô tả/ảnh chính, state PENDING/AVAILABLE/FAILED, soft-delete và reconcile/cancel operation. `/Files/Content/{id}` cookie GET gateway áp dụng owner/scope như API, PDF view/download và photo preview, DOCX attachment. Asset detail/nav có link tương ứng. Không phải UI MAUI/camera.

CSRF, validation/413/stale409/503/forbidden/empty/loading, per-form keys; form lỗi giữ key/input/version cũ, không tự nâng version. Fetch upload giữ File object và metadata khi HTTP/network lỗi, controls busy; no-JS file phải chọn lại theo giới hạn browser, metadata/key vẫn giữ. Cancel/reconcile không dùng nhầm key upload. Bootstrap/aria/status/layout implementation có HTTP rendered evidence56 task, browser interaction/layout thật NOT_RUN; không giao UAT mới.
