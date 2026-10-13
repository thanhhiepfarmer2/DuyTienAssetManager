# TASK-010 — Import Items và snapshot số lượng

- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-006; nguồn stock snapshot và ngưỡng
- AC liên quan: AC-11, AC-12

## Phạm vi
Mapping 20.744 rows thành Items qua chunk/preview; resolve CĐKEM có decision; số lượng snapshot riêng UNKNOWN. Không tạo một Asset cho mỗi Item.

## Acceptance criteria của Task
- [ ] Hai cột Mô tả không ghi đè nhau; mã cũ đủ số 0/format
- [ ] Không coi BOM/kho ngầm định là tồn; snapshot input có asOf/unit/source
- [ ] Partial import/retry không silent duplicate; default stock UNKNOWN

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Bằng chứng kiểm tra — AI chạy/quan sát trực tiếp 2026-10-13

Build server cuối exit0,0warnings/errors (.NET10.0.400); suite TASK-01066/66 exit0,0failed/skipped; sau thay UI mapping/quyết định mã trùng/địa điểm kiểm riêng InventoryHttpRazorTests8/8 exit0, không đổi core/Google. Hồi quy Category/scope/tree guard46/46 exit0. Node syntax/diff check exit0; format4project và bổ sung paths file rõ exit0. [Test Report](../07-testing/TEST-REPORT-TASK-010.md), [evidence](../07-testing/TASK-010-LOCAL-EVIDENCE.json), [thiết kế](../02-architecture/INVENTORY-DESIGN-TASK-010.md); lệnh COMMANDS.

## Phần local đã triển khai

- Reader XLSX/JSON20ordinal, text/custom zero-mask giữ mã, hai mô tả riêng, conversion/commercial/BOM raw reference, validation/fingerprint/unsafe input.
- Preview/diff, mapping Category DRAFT/tracking nullable, MERGE đồng nhất/SPLIT identity tường minh có reason/audit, quarantine không global. Không Item→Asset/ObjectCode hoặc BOM/kho→stock.
- Staging durable/VALIDATED, byte-budget chunk100max, PARTIAL/COMPLETE totals, immutable identity/Version/key/progress/tree conflicts, retry/resume/reconcile sau restart. Fake synthetic20744rows nhập đủ, không là nguồn công ty.
- Snapshot nullable UNKNOWN/OUT/LOW/AVAILABLE, asOfUTC/source/actor/unit exact/min nullable, server scope ba chiều cùng assignment, no conversion/accounting ledger.
- Backend/Google inventory adapter8tab atomic delta/journal/audit/replay, scoped API bearer và Razor list/detail/search/paging, nhóm nguồn/danh mục/tracking và mã trùng bằng lựa chọn trực tiếp, snapshot/tiến độ/CSRF/error/retention/empty/loading.
- Guard metadata TASK-008 nguyên vẹn; thêm bảo vệ category full import có Item, không đổi ancestors/state/mất category để đi vòng guard. Thứ tự chunk lấy journal, không rows materialized sau sort.

| AC | Local actual | Gate còn mở |
|---|---|---|
| Hai Mô tả/mã giữ leading0/format | PASS_LOCAL JSON/XLSX fixture20cols; format chưa hỗ trợ bị từ chối | Workbook gốc/header/format/hash NOT_VERIFIED_SOURCE_ORIGINAL |
| Không lấy BOM/kho làm tồn; snapshot asOf/unit/source | PASS_LOCAL memory/stub/API/HTTP Razor | Nguồn stock/ngưỡng/reference OPEN/TBD, Google/browser NOT_RUN |
| Partial/retry không duplicate; default UNKNOWN | PASS_LOCAL synthetic20744/chunk/restart/staging/lost-response/sort/replay | Google quota/throughput/restart thật và browser chưa chạy |

Các checkbox nghiệm thu tổng thể chưa đánh dấu vì gate thật chưa đủ; TASK-010 IN_REVIEW, không DONE. TASK-003 IN_REVIEW, TASK-004 DONE; TASK-005–009 IN_REVIEW, DEFERRED_BY_USER/STOPPED_BY_USER và lịch sử giữ. TASK-011–014 TODO; không tự bắt đầu task sau. Không Google thật/dữ liệu thật/UAT/secret mới/deploy. Phần TASK-010 chưa commit theo chỉ đạo.

Self-review theo dotnet-backend-patterns và ai-debt-detector/session-guard đã đọc: rà failure/null/unsafe ZIP/stream cleanup/version/identity/scope/order/bounds, sửa các lỗi report; không independent reviewer/subagent hoặc tuyên bố audit bảo mật toàn diện. Ngoại lệ người dùng ưu tiên hoàn tất local, không dừng tự động chỉ vì số tool calls.

## Ngoại lệ phụ thuộc — 2026-10-13

Người dùng cho phép phần local TASK-010 khi TASK-005–009 IN_REVIEW; không đổi gate/historical evidence, DEFERRED_BY_USER/STOPPED_BY_USER giữ. Checkpoint trước code đã commit/push origin/main 463896d82629847ebd41866eb3e0cb97becfc2f2 (exit0, remote hash khớp); 88 file TASK-007–009, diff/staged check exit0. Không chạy lại build/test cho checkpoint. Quyền Git chỉ checkpoint trước TASK-010; phần mới để chưa commit.

Không Google live/UAT thủ công/secret mới/dữ liệu thật/deploy/TASK-011. Guard metadata TASK-008 giữ nguyên: Items/StockBalances/Files/Documents registry chưa được đối soát thì tree mutation fail-closed. TASK-010 không cần tree mutation. Workbook gốc/nguồn stock/rule/ngưỡng/Org-Location còn OPEN/TBD, fixture giả chỉ bằng chứng local. .NET backend patterns đã đọc, tái sử dụng service/DI/async/DTO và kiểm thử xUnit; không EF/SQL/subagent.
