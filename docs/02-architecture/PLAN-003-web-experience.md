# PLAN-003 — Hoàn thiện trải nghiệm web Duy Tiến Assets

- Status: READY cho thực hiện tuần tự ở lượt sau; TASK-016–021 đều TODO, chưa code UI.
- Ngày: 2026-10-15. Requirement: [REQ-003](../01-requirements/REQ-003-web-experience.md).
- Thiết kế: [WEB-UI-DESIGN](WEB-UI-DESIGN.md). Kế hoạch tổng: [PLAN-001](PLAN-001.md).
- Workflow chính: context-driven-development áp dụng vào docs hiện có, không tạo conductor/ song song; design-system-patterns cho tokens/components. Không subagent, không commit/push theo chỉ đạo.

## Đầu vào và giới hạn

Giữ code/tài liệu TASK-015 v2 và các thay đổi chưa commit. Người dùng xác nhận login thành công bằng tài khoản hiện có: PASS_MANUAL cho case login, không là AI Google auth-read/consent/schema/phiên/reconnect/freshness pass. Giữ TASK-003 IN_REVIEW,004 DONE,005–012/014/015 IN_REVIEW và gate cũ; Android tạm dừng, iOS DEFERRED_BY_USER. Không đòi đóng mobile để hoàn thiện web.

HTML đúng tên chưa có trong workspace/attachments. Phần quy chuẩn hiện là DRAFT dựa trên chỉ đạo và màu người dùng, không phải đã đọc HTML. Cho phép lập kế hoạch và kiểm kê source độc lập; TASK-016 không hoàn tất gate nguồn khi thiếu file.

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

TASK-014 tiếp tục là nơi tổng kết nghiệm thu/vận hành web hiện có; TASK-015 giữ hành vi online/offline. Task mới bổ sung UX, không thay DONE/gate của hai task đó. Lượt này chỉ lập kế hoạch, không bắt đầu TASK-016 hoặc tự cấp quyền chạy Google live. Khi thực hiện task sau, đầu ra task trước phải đủ để phụ thuộc; mọi ngoại lệ thực hiện phải ghi cụ thể, không đổi trạng thái để vượt phụ thuộc.

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
