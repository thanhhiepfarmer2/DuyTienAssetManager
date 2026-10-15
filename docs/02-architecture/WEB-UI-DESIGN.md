# Thiết kế giao diện web — Duy Tiến Assets

- Ngày: 2026-10-15. Status: **DRAFT — quy chuẩn đề xuất từ yêu cầu; chưa đối chiếu HTML gốc**.
- Nguồn authoritative: [REQ-003](../01-requirements/REQ-003-web-experience.md), [REQ-002 v2](../01-requirements/REQ-002-local-offline.md), [PERMISSIONS](../01-requirements/PERMISSIONS.md).
- Thực hiện qua [PLAN-003](PLAN-003-web-experience.md), TASK-016–021. Đây là đầu vào lập kế hoạch, không chứng nhận TASK-016 đã kiểm kê đủ hoặc UI đã sửa.

## 1. Nguồn và ranh giới

`QuanLyVatTu_DuyTien_v4_Offline.html`: **MISSING_REFERENCE / NOT_REVIEWED_HTML** sau tìm filename trong repo/attachments. Không có screenshot hoặc browser của tool được AI quan sát lượt này. Màu, header/sidebar và mô tả trải nghiệm lấy từ yêu cầu người dùng; không ghi “đã đối chiếu tool”. TASK-016 phải đọc đúng file trước khi chốt quy chuẩn. Không chép dataset20744x12, Apps Script, IndexedDB/auth, rule ID/mã hoặc source thật vào app/Git. Contract VTHH20 cột hiện tại giữ riêng.

## 2. Bố cục và tokens đề xuất

Header mang thương hiệu **Duy Tiến Assets**, tên người dùng từ phiên server và kết nối. Sidebar gồm Trang chủ, Danh mục, Tài sản, Hồ sơ, Vật tư/hàng hóa (Items), Số lượng (snapshot), Báo cáo, Quản trị theo quyền, Hướng dẫn. Breadcrumb dẫn về danh sách và giữ filter hợp lệ. Nội dung trắng trên nền xám sáng; page header có tiêu đề/mô tả ngắn/action chính; tiếp theo filter, feedback, bảng/thẻ và paging. Trên màn hình hẹp sidebar thu gọn; cây chỉ hiện tại màn hình dùng cây.

| Token/nhóm | Giá trị/đề xuất | Phạm vi |
|---|---|---|
| Brand | #0b74e5 | Người dùng chốt màu tham chiếu; header/action primary |
| Heading/text | #102a43 / #243b53 | Không dùng chữ nhỏ/nhạt thay thông tin quan trọng |
| Canvas/surface/border | #eef3f7 / #ffffff / #d9e2ec | Không phân trạng thái chỉ bằng màu |
| Typography | System Segoe UI/Arial/sans-serif, body16px, line-height~1.5, helper tối thiểu14px | Đề xuất kỹ thuật, không download font; kiểm tiếng Việt thật ở TASK-017/021 |
| Spacing | Thang4/8/12/16/24/32px, control đủ vùng bấm, margin field đều | Đề xuất; không thu font để nhét bảng |
| Focus | Outline rõ và không bị header/sidebar che; giữ skip link | Tab/Shift+Tab/Enter/Escape; không tạo keyboard trap |
| Icon | Một bộ SVG local có license rõ, kèm nhãn text/accessible name | Chọn ở TASK-016, không tự cài icon/CDN trong lượt này |
| Table/card | Table cho dữ liệu so sánh; card cho media/shortcut phù hợp | Không tạo toggle giả nếu chỉ có một view; không tự tải full dataset |

Màu/tokens dùng semantic CSS variables/partials trong Razor hiện có; không thêm design platform hoặc framework mới. Phép tính màu sRGB trong lượt lập kế hoạch: trắng trên #0b74e5 khoảng4.52:1, #243b53 trên trắng khoảng11.50:1. Chỉ là tính trên màu cố định, chưa kiểm hover/disabled/focus/thực tế browser; khi triển khai phải kiểm lại mọi cặp và không dựa vào màu đơn độc.

