# PLAN-001 — Demo quản lý tài sản dựa trên Google Sheets/Drive

## Ưu tiên hiện hành — 2026-10-14

Chỉ đạo mới thay yêu cầu TASK-013: hoàn thiện Web trước (TASK-014 pha Web), Android sau; iOS DEFERRED_BY_USER vì môi trường/thiết bị chưa sẵn sàng. TASK-012 giữ IN_REVIEW/local42tests/build, phát triển/runtime UAT DEFERRED_BY_USER. Không gate mobile chặn Web, không xóa code hoặc thay bằng chứng lịch sử. TASK-003 IN_REVIEW, TASK-004 DONE; TASK-005–011/gates hoãn giữ. TASK-010/011/012 chưa commit bảo vệ; không Google live/real writes/deploy/commit/push/timeout runner mới. [Thiết kế Web](WEB-COMPLETION-DESIGN-TASK-014.md).

## Ngoại lệ phụ thuộc TASK-012 — 2026-10-13

Người dùng cho phép local TASK-012 dù TASK-005/007/009 IN_REVIEW, không đóng gate/task trước. Giữ TASK-010/011 chưa commit, lịch sử/DEFERRED_BY_USER/STOPPED_BY_USER; không Google live/dữ liệu thật/deploy/commit/push/TASK-013 hoặc UAT/secret/thiết bị mới. [Thiết kế Android](MOBILE-ANDROID-DESIGN-TASK-012.md).

## Ngoại lệ phụ thuộc TASK-011 — 2026-10-13

Người dùng cho phép hoàn tất local TASK-011 dù TASK-007/010 IN_REVIEW; TASK-005–010 giữ trạng thái/gates/DEFERRED_BY_USER/STOPPED_BY_USER và lịch sử. Worktree39file TASK-010 đầu lượt giữ, HEAD463896d/main; không commit/push/deploy/Google live/dữ liệu thật/secret mới/TASK-012. Local pass không thay nguồn/rule/ngưỡng/OrgLocation/browser/Google chưa xác minh. Guard metadata/single-writer/.NET10/Sheets/Drive/drive.file/passwordmin8/session policy giữ.

## Ngoại lệ TASK-010 trước TASK-011 — 2026-10-13

Cho phép local TASK-010 dù TASK-005–009 IN_REVIEW; gate cũ/hoãn/bằng chứng giữ nguyên, không phê duyệt DRAFT. Checkpoint TASK-007–009 đã commit/push main 463896d (exit0, remote khớp); quyền commit/push chỉ trước TASK-010. Phần TASK-010 để chưa commit; không live/browser UAT/secret mới/ghi dữ liệu thật/deploy/TASK-011. Guard metadata giữ: tree mutation chặn khi registry mới chưa đối soát. Workbook/rule/nguồn đơn vị/địa điểm/stock/ngưỡng vẫn OPEN/TBD; dùng fixture giả có nhãn.

## Ngoại lệ phụ thuộc TASK-009 — 2026-10-13

Người dùng cho phép hoàn tất phần local TASK-009 dù TASK-005/006/007/008 IN_REVIEW; không đóng gate trước, giữ DEFERRED_BY_USER. Không Google live/UAT thủ công/dữ liệu thật/commit/push/deploy/TASK-010. Guard metadata TASK-008 giữ nguyên, không bypass; tab Files/Documents làm tree mutation fail-closed cho đến khi có thiết kế đối soát module được giao riêng. Rule/workbook/Org-Location OPEN/TBD; file mẫu thật và Drive target chưa xác minh, fixture giả chỉ cho local.


## Ngoại lệ phụ thuộc TASK-008 — 2026-10-13

Người dùng cho phép hoàn tất phần local TASK-008 dù TASK-005/006/007 còn IN_REVIEW. Các gate hoãn TASK-005 và nguồn/workbook/Google/browser TASK-006/007 giữ nguyên; rule mã/fields/Org-Location công ty OPEN/TBD. Ngoại lệ không phê duyệt DRAFT, không cho ghi dữ liệu thật/commit/push/deploy/TASK-009. Publish kỹ thuật là đổi pointer nguyên tử sang snapshot DRAFT đầy đủ, không công bố mã chính thức.


## Ngoại lệ phụ thuộc TASK-007 — 2026-10-13

