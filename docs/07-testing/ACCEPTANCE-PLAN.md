# Kế hoạch nghiệm thu

Không có test ứng dụng đã chạy trong phiên soạn này. Các case sau sẽ thực hiện theo Task/code/môi trường đã tạo.

## Case bắt buộc
1. Import cây → 665 nodes / 15+98+552; report 39 tên nhiều mã; lần hai không nhân đôi; changed workbook preview.
2. Admin bootstrap login đúng cấu hình, restart không reset, hash không plaintext; user-editor-viewer các chức năng thực tế.
3. Viewer gửi POST/PATCH/DELETE trực tiếp; user scoped truy cập Id/fileId/report ngoài phạm vi; bị chặn, không lộ tổng hay data.
4. 2 concurrent creates, retry cùng key, reuse key payload khác, timeout sau commit và restart; unique code và một entity.
5. Change classification/root/location/name, soft-delete/restore; ID/code không đổi hoặc tái cấp.
6. Category move cây con, cycle/F11, bulk delete có impact, scope nguồn/đích, export TXT Unicode.
7. Import MISA duplicate CĐKEM, duplicate header Mô tả, textcodes, chunk failure/resume, zero vs missing quantity.
8. R03/R04 quantities=0, below/equal/above min, missing min, missing qty, khác unit, service/nonstock, scope restricted.
9. Upload image/type spoof/oversize, Drive permission revoked, upload success/Sheet fail, retry; reconciliation không mất file hay báo success giả.
10. DOCX/PDF version pair đúng, missing counterpart, viewer unauthorized file, backend stream headers/content.
11. Android chụp thật/chọn file/từ chối camera/mất mạng/revoke/session/version conflict; iOS cùng case và evidence riêng.
12. Quota429/backoff/timeouts/cache invalidation, Google offline, administrative maintenance, no 2 writers.
13. Backup/restore trên dev khác đích; kiểm code/counter/scope/files/meta; không khôi phục ngược vào dữ liệu production để thử.

## Chi tiết TASK-005 — `ACCEPTED`, local pass; live chưa chạy

Fixture dưới đây hoàn toàn giả, không mô tả cơ cấu hoặc tài khoản Công ty Duy
Tiến. Password/token được sinh trong test runtime, không ghi vào tài liệu.

| Nhóm | Fixture/case chính | Expected |
|---|---|---|
| Scope | `OU-DEMO-A/A-CHILD/B`, `LOC-DEMO-A/A-CHILD/B` có `ParentId`, `CAT-DEMO-EQUIPMENT/CHILD/VEHICLE` | Validate ParentId/no-cycle; exact, descendant và sibling được phân biệt; thiếu scope fail-closed |
| Role | Viewer, AssetEditor, CatalogManager, DocumentManager, Admin | Permission đúng [ma trận](../01-requirements/PERMISSIONS.md); Viewer không write; editor không quản lý user |
| Nhiều role | AssetEditor scope A + DocumentManager scope B | Permission union nhưng không ghép AssetEditor với scope B |
| Bootstrap | System store trống, chạy bootstrap hai lần và restart | Tạo `admin` một lần; lần sau no-op; hash/role/scope không reset |
| Password | Chuỗi 7/8 ký tự, chuỗi đơn giản/username, first login, reset, change, 5 lần sai | Chỉ min 8; không complexity/max/blocklist/username rule; must-change hạn chế quyền; reset/change revoke; lần 5 khóa 15 phút |
| Web session | Cookie mặc định/remember-me, CSRF, idle/absolute expiry | Flags đúng; state-changing request thiếu token bị chặn; expiry đúng proposal |
| Mobile session | Access 15 phút, refresh rotate, reuse token cũ | Reuse revoke family; secure token không xuất hiện log/response sai |
| Revoke | Lock user, đổi role/scope, logout/logout-all | Cookie/access/refresh cũ mất hiệu lực qua `SecurityVersion` |
| Last admin | Assignment active/revoked/chưa hiệu lực/hết hạn và ALL đủ/thiếu | Chỉ assignment Admin hiệu lực có đủ ba ALL mới được tính; chặn mutation làm admin về 0 |
| Writer lease | Web/CLI và hai CLI dùng cùng lock file | Chỉ một process giữ lease; lock RAM không được dùng làm bằng chứng liên tiến trình |
| Scope leak | Detail/file ngoài scope; list/report/export trộn A/B | Ngoài scope 404; list/count/aggregate chỉ gồm row được phép |
| Razor Pages | Login, change password, users, authorization, security events | Validation/empty/access denied; dùng cùng service với API |

Local unit/integration dùng fake system store và HTTP stub; 57/57 test pass tại
[TEST-REPORT-TASK-005](TEST-REPORT-TASK-005.md). Trước khi TASK-005 `DONE`, phải
có test live trên system spreadsheet dev được cấp quyền qua provisioning/Picker;
lượt local này không tạo hoặc ghi Google.

## Fixture và evidence
Unit tests dùng dữ liệu giả; integration fake adapter có fault injection không thay live Google test. Dữ liệu nguồn đã cung cấp dùng kiểm import trong phạm vi task, không upload lên dịch vụ khác ngoài đích đã xác lập.
Report Task ghi build/test counts và platform/device/sdk/schema/source hash. Mã hoạt động mà chưa có evidence cần IN_REVIEW, không DONE.
UAT bản demo bởi người dùng: login, tree, asset lifecycle, ảnh, template, reports và phạm vi thực. Từng bước có expected/actual và lỗi còn lại.