## 3. Thành phần dùng chung

- Layout/header/sidebar/breadcrumb/page heading; active route và return URL local an toàn; menu không phát sinh lượt đọc auth riêng ngoài request pipeline.
- Filter bar có label, áp dụng/xóa lọc, count sau scope; giữ query khi paging/quay lại. Lưu mở cây/chọn/scroll theo user+workspace và view, không giữ row/PII toàn cục hoặc dữ liệu user trước.
- Form field/summary validation; focus lỗi đầu; giữ dữ liệu đã nhập và ExpectedVersion/key. 409 yêu cầu so sánh/tải bản mới có chủ ý, không âm thầm ghi đè. Input file không thể tự gán lại sau navigation: nói rõ khi bytes đã không còn và yêu cầu chọn lại; không giả đã giữ file.
- Empty thực khác thiếu cache/thiếu quyền/dịch vụ hỏng. Loading kết thúc ở mọi nhánh; chống double-submit nhưng không thay idempotency server.
- Xác nhận nguy hiểm hiển thị mục tiêu, phạm vi ảnh hưởng/lý do theo contract, cancel an toàn. Có fallback form server/anti-forgery; JS chỉ cải thiện thao tác.
- File tile/preview/version/status; download qua gateway đã phân quyền, không lộ URL Drive/token. Preview unsupported/corrupt rõ, không tạo file giả.
- Thông báo đang lưu/đã lưu/lỗi/kết quả chưa rõ; “đã lưu” chỉ sau backend xác nhận bền vững. Không tự gửi lại bằng key mới sau lỗi mạng.

## 4. Bản đồ màn hình sơ bộ từ source hiện tại

Các path là URL Razor mặc định, tham số `{id}` theo @page. Bảng chưa thay kiểm kê từng field/action/role ở TASK-016.

| Nhóm / path | Khả năng đã thấy trong source | Đầu việc UX / task |
|---|---|---|
| Shared _Layout/site.css/_ValidationScriptsPartial/site.js/connection.js | Navbar ngang, local Bootstrap/jQuery, skip link, connection banner, footer demo | Header/sidebar/breadcrumb/tokens/components và nội dung chung,017 |
| /Account/Login, ChangePassword, Logout, AccessDenied | Login/password/logout form, forced-change, lỗi dịch vụ có correlation | Nhất quán states/validation; giữ auth hiện có,017 |
| /Offline (ConnectedRuntime sinh HTML), /api/connection, /api/connection/files | Query cache có scope, partial coverage và bytes availability | Đồng nhất shell/status, không giả trang mới nhất hoặc cache đủ,017/021 |
| /Categories, /Categories/Detail/{id}, /Categories/Import, /Categories/Manage/{id?} | List/tree/search/TXT, metadata; preview/import; preview/commit sửa/chuyển/ngừng | Tree context, impact/diff/trùng tên và guard errors,018 |
| /Assets, /Assets/Create, /Assets/Detail/{id}, /Assets/Move/{id?} | Search/filter/page, CRUD, soft-delete/restore; chuyển nhiều AssetIds có preview/commit | Form/chi tiết/bulk selection/giữ context,018; không tự thêm bulk action khác |
| /Assets/Photos/{id} | Upload/update/delete/reconcile/cancel | Preview, pending/failed/cancel/primary/retry,019 |
| /Documents, /Documents/Detail/{id}, /Files/Content/{id} | Metadata, tạo version, DOCX/PDF upload/publish/archive, gateway | Phân biệt document/version/file/source/bytes,019 |
| /Items, /Items/Detail/{id}, /Items/Import | Search/filter/detail, staging/chunk/resume theo contract | Field mapping/error/progress/job và không trùng dữ liệu,019 |
| /Stock | Search/page và nhập snapshot qty/min/unit/source/asOf/version | Nullable/UNKNOWN/không theo dõi/đơn vị rõ,019 |
| /Reports | R01–R04, filter/aggregate/page/exportCSV | Assets khác Items; scope trước tổng/export; units/asOf rõ,019 |
| /Admin/Users, /Admin/Users/Detail/{id}, /Admin/Authorization, /Admin/SecurityEvents | Create/update/lock/unlock/reset, assignments/scopes, role matrix/audit | Validation/xác nhận/revoke/admin cuối cùng theo core,020 |
| /, /Help, /Privacy, /Error | Shortcut/hướng dẫn/privacy/error, nhiều copy demo/task/kỹ thuật | Ngôn ngữ công việc, không KPI giả,020 |
| /Account/LocalSetup, /Admin/LocalDemo | Code legacy còn, v2 middleware trả410 | Không đưa vào menu/onboarding; giữ code/workspace cũ,017/020 |

