> Cập nhật triển khai15/10: nguồn HTML đã nhận đúng bytes/SHA256 và đọc tĩnh ở TASK-016; [inventory](../02-architecture/WEB-UI-INVENTORY-TASK-016.md). Người dùng cho phép thực hiện liên tục016→021; task trước đủ local nhưng thiếu browser/Google/network được tiếp tục với ngoại lệ ghi rõ và giữ IN_REVIEW. Không commit/push/deploy/mobile/Google resources/real writes. Các mô tả chỉ lập kế hoạch/thiếu nguồn dưới đây thuộc checkpoint trước.

# PLAN-003 — Hoàn thiện trải nghiệm web Duy Tiến Assets

- Status: IN_REVIEW — TASK-016 DONE tài liệu; TASK-017–021 đã triển khai/kiểm local, giữ IN_REVIEW cho browser/Google/mạng thật.
- Ngày: 2026-10-15. Requirement: [REQ-003](../01-requirements/REQ-003-web-experience.md).
- Thiết kế: [WEB-UI-DESIGN](WEB-UI-DESIGN.md). Kế hoạch tổng: [PLAN-001](PLAN-001.md).
- Workflow chính: context-driven-development áp dụng vào docs hiện có, không tạo conductor/ song song; design-system-patterns cho tokens/components. Không subagent, không commit/push theo chỉ đạo.

## Đầu vào và giới hạn

Giữ code/tài liệu TASK-015 v2 và các thay đổi chưa commit. Người dùng xác nhận login thành công bằng tài khoản hiện có: PASS_MANUAL cho case login, không là AI Google auth-read/consent/schema/phiên/reconnect/freshness pass. Giữ TASK-003 IN_REVIEW,004 DONE,005–012/014/015 IN_REVIEW và gate cũ; Android tạm dừng, iOS DEFERRED_BY_USER. Không đòi đóng mobile để hoàn thiện web.

Lịch sử lúc lập kế hoạch (đã giải quyết nguồn ở TASK-016): HTML đúng tên chưa có trong workspace/attachments. Phần quy chuẩn hiện là DRAFT dựa trên chỉ đạo và màu người dùng, không phải đã đọc HTML. Cho phép lập kế hoạch và kiểm kê source độc lập; TASK-016 không hoàn tất gate nguồn khi thiếu file.

## Thiết kế tối thiểu và cách chia

Tái sử dụng Razor layout/partials, Bootstrap/JS local, DTO/services/auth/cache hiện tại. Tách shared shell trước các vertical flows. Mỗi task ghi scope, ngoài scope, screens, permission, success/error/empty/loading và bằng chứng. Menu/nội dung mới không là permission grant; không đổi enum, ID/mã/history/version/idempotency/schema, metadata guard, single-writer, password/session hoặc Google drive.file. Không lấy UI đẹp làm bằng chứng nguồn đã được duyệt.

| Thứ tự | Task | Đầu ra | Phụ thuộc thực hiện | Điều kiện chính |
|---|---|---|---|---|
| 1 | [TASK-016](../06-tasks/TASK-016.md) | Inventory route/field/action, gap backend, đối chiếu HTML và quy chuẩn | Source/web hiện tại; cần đúng file tham chiếu để chốt | Không sót module hoặc giả đã đọc nguồn; tiêu chí UI đo được |
| 2 | [TASK-017](../06-tasks/TASK-017.md) | Layout/header/sidebar/breadcrumb/components + account/offline shell | TASK-016 | Nhận diện/điều hướng nhất quán, login hiện có không hồi quy |
| 3 | [TASK-018](../06-tasks/TASK-018.md) | Cây và Assets: browse/detail/forms/preview/commit/bulk hiện có | TASK-017; services005–008/014 | Scope/guard/ID/version/key giữ; context/validation/conflict rõ |
| 4 | [TASK-019](../06-tasks/TASK-019.md) | Media/Documents/Items/import/snapshot/R01–R04 | TASK-018; services009–011 | Upload/reconcile/bytes/units/asOf/UNKNOWN/export đúng contract |
| 5 | [TASK-020](../06-tasks/TASK-020.md) | Admin/home/help/privacy và content sweep toàn web | TASK-019; core005 | User/role/scope/audit và nội dung công việc; không KPI/nút giả |
| 6 | [TASK-021](../06-tasks/TASK-021.md) | Kiểm trải nghiệm/online-offline/hiệu năng, fixes trong scope và tổng kết | TASK-020; trạng thái015v2 | Bằng chứng browser/Google/mạng/fixture tách riêng; gate thiếu giữ mở |

TASK-014 tiếp tục là nơi tổng kết nghiệm thu/vận hành web hiện có; TASK-015 giữ hành vi online/offline. Task mới bổ sung UX, không thay DONE/gate của hai task đó. Checkpoint lập kế hoạch trước đây chỉ tạo tài liệu. Chỉ đạo mới cho phép liên tục016→021 đã thực hiện local; không tự cấp quyền Google live. Khi thực hiện task sau, đầu ra task trước phải đủ để phụ thuộc; mọi ngoại lệ thực hiện phải ghi cụ thể, không đổi trạng thái để vượt phụ thuộc.

## Rủi ro và xử lý