Người dùng cho phép triển khai đầy đủ phần local TASK-007 dù TASK-006 chưa DONE. TASK-005 giữ IN_REVIEW và DEFERRED_BY_USER; TASK-006 giữ IN_REVIEW, workbook gốc/Google live/browser chưa xác minh. Ngoại lệ chỉ cho bắt đầu local, không đóng gate và không cho phép ghi dữ liệu thật, commit/push/deploy hoặc làm TASK-008. Rule mã/trường nghiệp vụ công ty chưa chốt: dùng fixture giả có nhãn cho phần độc lập, không tự phê duyệt rule hay DRAFT.


## Ngoại lệ đã được người dùng cho phép — 2026-10-10

Người dùng dừng nghiệm thu TASK-005 và cho phép triển khai local TASK-006 dù TASK-005 chưa DONE (2026-10-10).
TASK-005 giữ `IN_REVIEW`: G2a/G2b PASS_MANUAL; G2c và browser còn mở DEFERRED_BY_USER; idle 30 phút thực PASS; W13 8h/24h/7d STOPPED_BY_USER. PID 18964 đã dừng đúng runner, credential UAT được AI xác minh không còn; giữ tài khoản/audit/bằng chứng lịch sử. Không chờ 17/10, không tiếp tục giao UAT hoặc yêu cầu secret. TASK-003 IN_REVIEW, TASK-004 DONE.


- Requirement: [REQ-001](../01-requirements/REQ-001-asset-digitization.md)
- Status: READY
- Workflow chính / skill thực tế: `conductor:context-driven-development` cho Context → Spec/Plan và đồng bộ nguồn chuẩn; thực thi từng Task theo skill kỹ thuật phù hợp sau này.

## Thiết kế tối thiểu
- Quyết định sản phẩm: Google Sheets là CSDL, Drive lưu file; có web và app Android/iOS riêng; cây Excel hiện tại là `DRAFT`.
- [ADR-001](ADR-001-proposed-sheets-drive-web-mobile.md) đã `ACCEPTED`: .NET 10/Razor Pages/MAUI, API chung và một backend instance/single writer cho demo.
- Web và mobile dùng chung API, rules, authorization và dữ liệu; backend kiểm tra quyền cho list/detail/report/file, không dựa vào việc ẩn nút.
- Lát đầu tiên sau PoC/auth: dùng một nhánh máy móc TST với dữ liệu giả, import nhánh, admin tạo một máy, viewer tìm/xem, editor sửa vị trí và kiểm tra ID không đổi.

## Thứ tự thực hiện
| Thứ tự | Task | Trạng thái | Đầu ra | Phụ thuộc / gate |
|---|---|---|---|---|
| 1 | [TASK-002](../06-tasks/TASK-002.md) | DONE | Requirement/Plan/Task authoritative và câu hỏi cho bước setup | Không phụ thuộc code |
| 2 | [TASK-003](../06-tasks/TASK-003.md) | IN_REVIEW | ADR accepted, scaffold/framework và môi trường local; checkpoint external gates | Google/host/device |
| 3 | [TASK-004](../06-tasks/TASK-004.md) | DONE | Local 31 tests pass; live Google và manual resource/control-file có user-provided evidence | TASK-003 vẫn IN_REVIEW nhưng không chặn checkpoint TASK-004 |
| 4 | [TASK-005](../06-tasks/TASK-005.md) | IN_REVIEW | [Auth design](AUTH-DESIGN-PROPOSED.md), backend/API + Razor login/admin/roles/scope | Local 78/78 và 14 boundary riêng; Google/HTTP 173/173; idle30ph thực PASS; nghiệm thu còn lại DEFERRED_BY_USER/W13 STOPPED_BY_USER |
| 5 | [TASK-006](../06-tasks/TASK-006.md) | IN_REVIEW | Local preview/import/tree/detail/search/TXT + Google adapter/API/Razor; build sạch, 46 tests + 12 hồi quy pass | Ngoại lệ bắt đầu khi TASK-005 IN_REVIEW; workbook gốc/Google live/browser còn NOT_RUN |
| 6 | [TASK-007](../06-tasks/TASK-007.md) | IN_REVIEW | Local CRUD máy giả + API/Razor/Google adapter, build sạch/54 tests +9 hồi quy | Ngoại lệ 2026-10-13; rule/fields/source/Google/browser chưa đạt |
| 7 | [TASK-008](../06-tasks/TASK-008.md) | IN_REVIEW | Local API/Razor/Google joint adapter cho sửa/move/archive cây và bulk Asset; build sạch42+91 test pass | Ngoại lệ13/10 khi TASK-005/006/007 IN_REVIEW; Google/browser/source/rule còn gate |
| 8 | [TASK-009](../06-tasks/TASK-009.md) | IN_REVIEW | Local ảnh/API/Razor, registry DOCX/PDF/gateway/reconcile/retention; build sạch56+67 tests | Ngoại lệ13/10 khi TASK-005–008 IN_REVIEW; mẫu/Drive/live/browser chưa đạt; guard giữ |
| 9 | [TASK-010](../06-tasks/TASK-010.md) | IN_REVIEW | Local import Items/chunk/snapshot/API/Razor, build sạch66+8UI+46hồi quy | Ngoại lệ13/10; nguồn stock/ngưỡng/workbook/Google/browser còn gate |
| 10 | [TASK-011](../06-tasks/TASK-011.md) | IN_REVIEW | Local R01–R04/API/Razor/scopedCSV, build sạch78+36tests | Ngoại lệ13/10 khi TASK-007/010 IN_REVIEW; source/stock/ngưỡng/Google/browser còn gate |
| 11 | [TASK-012](../06-tasks/TASK-012.md) | IN_REVIEW | Android MAUI local auth/assets/ảnh; build sạch42/42tests | Ngoại lệ13/10 khi TASK-005/007/009 IN_REVIEW; runtime/device/camera/Google còn gate |
| 12 | [TASK-013](../06-tasks/TASK-013.md) | TODO | App iOS hoãn DEFERRED_BY_USER | Môi trường/thiết bị chưa sẵn sàng; không chặn Web |
| 13 | [TASK-014](../06-tasks/TASK-014.md) | IN_REVIEW | Web local: menu/registry/luồng/hướng dẫn; 126 checks/build đạt, runtime TLS blocked | Mobile không chặn Web; giữ gate browser/Google/source/restore |