Field sơ bộ cần đối chiếu đầy đủ ở TASK-016: Assets tên/loại/category/org/location/custodian/serial/ngày/tình trạng/aliases/notes; tree category/destination/name/segment/definition/reason/delete plan/AssetIds; media metadata/version/kind/description/primary/file/reason; stock Item/location/unit/quantity/minimum/asOf/source; admin username/display/role/effective dates/ba scope/descendants; report filters/section/unit/asOf. Required/length/default/enum chỉ lấy từ DTO/binder/service hiện có, không tự đặt rule.

## 5. Nội dung và nhãn trạng thái

| Giá trị/ý nghĩa giữ trong domain | Nhãn trình bày dự kiến | Quy tắc |
|---|---|---|
| DRAFT | Chưa duyệt | Không tự đổi sang approved khi đổi copy |
| DRAFT_DEMO/fixture | Dữ liệu kiểm thử / chưa xác nhận nguồn | Nhãn gần dữ liệu, export/preview giữ provenance; không gọi toàn ứng dụng demo |
| PUBLISHED | Đã phát hành | Nếu source là thử nghiệm/chưa duyệt, vẫn hiện nhãn nguồn tương ứng; không ngầm công nhận công ty |
| OUT / LOW | Đã hết / Sắp hết | 0 và 0<quantity<=minimum theo core |
| UNKNOWN / NOT_TRACKED / TRACKING_UNCONFIRMED | Chưa có số lượng / Không theo dõi tồn / Chưa xác nhận theo dõi tồn | Không thay bằng0 hoặc gộp thành một trạng thái |
| MISSING_MINIMUM / SOURCE_MISSING | Chưa có ngưỡng / Thiếu nguồn | Không tự đặt ngưỡng mặc định |
| File PENDING / FAILED / AVAILABLE | Đang xử lý / Không thành công / Sẵn sàng trên trung tâm | AVAILABLE không chứng minh bytes đã có offline |
| Bytes chưa cache | Chưa có bản lưu ngoại tuyến | Không nút mở file giả thành công |

Menu/header/footer/home/help không quảng bá “demo”, “PoC”, “bản nháp”, “scaffold”, tên task hoặc chi tiết triển khai cho người dùng thường. Ngoại lệ là nhãn trạng thái/nguồn thật và hướng dẫn vận hành kỹ thuật dành đúng đối tượng. Không thay đổi tên literal trong audit cũ, IDs, raw enums hoặc test evidence để pass content scan.

## 6. Kết nối, dữ liệu và bảo mật