- Thiếu HTML: ghi MISSING_REFERENCE, không suy bố cục từ mô tả thành đã đối chiếu. Không đưa HTML chứa dataset công ty vào Git.
- Bỏ chữ demo làm mất provenance: tách branding ứng dụng và badge dữ liệu; phát hành kỹ thuật không là phê duyệt nguồn.
- Cây/thống kê lớn hoặc component tự gọi Google: reuse server queries, paging/index đã có, request auth memo; đo call count trước/sau, không tắt pacing hoặc scope.
- UI giữ state làm lộ user cũ: key theo user/workspace/authority, clear theo logout/revoke; không localStorage toàn bộ rows.
- Dirty form/lỗi mạng: giữ payload/version/key, trạng thái UNKNOWN và đối soát; không auto queue/resend hoặc ép reload mất nội dung.
- Thiếu backend: ghi gap rõ; chỉ sửa integration đã được duyệt, không button giả hoặc tự chốt rule. Source/rule/ngưỡng vẫn OPEN/TBD.
- Thay shared layout làm hỏng offline/cache/auth: regression và coverage riêng; không coi HTML cũ trong cache là nguồn hiện hành.

## Kiểm tra và điều kiện kết thúc

Lượt lập kế hoạch: chỉ kiểm ID duy nhất/không overwrite, links, dependencies không cycle, đủ trường task/AC, consistency statuses/manual evidence và diff/fingerprint bảo toàn code. Build/test/browser/Google **NOT_APPLICABLE** cho thay đổi docs, không là các gate sản phẩm PASS.

Khi triển khai: build/filtered tests phù hợp, compiled Razor/HTTP và browser nếu provider có; không lặp full UAT TASK-005/006 hoặc timeout dài. TASK-021 ghi cold/warm startup/auth/Google calls/pacing/coordinator/deserialize/render và online/offline riêng trên cùng môi trường. Case không chạy giữ NOT_RUN/BLOCKED đúng nguyên nhân. Không yêu cầu người dùng chạy chuỗi UAT cũ.

Một task chỉ DONE khi AC bắt buộc và bằng chứng tương ứng đủ. Nếu browser/real network/Google còn thiếu thì giữ IN_REVIEW hoặc BLOCKED theo phụ thuộc, không dùng build/fake làm bằng chứng thay thế. Tổng kết web rõ trước bất kỳ quyết định tiếp tục Android; iOS tiếp tục hoãn.


## Kết quả thực hiện liên tục15/10

Hoàn tất phần local PLAN-003: shell/header xanh/sidebar/breadcrumb và tài nguyên local; giao diện danh mục/tài sản/ảnh/hồ sơ/import/vật tư/số lượng/báo cáo/quản trị, hướng dẫn online mặc định/ngoại tuyến chỉ đọc. TASK-016 DONE tài liệu; TASK-017–021 IN_REVIEW do browser/Google/mạng thật chưa đủ. Không mở task/mobile khác.

TASK-021:137/137 integration passed exit0 (17s), Node5/5 exit0, benchmark Connected/Razor665nút/20744Items1/1 exit0 (26s);9/9 copy-import+baseline check exit0 (1s), có overlap không cộng thành tổng unique. Release build exit0/0warnings/errors5.47s. Một backend PID25412, Google/Development/Connected; strictHTTPS public login200 và tài nguyên local có fingerprint200, không AI login/Google business pass. [Báo cáo tổng hợp](../07-testing/TEST-REPORT-TASK-021.md). Ngoại lệ017→021 đã áp dụng sau checkpoint local của từng bước, không đổi DONE để vượt browser/Google. Backend thiếu contract (KPI/bulk thêm/cancel import) giữ gap, không nút giả; dữ liệu nguồn OPEN/TBD.


## Runtime16/10 — cập nhật tiến độ, không thêm feature/plan

Giữ016DONE/017–021IN_REVIEW. Login trung tâm hiện có được user xác nhận, Home/Danh mục được AI quan sát browser; Google auth/HTTP timing thật đã có nhưng browser dừng URL-confidence ở Assets. Sửa literal cấp F/breadcrumb, cache UI revision/legacy và Google auth read capability; public static manifest không gây Google auth thừa (actual14GET/0calls sau sửa).101/101localpass/exit0, Release0warnings/errors; backend một instance PID13680. Main Google GET vẫn nhiều giây, chưa có warm p95/page before-after/browser/cache/network đầy đủ. [Report021](../07-testing/TEST-REPORT-TASK-021.md). Không tắt pacing/guard, ghi nghiệp vụ trung tâm, reset admin, mở lại gate hoãn hoặc tiếp mobile; không đổi trạng thái để vượt AC.


## TASK-02116/10 — hiệu năng source reads

Tiếp đúng021, không task/feature mới. Fresh business values Asset+Category gom cùng spreadsheet, auth riêng; detail render chung snapshot. Read guards/coordinator/pacing giữ. Google CLI A/B3cặp Assets4→3GET/median5.00→3.77s, Categories3→3;166/166+25/25+Node11/11exit0. Chỉ CLI/no web session/touch; browser stop được tôn trọng, chưa page p95/network/runtime UAT. Giữ021IN_REVIEW và mọi gate/task trước. [Request map](../07-testing/TASK-021-REQUEST-MAP.md), [report](../07-testing/TEST-REPORT-TASK-021.md). Không mobile/commit/push/deploy.