Chi tiết và trạng thái authoritative nằm tại [INDEX](../06-tasks/INDEX.md). Không
bắt mọi Task chờ toàn bộ sản phẩm, nhưng không bỏ qua gate trực tiếp; không dựng
tất cả bảng/module/code trong một lượt.

Checkpoint 2026-10-06: người dùng cho phép triển khai phần local của TASK-004 độc
lập với các gate Google/hosting/thiết bị còn mở. Ngày 2026-10-07, người dùng cung
cấp kết quả chạy readiness, access-boundary, init và PoC trên Google thật;
TASK-004 chuyển `DONE` sau khi người dùng xác nhận thủ công tab/TXT và tính hợp lệ
file control; AI không trực tiếp quan sát. TASK-003 vẫn `IN_REVIEW` vì host/device
còn mở.

## Rủi ro và phương án xử lý
- Mã nháp xung đột DOCX: giữ state `DRAFT`, source audit và chốt rule demo trước allocator.
- Sheets cạnh tranh/timeout: PoC single writer/idempotency/atomic batch; không scale-out khi chưa có cơ chế phối hợp được duyệt.
- Drive ownership/file ACL: My Drive đã chốt; chỉ dùng `drive.file`. Resource mới
  phải do app provisioning, resource có sẵn qua Picker; folder không truyền quyền
  cho file con. Live test phải có cả resource được phép và file control bị chặn.
- Không có tồn: snapshot riêng, `UNKNOWN` không biến thành 0.
- iOS: Mac/Xcode/signing/device là gate thật; thiếu thì `BLOCKED`/`NOT_RUN`, không lấy Android thay bằng chứng.
- Auth scope: dùng cùng authorization service cho API/file/report/list/detail và có test trực tiếp.
- Auth design TASK-005 đã `ACCEPTED`: scope gắn từng role assignment để nhiều
  role không khuếch đại quyền; system spreadsheet riêng và bootstrap bằng command.
  Lượt local 2026-10-07 không tạo/chọn/ghi resource Google; lượt 2026-10-09
  đã provisioning/init DEV riêng, chưa bootstrap hoặc đạt toàn bộ UI/live gate.

## Kiểm thử và nghiệm thu
[Kế hoạch nghiệm thu](../07-testing/ACCEPTANCE-PLAN.md) ánh xạ các case mức sản
phẩm. Mỗi Task phải ghi build/test count, môi trường, thiết bị và bằng chứng thật;
fake adapter không thay live Google, Android không thay iOS. Tài liệu READY không
có nghĩa feature đã hoàn thành.

Checkpoint Web 14/10: [Report](../07-testing/TEST-REPORT-TASK-014-WEB.md). Phát triển/runtime Android TASK-012 DEFERRED_BY_USER; iOS TASK-013 DEFERRED_BY_USER do môi trường/thiết bị chưa sẵn. Không tự chuyển sang mobile từ local Web pass.