| Trạng thái thật | Hiển thị / khả năng |
|---|---|
| Chưa login | Mời đăng nhập; không khẳng định đã có cache hoặc quyền offline |
| ONLINE sau xác minh trung tâm | Đầy đủ chức năng theo quyền; thời điểm xác minh dữ liệu thật, không lấy giờ UI giả. Snapshot asOf nghiệp vụ tách thời điểm đọc/xác minh |
| OFFLINE_READ_ONLY | “Ngoại tuyến — chỉ đọc”, lần đồng bộ/bản lưu cuối, coverage thiếu/query/file; chỉ phiên hợp lệ từng online, không nút chuyển offline tùy ý |
| RECONNECTING | Đang kết nối lại, chưa mở ghi; giữ form/UNKNOWN/key và đối soát trước retry |
| SERVICE_UNAVAILABLE/không đủ bằng chứng mạng | Báo dịch vụ/kết nối chưa xác minh; không gọi là mất Internet hoặc dùng snapshot cũ như current |
| Hết hạn/revoke/khóa/đổi user | Login online lại; xóa khả năng xem dữ liệu/corpus của phiên cũ theo core, không giữ DOM/selection chứa dữ liệu user trước |

Không thêm polling/auth/metadata call cho từng component, không bỏ pacing/guard/freshness, không đọc Google trực tiếp từ browser. Các read/count/aggregate/page/export/file đều scope ở server. UI cache version/migration cần giữ provenance và quyền; không gọi refresh CSS là refresh dữ liệu. Snapshot thiếu không thành empty/0. Admin không được ghi ngoại tuyến hoặc xem lại admin data ngoài allowlist.

## 7. Gap cần phân loại trước code

| ID | Khoảng trống / trách nhiệm | Cách xử lý |
|---|---|---|
| REF-01 | Thiếu HTML đúng nguồn | TASK-016 ghi nguồn/hash metadata khi có file, không import dữ liệu; chưa được DONE phần đối chiếu |
| MAP-01 | Chưa kiểm kê toàn bộ field/action/role/error/legacy route ở mức contract | TASK-016 lập bảng route→DTO/service/permission→field/action→trạng thái/test; phân biệt có code với runtime đã chạy |
| BE-01 | Bulk ngoài chuyển tài sản/impact tree hiện có chưa có xác nhận contract | Ghi thiếu backend; không vẽ nút hoặc tự thêm bulk create/delete/module mới |
| BE-02 | Dashboard counters/KPI mới chưa có contract và nguồn được duyệt | Home dùng shortcuts có thật; chỉ hiện số khi có read đã scoped/verified, không tự thêm service hoặc gọi dư Google |
| BE-03 | Offline cache chỉ phủ query/bytes đã đọc, HTML do middleware sinh khác Razor | TASK-017/021 rà shell/render/cache compatibility; không hứa full dataset/mirror hoặc offline form write |
| BE-05 | Razor Items import mới quan sát Preview/Start/Chunk, chưa thấy handler cancel job | TASK-016 đối chiếu API/service trước khi đưa hủy job vào UI; không tạo thao tác giả |
| BE-04 | Source/rule/ngưỡng/đơn vị/địa điểm chưa chốt | Giữ OPEN/TBD và thiếu thông tin; không “sửa” UI thành nghiệp vụ giả định |

Nếu phát hiện thiếu chức năng server khác, ghi rõ trigger/actual/expected, contract/permission, phạm vi được duyệt và test trước sửa. Thay đổi nghiệp vụ mới ngoài scope phải trở thành yêu cầu/task riêng; không che bằng UI. TASK-016 sẽ cập nhật gap log sau kiểm kê thật.

## 8. Cách xác minh khi triển khai

Build và filtered tests của đúng phần đổi; HTTP/rendered khác browser. Khi có browser dùng fixture GIẢ/DEV trong phạm vi được phép để kiểm desktop1280x720, hẹp375/768, keyboard/focus/validation/loading/empty/error/conflict/return context/download. Không giao checklist UAT cũ cho người dùng. Nếu không có provider giữ NOT_RUN_BROWSER, task không DONE khi browser là gate bắt buộc. Online/offline latency và Google call counts đo riêng; mục tiêu GET warm p95<500ms kế thừa REQ-002 nhưng không dùng fake/local để chứng minh Google. Không phát sinh Google live/ghi dữ liệu thật chỉ vì đã lập plan.
