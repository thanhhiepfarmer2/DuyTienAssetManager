> Cập nhật triển khai15/10: nguồn HTML đã nhận đúng bytes/SHA256 và đọc tĩnh ở TASK-016; [inventory](../02-architecture/WEB-UI-INVENTORY-TASK-016.md). Người dùng cho phép thực hiện liên tục016→021; task trước đủ local nhưng thiếu browser/Google/network được tiếp tục với ngoại lệ ghi rõ và giữ IN_REVIEW. Không commit/push/deploy/mobile/Google resources/real writes. Các mô tả chỉ lập kế hoạch/thiếu nguồn dưới đây thuộc checkpoint trước.

# REQ-003 — Hoàn thiện trải nghiệm web Duy Tiến Assets

- Status: IN_REVIEW — đã đối chiếu HTML tĩnh và triển khai local TASK-016–021; browser/Google/mạng thật chưa đủ.
- Ngày: 2026-10-15 (Asia/Saigon). Người xác lập: Nguyễn Thanh Hiệp.
- Nguồn: yêu cầu người dùng trong file đính kèm “Cập nhật kế hoạch phát triển DuyTien Assets…”, ngày15/10; không chép dữ liệu công ty từ HTML vào repo.
- Plan: [PLAN-003](../02-architecture/PLAN-003-web-experience.md); thiết kế: [WEB-UI-DESIGN](../02-architecture/WEB-UI-DESIGN.md); Task: TASK-016–021 tại [INDEX](../06-tasks/INDEX.md).
- Đọc cùng [REQ-001](REQ-001-asset-digitization.md), [REQ-002 v2](REQ-002-local-offline.md), [PERMISSIONS](PERMISSIONS.md) và [ADR-001](../02-architecture/ADR-001-proposed-sheets-drive-web-mobile.md).

## Mục tiêu và người dùng

Đưa web hiện có thành công cụ làm việc dễ dùng cho người tra cứu, quản lý tài sản/danh mục/hồ sơ và quản trị tài khoản. Ưu tiên web; Android tạm dừng, iOS DEFERRED_BY_USER. Giữ ASP.NET Core .NET10/Razor Pages, Sheets/Drive và một backend single-writer. Người dùng xác nhận đã đăng nhập thành công bằng tài khoản hiện có: chỉ case này PASS_MANUAL, không đóng các gate TASK-015 khác.

## Trong phạm vi

Kiểm kê màn hình/field/action; quy chuẩn màu, typography, icon, bố cục, điều hướng, breadcrumb, bảng/thẻ, form và trạng thái dùng chung. Hoàn thiện cách trình bày các luồng backend đã có: account/admin, cây, Assets, ảnh/hồ sơ, import, Items/snapshot, R01–R04, trang chủ/hướng dẫn và online/offline. Các sửa backend cần thiết phải được chỉ ra riêng, gắn contract/permission/test trước khi code, không giả bằng UI.

## Ngoài phạm vi

Không chuyển React, sao chép Apps Script/auth/dataset nhúng hoặc cách tạo ID/mã của tool tham chiếu. Không tạo module/report mới, KPI giả, offline write queue, SQL/SQLite, chính sách password/session mới, mobile, deploy, commit/push hoặc dữ liệu thật. Không tự duyệt nguồn/rule/ngưỡng/OrgUnit/Location. Không chạy lại chuỗi UAT cũ hoặc timeout dài.

## Nguồn tham chiếu và độ tin cậy

File thực nhận `D:/Dev/01-QuanLyVatTu_DuyTien_v4_Offline.html` khớp7190714bytes/SHA256577423ad5166a11a14597d97650366b2052af366df076c5f6a23bb78e92e22a6. TASK-016 đọc CSS/classes tĩnh bằng HTMLParser; không thực thi script/Apps Script, không nhập dataset hoặc đưa HTML vào Git. **MISSING_REFERENCE/NOT_REVIEWED_HTML của checkpoint lập kế hoạch đã RESOLVED_STATIC**; [report016](../07-testing/TEST-REPORT-TASK-016.md) và [inventory](../02-architecture/WEB-UI-INVENTORY-TASK-016.md) là nguồn hiện tại. Báo cáo lập kế hoạch giữ lịch sử thiếu file lúc đó. UI runtime chưa có browser pass.


## Luồng công việc

1. Đăng nhập bằng tài khoản hiện có; thấy tên người dùng và trạng thái kết nối đúng thực tế.
2. Từ sidebar chọn module có quyền; dùng breadcrumb, tìm/lọc và paging. Cây nằm ở màn hình cần duyệt phân loại, không áp cho mọi module.
3. Mở chi tiết; nếu có quyền và online đã xác minh, thao tác bằng form có nhãn/validation. Hành động nguy hiểm xác nhận mục tiêu và ảnh hưởng.
4. Lỗi validation/conflict/dịch vụ giữ form và key thích hợp; chưa rõ kết quả ghi phải giữ UNKNOWN và đối soát, không báo thành công hoặc tạo key mới để gửi lại mù.
5. Mất Internet đã được server xác định: chỉ đọc bản lưu trong scope và phiên còn hạn; hiện thời điểm bản lưu/thiếu file. Reconnect xác minh tài khoản/phiên/SecurityVersion/quyền và dữ liệu trước ghi.

