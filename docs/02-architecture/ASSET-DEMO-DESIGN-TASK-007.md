# Thiết kế lát local TASK-007 — máy và mã giả

Ngày: 2026-10-13. Status: IMPLEMENTED_LOCAL_FIXTURE_ONLY; không phải phê duyệt rule mã/trường nghiệp vụ công ty.
Nguồn: [REQ-001 FR-04/AC-05/06/08](../01-requirements/REQ-001-asset-digitization.md), [PLAN-001](PLAN-001.md), [TASK-007](../06-tasks/TASK-007.md) và chỉ đạo người dùng 2026-10-13 cho phép local dù TASK-006 IN_REVIEW.

## Phạm vi và dữ liệu

Một loại `MACHINE`, Asset luôn `DRAFT_DEMO`; danh mục đang hiệu lực demo vẫn `DRAFT`. Không publish, không chuyển nháp thành dữ liệu phê duyệt, không nhập nhân sự/Items/BOM thành Assets. Technical required để kiểm scope: tên/loại/CategoryId/OrgUnitId/LocationId; tình trạng mặc định UNKNOWN (không là số lượng). Serial/model/hãng/người quản lý/ngày ghi nhận/ghi chú/alias tùy chọn ở fixture. Required nghiệp vụ từng loại chưa được người có thẩm quyền xác lập.

Các test dùng cây/đơn vị/địa điểm hoàn toàn giả, prefix TEST và nhóm TST với mã mẫu TEST-TST-000001. Không dùng GM như rule đã duyệt. Runtime không tự seed rule/reference/tài sản hoặc init khi startup. Chỉ đúng một CodeRule `Status=DEMO`, `ApprovedForDemo=true`, CCC khớp root danh mục hiện tại mới được cấp mã. Cờ true trong fixture chỉ phục vụ test; không chứng minh có phê duyệt công ty. CodeRules/OrgUnits/Locations đọc từ business Sheets; thiếu nguồn/rule thì từ chối create, không dựng dữ liệu thay thế.

## Nghiệp vụ và quyền

- AssetId opaque; ObjectCode/IssuedCCC/CodeRuleId/Created metadata bất biến. Counter theo prefix+CCC (không theo row, không reset khi đổi RuleId). Format kỹ thuật prefix-CCC-sáu chữ số; hết 999999 dừng, không quay vòng. Alias/mã đã dùng kể cả xóa mềm không tái cấp.
- GET list/detail/search/paging lọc đủ OrgUnit+Location+Category trong cùng assignment trước count; cùng ScopeAuthorizationService TASK-005. Không bypass ALL cho ID/reference thiếu/ngừng dùng. Không lộ ancestor/path ngoài scope qua choice/category label.
- UPDATE thay đổi phạm vi bằng form cùng record phải có ASSETS_UPDATE và ASSETS_MOVE match nguồn+đích trong cùng assignment. Không thêm endpoint move-preview/move riêng của TASK-008; không làm cây/bulk action.
- Alias namespace DEMO do người dùng nhập cho demo, giữ text/zero đầu. Một mã chỉ mapping một tài sản, kể cả alias đã ngừng dùng; loại khỏi form active vẫn giữ lịch sử và tìm được. Không xác nhận alias công ty.
- Mỗi write cần Idempotency-Key gắn actor + hash action/id/payload/expectedVersion/tree. Retry cùng key đọc ledger trước version check, không thêm entity/counter/operation; payload khác trả409. Retry cũng recheck quyền/scope hiện tại.
- Sửa/delete/restore dùng expectedVersion; stale409 không overwrite. Form scope chọn từ cùng snapshot DRAFT, stale tree409. API DTO không nhận sửa ID/ObjectCode/DataState; unknown JSON members bị400.
- Xóa mềm giữ ID/mã/alias; restore dùng cùng ID/mã và tăng Version. Trạng thái/counter/alias/ledger/audit atomic trong một business spreadsheet, cùng writer và process lease hiện có. Auth password/session không đổi.

## Persistence và giới hạn

[GoogleAssetCatalogStore](../../src/DuyTien.AssetManagement.Infrastructure.Google/Adapters/GoogleAssetCatalogStore.cs) dùng executor/OAuth drive.file hiện có. Sáu bảng ASSET_DEMO_V1 được tạo chỉ trong explicit first write nếu tất cả vắng: AssetSchemaVersions, Assets, IdCounters, LegacyAliases, AssetOperations, AssetAuditEvents. Ba bảng reference chỉ đọc: CodeRules, OrgUnits, Locations; adapter không seed hoặc ghi chúng. Headers trong code là contract local, không tự overwrite schema lạ.

Asset journal/audit riêng để không đổi CATEGORY_V1/Operations/AuditEvents đã có của TASK-006. Counter/asset/alias/ledger/audit cùng atomic batchUpdate; timeout/lost response đối soát operation, không retry addSheet/allocation mù. Khôi phục adapter/service trong test chứng minh đọc lại từ HTTP stub, chưa là restart backend trên Google thật.

Header/range/partial schema, null/malformed JSON, counter/entity/alias/audit khác journal đều fail closed. Asset/counter/alias row sort được đọc lại theo ID, không dựa vị trí dòng; thứ tự journal không được tự thay. Trước ghi chặn bảng trên5000 dòng, ô trên45000 ký tự và serialized batch trên1800KiB; không cắt alias/histories hoặc ghi PARTIAL. Đây là giới hạn kỹ thuật lát demo, chưa là chứng nhận throughput/Google quota/limit thực. Không có SQL/SQLite, adapter RAM chỉ ở tests.

## API/web và bằng chứng

GET /api/assets (q/categoryId/includeDescendants/condition/orgUnitId/locationId/custodianRef/includeDeleted/page/pageSize), /{id}, /form-choices?editing=true; POST /api/assets, PATCH/DELETE /{id}, POST /{id}/restore. API yêu cầu bearer và no-store; cookie dùng Razor /Assets, /Assets/Create, /Assets/Detail/{id} với antiforgery. PATCH dùng toàn bộ AssetInput qua form DTO, không phải JSON Patch. pageSize1–100; list không trả tổng ngoài scope.

Validation API422 (binder JSON400), quyền403, resource ngoài scope404, stale/key409, Google503 an toàn. Razor validation binding400/service422, 409 giữ input và link reload, lỗi upstream giữ yêu cầu/key và không báo lưu thành công. Role ẩn form chỉ là UX; service kiểm quyền độc lập. Loading có aria-busy/status và disable submit; có fallback không JS. Layout Bootstrap responsive, chưa kiểm browser/layout thật.

Local 54/54 test task +9/9 regression, build sạch exit0. [Test Report](../07-testing/TEST-REPORT-TASK-007.md) phân biệt fake Google/HTTP-rendered Razor với live/browser. Gate nguồn/rule nghiệp vụ, Google live/restart/quota và browser/UAT chưa đạt; TASK-007 IN_REVIEW.
