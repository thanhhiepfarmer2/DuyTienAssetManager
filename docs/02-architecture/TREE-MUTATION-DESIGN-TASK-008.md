# TASK-008 — Thiết kế thay đổi cây nháp và bulk Asset local

> Cập nhật14/10/2026: hạn chếguardhiện diệnregistry trongthiết kế13/10 bên dưới là lịch sử; [TASK-014 pha Web](WEB-COMPLETION-DESIGN-TASK-014.md) bổsungfullschema/owner/journal/scope/CAS4registryvàjointbatch, khôngbypassguard. Googlelive/schema/sourcegatecũ chưaPASS.

Ngày: 2026-10-13. Implementation local; TASK-008 IN_REVIEW. Nguồn: REQ-001 FR-02/FR-03/FR-04, AC-06/AC-07, PERMISSIONS và ngoại lệ phụ thuộc do người dùng cho phép. Không thay ADR-001, auth/password/session policy hoặc rule nghiệp vụ công ty.

## Trạng thái và phạm vi

Publish ở đây là đổi active pointer sang một snapshot DRAFT đầy đủ trong business spreadsheet. Không phê duyệt cây/mã công ty. Node hoạt động DRAFT, ngừng dùng DRAFT_INACTIVE; TreeVersions luôn DRAFT, Asset vẫn DRAFT_DEMO. Import workbook TASK-006 tiếp tục kiểm nguồn F1/F2/F3; validator tree lưu trữ cho phép cây sau move tới F10. Không thêm endpoint tạo nút sâu hoặc phê duyệt dữ liệu.

Rule prefix/CCC/required fields, nguồn OrgUnits/Locations và workbook gốc vẫn OPEN/TBD/chưa xác minh. Validation mã đoạn 3 ký tự theo subset demo sẵn có không là rule cấp ObjectCode chính thức. Đổi mã root không tự sửa CodeRules/counter: create mới có thể bị chặn khi rule không còn match; các mã đã cấp được giữ.

Task này xử lý Categories + Assets đã có trong TASK-007. Không triển khai Items/Documents/Files/Stock hoặc TASK-009. Adapter từ chối business spreadsheet có tab Items/Documents/DocumentVersions/StockBalances/Files vì chưa thể đối soát tác động các module đó; không báo chúng rỗng. Preview không xuất count giả của module chưa triển khai.

## Các thao tác

| Kind | Tác động / điều kiện |
|---|---|
| EDIT_CATEGORY | Sửa tên/mã nháp/definition; giữ CategoryId và provenance. Tính lại full code của descendants. Code đã thuộc node khác trong bất kỳ version nào không tái dùng |
| MOVE_CATEGORY | Đổi cha, kéo toàn bộ descendants gồm node ngừng dùng; chặn self/cycle/thiếu cha/F11. Chuyển thành root cần grant Category ALL. Tính lại path/level/full code; AssetId/ObjectCode không đổi |
| ARCHIVE_CATEGORY + EMPTY_ONLY | Chỉ node không có con và không có Asset, kể cả Asset đã xóa mềm. Không hard delete dữ liệu |
| ARCHIVE_CATEGORY + RELOCATE | Đích DRAFT nằm ngoài nhánh; chuyển tất cả Asset liên quan sang đích, giữ trạng thái xóa, rồi ngừng cả nhánh |
| ARCHIVE_CATEGORY + SOFT_DELETE | Xóa mềm Asset hoạt động, giữ Asset đã xóa và lịch sử/counter/mã; ngừng cả nhánh |
| MOVE_ASSETS | 1–100 AssetId duy nhất, danh mục đích bắt buộc; Org/Location trống giữ từng giá trị cũ, chọn mới phải đúng relationship và scope |

Lý do bắt buộc (tối đa500 ký tự), preview toàn bộ tác động và confirmImpact=true trước commit. Không có thay đổi thì không tạo tree version/operation mới. Giữ alias Asset/IssuedCCC/CodeRuleId/counter; history Categories là mapping code/path theo ID bất biến. SourceKey/SourceSTT/row là provenance, không được tính lại thành ID hoặc STT cấu trúc hiện tại.

Asset đi cùng category move giữ CategoryId/Version nếu không thay fields; phân loại/path đọc theo active tree mới. Bulk đổi scope hoặc soft-delete tăng Version từng Asset bị thay đổi, append AssetOperations MOVE/DELETE và audit. UI thường chỉ hiển thị DRAFT và Asset có scope hợp lệ. Asset đã xóa dưới nhánh ngừng dùng chỉ được đưa vào plan khi active Admin/ALL hợp lệ; không mở thêm màn phục hồi ngoài task. Restore thường trên nhánh ngừng dùng bị chặn, cần plan hợp lệ.

## Authorization

Tái sử dụng ScopeAuthorizationService TASK-005, current user active/không MustChangePassword và assignment đang hiệu lực. Chặn Viewer ở server; role UI không thay scope. Category actions cần permission tương ứng và READ mọi node ảnh hưởng. Nếu thiếu quyền đọc/chuyển bất kỳ node/Asset nào, cả preview bị từ chối trước count, không lộ tổng hay tên ngoài scope.

