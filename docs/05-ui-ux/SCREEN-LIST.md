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

## TASK-010 local Razor — 2026-10-13

/Items list/search/paging và /Items/Detail/{id} scoped, giữ raw20cột và hai mô tả, DRAFT_DEMO/quarantine không là official. /Items/Import CatalogManager/Admin: upload XLSX/JSON, group mapping CategoryId/tracking nullable, duplicate MERGE/SPLIT có lý do, preview ADD/UPDATE/SKIP/errors/baseline warning, confirm lưu staging; jobs progress và POST next chunk100max/byte budget. Lỗi giữ parsedJSON/mapping/key, STAGING/PARTIAL không trình bày như COMPLETE; file browser cần chọn lại nếu chưa parse thànhJSON.

/Stock list scoped paginated, nullable quantity/min, asOfUTC/source/reason/version và primaryUnit exact, trạng thái UNKNOWN/OUT/LOW/AVAILABLE; link Item và version để nhập snapshot riêng. Viewer chỉ đọc; mọi POST server kiểm quyền/CSRF. Empty/validation/409/503/loading feedback có HTTP rendered tests; browser layout/tương tác thực NOT_RUN, không yêu cầu người dùng UAT. Không UI MAUI hoặc report TASK-011.

Bổ sung UI TASK-010 cuối: sau đọc file, chọn Category nháp/tracking theo từng nhóm bằng dropdown; mã trùng có lựa chọn gộp đồng nhất/giữ riêng và lý do/identity từng dòng. JSON staging chỉ tùy chọn nâng cao, không bắt viết JSON cho mapping/decision thông thường. Snapshot chọn địa điểm được phép bằng dropdown. HTTP form-binding/error/confirm kiểm riêng8/8; chưa browser UAT.

## TASK-011 local Razor — 2026-10-13

/Reports?Code=R01..R04: bốn report REQUIRED, scoped category/org/location dropdowns, Q/type/condition/Flevel hoặc unit/asOfUTC/section, include category descendants và paging. R01 Assets, R02 direct/subtree không double count, R03/R04 matches và quality sections/unknown/nonstock/min-source-missing, exact unit summaries/nullable values. Report và row hiển thị DRAFT/demo, readAtUTC/asOf/source/unit, partial paths che ancestor ẩn. Unlocated catalog row không kho/location/tồn0; date/geo filters chỉ actual snapshot.

CSV toàn filter với current EXPORT scopes có thể hẹp hơn view; Viewer không nút/export server403. Error503 giữ input/selects, không success/empty giả; validation/binding lỗi, trang vượt dữ liệu có feedback và total thật; script loading không bỏ controls. GET read/export nên không form mutation/CSRF token mới (không bỏ anti-forgery nền tảng). Kestrel compiled Razor HTTP đã kiểm, browser DOM/layout/loading/download thật NOT_RUN; không giao UAT thủ công hoặc làm Task012/mobile.

## Android TASK-012 local — 2026-10-13

MAUI MainPage login/environment HTTPS/forced password/logout; AssetListPage q/category(descendants)/org/location/condition/custodian lọc trên server/page20/total/asOf/empty/error và reload khi quay lại; AssetDetailPage/current IDs/state/aliases; AssetEditPage scoped choices/DRAFT_DEMO/MACHINE/full fields/409 reload/giữ input/idempotency; PhotoPage camera/system picker/local preview/state/retry/reconcile/scoped gateway. Có busy/disabled controls, chặn đóng trang pending vô ý, logout bỏ RAM; không offline crash recovery hoặc auto upload. Backend vẫn enforce dù role UI ẩn nút. Native UI/layout/keyboard/navigation/media/permissions chưa chạy: NOT_RUN_ANDROID_UI; compile/core/API tests không thay runtime. [Report](../07-testing/TEST-REPORT-TASK-012.md).

## TASK-014 pha Web —2026-10-14

Home/menu/help/Privacy cóluồngđãtriển khai, anonymouskhôngbusinessnav, admindropdown, collapseid/ariađồngbộ. Categorydetail→Asset/Item/R02; Assetdetail→ảnh/currentCategory/R01. Treepreviewcó registryimpactđãscope; snapshoteditGETnạpdữliệu/currentversion, POSTlỗigiữpayload/key/version, numberbinderinvariant. NativePOST loading/double-submitguard khôngdisablefield/submitter, uploadAJAXgiữcơchếcũ. HTTPcompiledRazor126checks mới/hồiquyđạt; browserDOM/layout NOT_RUN doIAB/Chrome unavailable. [Report](../07-testing/TEST-REPORT-TASK-014-WEB.md).
