# TASK-011 — Báo cáo tài sản và tồn hết/sắp hết

- Status: IN_REVIEW
- Type: FEATURE
- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Plan: [PLAN-001](../02-architecture/PLAN-001.md)
- Created: 2026-10-06
- Phụ thuộc/gate: TASK-007,010
- AC liên quan: AC-04, AC-12

## Phạm vi
Triển khai R01-R04, scoped filters/tree aggregation, asOf/units, paging. Chỉ thêm report PROPOSED khi được chọn.

## Acceptance criteria của Task
- [ ] 0/low/UNKNOWN/non-stock item phân biệt
- [ ] Không cộng khác đơn vị, không lộ tổng ngoài scope
- [ ] R01/R02 đếm Assets khác Item count; UAT filter

## Routing
Chọn skill thực tế sau khi đọc AGENTS và danh sách phiên. .NET/backend/database cho server; ui-design khi có UI; skill frontend/mobile chỉ khi phù hợp stack. Không coi tên plugin là lệnh; không bắt 14 plugin cùng chạy. Chỉ dùng một workflow điều phối chính.

## Phần local đã thực hiện

- Application read-only R01/R02 Assets riêng, filters/type/condition/org/location/category-descendants, tree direct/subtree/cấpF và unique count không double-count.
- R03/R04 snapshots0/low, quality sections UNKNOWN/missing-min/nonstock/unconfirmed/source-missing/invalid; Item không snapshot không gán kho/location/tồn0; exact-unit totals nullable, per-row/source timestamp/UTC range.
- Scope/data permission+REPORTS_READ hoặc EXPORT trong cùng assignment, active references và authority hiện hành trước count/aggregate/paging/export; che ancestor/private path/globalrevision counters.
- API bearer và Razor /Reports defaults/scoped dropdowns/filter/paging/loading/error/empty; CSV UTF8BOM toàn filter, bounded32MiB, RFC4180/formula guard, serverEXPORT khác read scope.
- Reuse stores/Google adapters/clock/writer/lease, không schema write/SQL/password-session change/guard bypass. Raw TASK-010 source/tests/evidence giữ; Program/Layout chỉ thêm report hooks.

## Bằng chứng kiểm tra — 2026-10-13

AI chạy/quan sát trực tiếp: buildserverexit0,0warnings/errors (.NET10.0.400); filterReport78/78 passed/failed0/skipped0 exit0,3s; hồi quy Asset/InventoryHTTP/scope/GoogleTree guard36/36 exit0,2s. Format riêng3project exit0; node syntax/diffcheckexit0. Fake sources/Google HTTP/Kestrel compiled Razor HTTP, synthetic20744 Items GIẢ; không Google live/browser/manual UAT. Lệnh COMMANDS; [Test Report](../07-testing/TEST-REPORT-TASK-011.md), [evidence](../07-testing/TASK-011-LOCAL-EVIDENCE.json), [thiết kế](../02-architecture/REPORT-DESIGN-TASK-011.md).

| AC | Local actual | Gate còn mở |
|---|---|---|
| 0/low/UNKNOWN/non-stock phân biệt | PASS_LOCAL: min/source/unit/asOf/tracking/case thiếu/tổng exact-unit/null | Workbook/stock/ngưỡng/reference OPEN/TBD; Google/browser chưa chạy |
| Không cộng đơn vị, scope trước totals/export | PASS_LOCAL core/API/HTTP/fakeGoogle; hidden data/paths/revisions, wide viewer+narrow export, scoped overflow | Actual Google permission/coherence/throughput và browser CSV reader |
| R01/R02 Assets khác Item count; UATfilter | PASS_LOCAL synthetic20744Items/chunked page/Assets-only/parent-Funique; HTTP defaults/filter/paging/error | NOT_RUN_BROWSER/UAT filter; không DONE từ HTTP render |

Checklist nghiệm thu tổng thể giữ chưa đạt vì gate thật; TASK-011 IN_REVIEW, không DONE. TASK-003 IN_REVIEW, TASK-004 DONE; TASK-005–010 IN_REVIEW, hoãn/lịch sử nguyên vẹn; TASK-012–014 TODO. Ngoại lệ local không phê duyệt nguồn/DRAFT/nguỡng hoặc giải quyết gate cũ.

Self-review theo AC/scope/current-data/failure/null/source projection/cross-assignment/CSV/bounds: sửa binding defaults và feedback trang ngoài range, boundedCSV/BOM formula guard. Không independent reviewer/subagent/audit bảo mật toàn diện. Không chạy toàn UAT trước, yêu cầu secret/checklist người dùng, real Google/data writes/deploy/commit/push/TASK-012.

## Ngoại lệ phụ thuộc — 2026-10-13

Người dùng cho phép hoàn tất local TASK-011 dù TASK-007/010 IN_REVIEW; TASK-005–010 giữ trạng thái/gates/DEFERRED_BY_USER/STOPPED_BY_USER và lịch sử. Worktree39file TASK-010 đầu lượt giữ, HEAD463896d/main; không commit/push/deploy/Google live/dữ liệu thật/secret mới/TASK-012. Local pass không thay nguồn/rule/ngưỡng/OrgLocation/browser/Google chưa xác minh. Guard metadata/single-writer/.NET10/Sheets/Drive/drive.file/passwordmin8/session policy giữ.

## Lựa chọn kỹ thuật local

Chỉ R01–R04. CSV UTF-8 BOM là định dạng export được chọn trong Task; REPORTS_EXPORT riêng, toàn kết quả đã lọc scope/filter, không chỉ trang; chống formula injection. Không XLSX/PDF/R05–R15. asOf là thời điểm đọc report và timestamp từng snapshot; datefilter không là time-travel lịch sử. .NET/backend patterns đã đọc được tái sử dụng với application service/DTO/DI/async và test xUnit; không SQL/EF/subagent.