MOVE kiểm nguồn và đích trong cùng một assignment có permission, với cả hierarchy trước và sau move. Asset cần đủ OrgUnit+Location+Category và ASSETS_MOVE/UPDATE hai phía; category manager riêng không được chuyển nhánh chứa Asset nếu thiếu quyền Asset. Không ghép permission role A với scope role B. Path che ancestor không được READ. History kiểm current scope và từng old tree; không lộ path lịch sử ngoài quyền.

## Preview, concurrency và retry

Preview stateless trả treeRevision, assetRevision, previewHash, treeVersionId, node diff, Asset tác động/count và issues/CanCommit. Digest gắn actor, normalized request và snapshot toàn bộ Categories/Assets/references/rules/aliases/counters. Không là bearer token hay cache cấp quyền. Commit nhận lại đầy đủ request, hai revision, digest, confirmImpact và Idempotency-Key; server đọc mới và recheck quyền/digest trong shared writer.

Stale revision, scope/reference/data đổi sau preview hoặc key đổi payload trả409, không ghi đè. Không dùng stdin hoặc preview RAM cho TASK-008. TreeChanges lưu key theo actor, request/result và links; reconstruct service/adapter có thể retry cùng yêu cầu đã commit. Retry recheck permission hiện tại trước trả result, không ghi operation/counter lần hai.

Import TASK-006 dùng asset store ở production: chặn thay/mất/state/chuỗi ancestor ID của category có Asset để không dùng import bỏ qua plan bulk. Giữ SourceKey và historical code mapping để reimport không sinh ID mới sau sửa mã thủ công; import không tự kích hoạt lại DRAFT_INACTIVE.

## Atomic Sheets và single-writer

GoogleTreeMutationStore chỉ dùng business spreadsheet khác system, executor OAuth Desktop với drive.file, process lease và shared single writer hiện có. Category/Asset read services cũng đi qua shared writer để tránh metadata+values hoặc hai snapshot bị trộn với write trong process. Không có SQL/SQLite hay fake adapter trong DI production.

Category/Asset adapters prepare requests rồi joint store gửi đúng một spreadsheets.batchUpdate chứa complete Category snapshot, TreeVersions, pointer, tree journal/detail/audit và mọi Asset mutation/journal/audit cần thiết. Không publish PARTIAL. Với MOVE_ASSETS không đổi cây, treeVersionId giữ nguyên nhưng Category revision/tree ledger tăng. Nếu Asset không thay đổi, không ghi bảng Asset.

Lost write response đọc đối soát cả hai revision/pointer/ledger; chỉ báo thành công khi thấy commit toàn bộ. Nếu read reconcile lỗi, trả503 an toàn và retry cùng key sau kết nối/reconstruction. Không blind retry non-idempotent append. Kiểm header/schema/journal/digest/row/cell/batch trước write; dữ liệu chưa xác định không overwrite. Guard bảng module chưa hỗ trợ giữ ở cả read và commit.

Giới hạn kỹ thuật subset: cây1–1000 node, F1–F10, Category table tối đa20000 dòng, Asset table5000 dòng, ô45000 ký tự, atomic batch1800KiB. Diff lớn lưu mỗi node một dòng TreeChangeDetails; không nhồi hàng trăm node vào một ô audit. Vượt giới hạn bị chặn trước ghi, chưa có generalized chunk/resume hay scale-out.

## Backend và Razor

POST /api/tree-changes/preview; POST /api/tree-changes/commit; GET /api/categories/{id}/history. API bearer-only/no-store, cookie403; validation400, permission403, ngoài scope404, stale/key409, upstream503. DTO không cho JSON field lạ/approved flag hoặc nhận sửa AssetId/ObjectCode.

`/Categories/Manage/{id?}` cho Admin/CatalogManager. `/Assets/Move/{id?}` cho Admin/AssetEditor. Form cùng service API, CSRF, reason/impact/confirm và banner DRAFT. Category detail có history theo scope; list/tree/TXT và Asset search/filter theo snapshot mới. Lựa chọn chỉ trong scope; khi đọc lỗi giữ reason/key/ID/đích, báo lỗi an toàn. Commit đã xác nhận nhưng refresh lỗi vẫn nói rõ commit thành công và màn mới chưa tải, không giả rollback. JavaScript chỉ trạng thái loading/ẩn field theo kind; POST hoạt động không phụ thuộc JS.

## Bằng chứng và gate

[TEST-REPORT-TASK-008](../07-testing/TEST-REPORT-TASK-008.md) ghi unit/fake persistence, Google HTTP stub và Kestrel/compiled Razor HTTP. Fixture665-node hoàn toàn tổng hợp, không chứng minh workbook công ty hoặc baseline15/98/552/39. HTTP render/node syntax không là browser UAT. Google live/browser/source/rule chưa đủ, TASK-008 giữ IN_REVIEW. Không tự tiếp tục task sau hoặc yêu cầu thao tác UAT/secret.