## Quy tắc

| ID | Quy tắc | Nguồn/giới hạn |
|---|---|---|
| UX-01 | Header xanh, thương hiệu “Duy Tiến Assets”, người dùng hiện tại và kết nối; sidebar module | Chỉ đạo15/10; không thêm cây toàn cục bắt buộc |
| UX-02 | Màu gốc #0b74e5/#102a43/#243b53/#eef3f7/#d9e2ec; nền sáng, mặt nội dung trắng, viền nhẹ | Người dùng; tokens đã đối chiếu CSS tĩnh ở TASK-016; visual browser chưa nghiệm thu |
| UX-03 | Breadcrumb, heading, search/filter/action/page/status thống nhất; không render20744rows cùng lúc | Dùng paging hiện có, không tải lại toàn bộ dataset cho thao tác giao diện |
| UX-04 | Font/icon/tài nguyên thiết yếu local; tiếng Việt dễ đọc, focus và keyboard rõ | Không CDN hoặc font download lúc chạy |
| UX-05 | Nội dung giới thiệu ứng dụng không gọi là demo/PoC/bản nháp/scaffold; không lộ tên task | Áp cho copy giới thiệu/menu/footer/help thông thường; không đổi raw trạng thái dữ liệu |
| UX-06 | DRAFT/DRAFT_DEMO/PUBLISHED/nguồn kiểm thử có nhãn tiếng Việt đúng ý nghĩa, vẫn nhận biết chưa duyệt | Không đổi enum/schema/ID, không coi phát hành kỹ thuật là dữ liệu công ty đã duyệt |
| UX-07 | Assets khác Items; 0/LOW/UNKNOWN/nontracked và đơn vị/asOf khác nhau | REQ-001 và thiết kế R01–R04, không suy số lượng từ BOM/kho |
| UX-08 | Menu chỉ hỗ trợ điều hướng; permission/scope/count/export/file vẫn server kiểm | PERMISSIONS; không lấy quyền từ cache giao diện hoặc ghép role/scope |
| UX-09 | Online phải đọc/xác minh hiện hành; lỗi401/403/429/503/schema không tự thành offline | REQ-002 v2/ADR-002; không giảm pacing/guard để tạo cảm giác nhanh |
| UX-10 | Không thống kê giả, link chết hoặc nút chưa có hành vi | Thiếu backend ghi gap/contract riêng; không tự mở rộng tính năng |

## Acceptance criteria

| ID | Given / When / Then | Kiểm phù hợp / task |
|---|---|---|
| UX-AC01 | Có file tham chiếu thật → kiểm kê/đối chiếu layout, field/action và ghi khác biệt có căn cứ | Tài liệu+nguồn, TASK-016; hiện NOT_REVIEWED_HTML |
| UX-AC02 | Người dùng đăng nhập → nhận diện/layout/menu/login/đổi mật khẩu thống nhất, không hồi quy auth | Build/rendered/HTTP/auth regression + browser khi khả dụng, TASK-017 |
| UX-AC03 | Duyệt/tìm/sửa/chuyển cây và tài sản → mọi luồng hiện có rõ, đúng scope/version/guard/idempotency | TASK-018, các service tests liên quan và browser nếu có |
| UX-AC04 | Dùng hồ sơ/import/Items/snapshot/report → preview, lỗi, bytes, trạng thái, đơn vị/asOf/UNKNOWN rõ | TASK-019; không báo fixture là Google/browser |
| UX-AC05 | Dùng admin/home/help → copy công việc, không demo branding; nguồn chưa duyệt vẫn nhận biết, không KPI giả | TASK-020, content/HTTP/permission checks |
| UX-AC06 | Offline/reconnect/revoke/dirty form/missing bytes → UI và server cùng trạng thái, không ghi offline/leak dữ liệu | TASK-021 và gate TASK-015 theo từng nguồn bằng chứng |
| UX-AC07 | Desktop/narrow/keyboard và tải lớn → có bằng chứng interaction/layout và số đo nguồn rõ | TASK-021; không coi build hoặc fake timing là browser/Google pass |

## Mục còn mở

HTML gốc chưa có; bộ icon/font cụ thể và một số layout vi mô cần TASK-016 chốt theo tài nguyên local/license. Nguồn workbook/rule/ngưỡng/đơn vị/địa điểm và gate cũ tiếp tục OPEN/TBD. Lượt này chỉ tạo tài liệu và Task TODO, không triển khai UX-AC02–07.
