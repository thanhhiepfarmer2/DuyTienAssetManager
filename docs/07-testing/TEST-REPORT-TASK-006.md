# TEST-REPORT — TASK-006

Ngày: 2026-10-10. Nguồn bằng chứng: **AI chạy/quan sát trực tiếp local**.
TASK-006 `IN_REVIEW`: local đã triển khai/kiểm tra; chưa đạt gate Google live/browser và đối chiếu workbook gốc.
Ngoại lệ bắt đầu khi TASK-005 chưa DONE là quyết định rõ của người dùng, đã ghi trong PLAN-001/INDEX/PROJECT-CONTEXT. Không chạy lại full UAT TASK-005; không hỏi UAT/secret.

## Chức năng đã triển khai

- Preview JSON staging/XLSX cấu trúc A/B/C và metadata F1/F2/F3 theo header/STT; không đọc sheet nhân viên hoặc tạo nút từ ghi chú. Giới hạn file 10 MB, 1000 nút, F1–F3 cho mapping này; DTD/external sheet/formula/ZIP trùng/oversize/header lạ bị chặn, không tự đoán mapping gốc thiếu.
- Validation code/unique/parent/level/cycle/provenance/name; báo lỗi sheet/dòng. Metadata definition giữ riêng, trạng thái DRAFT; danh sách tên nhiều code và chênh baseline rõ.
- CategoryId opaque cấp khi commit; match source mapping mã nguồn duy nhất, không STT/tên. Rename code cần explicit category_id JSON hoặc hiển thị ADD/REMOVE để review; không tự gộp. Giữ phiên bản cũ và diff tên/đường dẫn/definition/provenance.
- Preview gắn actor/TTL 15 phút; revision/hash chặn file/preview cũ; ledger idempotency cho cả commit và no-op, không dùng cùng key với payload khác. Retry sau mất response/reconstruction không nhân bản Category. TTL preview không đổi chính sách auth.
- Adapter thật Google Sheets viết **một atomic batch** trong business spreadsheet khác system. Cả Categories/TreeVersions/ImportJobs/ImportRows/Operations/AuditEvents/SchemaVersions cùng commit. Append lịch sử, pointer hiệu lực demo vẫn DRAFT; không publish mã chính thức. Header/journal khác đích thiết kế thì dừng, không overwrite. Timeout không append mù, đọc lại ledger xác minh trước trả thành công.
- Tree/detail/tìm có hoặc không dấu/TXT UTF-8 không BOM; lọc scope trước count/search/path; tổ tiên ẩn không xuất tên/mã/ParentId. TXT indent 2 space mỗi cấp **nhìn thấy**; path partial có nhãn. Category chỉ CategorySubtree, OrgUnit/Location N/A theo PERMISSIONS; không cross-combine assignments khi import đổi nhánh. Full snapshot đòi quyền tại tất cả nút cũ/mới; scoped manager không được thay toàn cây ngoài phạm vi.
- API bearer và Razor cookie/antiforgery dùng cùng service, có DRAFT/preview/error/empty/conflict/access denied/loading. Không đổi password/session; không thêm SQL, scope OAuth, mobile UI hoặc asset/move endpoint TASK-007/008.

## Đối chiếu nguồn

AI đọc/đếm trực tiếp `docs/03-database/category-candidates.json`: 665 nút, F1=15/F2=98/F3=552; 39 tên nhiều code, tất cả cha hợp lệ; delta baseline 0/0/0/0/0. Import reimport và version mới trên chính staging này được kiểm bằng Google HTTP stub.
Authoritative vẫn là **Bảng mã(Nháp lần 5).xlsx** theo SOURCE-AUDIT, không chuyển sang DOCX 13 F1. `rg --files --hidden` và inventory repository không có XLSX gốc. Không chứng nhận lại hash/metadata original; staging không có definition. Metadata XLSX mới chỉ test bằng workbook giả lập có Unicode/header lặp/ghi chú, chưa chứng minh tương thích mọi header workbook gốc.

## Lệnh thực tế và kết quả

Build/test dùng chung output chạy tuần tự; dừng đúng backend workspace PID18664 trước build bằng kiểm ExecutablePath, không dừng process khác. Backend dev không được khởi động lại để chạy UAT.

| Lệnh | Expected | Actual | Exit | Nguồn |
|---|---|---|---:|---|
| `dotnet build .\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj --no-restore --nologo -v:minimal` (cuối) | Compile sạch | Build succeeded; 0 warnings, 0 errors | 0 | AI local; guard LASTEXITCODE cho phép chạy lệnh tiếp |
| `dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~Category"` (cuối) | Các test TASK-006 pass | **46 passed / 0 failed / 0 skipped**; không warning | 0 | AI local; guard LASTEXITCODE và test output |
| `dotnet test .\tests\DuyTien.AssetManagement.ArchitectureTests\DuyTien.AssetManagement.ArchitectureTests.csproj --no-build --no-restore --nologo -v:minimal --filter "FullyQualifiedName~ScopeAuthorizationTests|FullyQualifiedName~AuthApiIntegrationTests|FullyQualifiedName~AuthRazorUserDetailTests"` | Hồi quy quyền/HTTP/auth Razor liên quan | **12 passed / 0 failed / 0 skipped** | 0 | AI local |
| `dotnet format <Application/Infrastructure.Google/Server/ArchitectureTests csproj> --no-restore --include <absolute paths của các file TASK-006 mới> --verbosity minimal` | Format file trong task | 4 project đều exit 0; không format file auth/user có sẵn | 0 từng lệnh | AI local; path selection được ghi trong COMMANDS |

Các nhóm 46 test: catalog 18; parser 12; Google HTTP contract 10; HTTP/rendered Razor 6. Chứng minh validation/duplicates/metadata parser, nguồn baseline, retry đồng thời/version/diff/ID, no-op ledger/key conflict/stale preview, quyền read/import/export/hidden path/cross-assignment move/revoke, atomic write/response lost/reconstruction/unknown tables/journal, bearer/cookie boundary, render HTML/encoding/empty/error503 recovery và antiforgery GET/POST preview/commit.
**HTTP render Razor không phải browser UAT**: JavaScript loading, tương tác/layout/accessibility chưa quan sát trên trình duyệt. Fake Sheets/memory chỉ nằm trong test, không là CSDL ứng dụng.

## Lịch sử kiểm tra trong lượt

- Build đầu exit 0, sạch. Test vòng đầu 37/37 exit 0, có warning xUnit2031 ở assertion; đã sửa overload đúng.
- Sau thêm HTTP/Razor: 42/42 exit 0. Sau sửa ledger no-op/journal: 43/43 exit 0. Sau các kiểm tra bổ sung: 46/46 exit 0. Không có test bị skip hoặc làm yếu để pass.
- Một batch tạo file quá dài bị runtime từ chối os error206 trước khi chạy; đã chia thành lệnh nhỏ. Đây không phải lỗi build/test và không có exit code shell để báo.
- Không chạy full auth suite hoặc Google UAT đã đạt 173/173. 78/78 và 14 boundary riêng là bằng chứng TASK-005 lịch sử, không cộng thành tổng suite mới.

## Gate chưa xác minh

| Gate | Trạng thái | Giới hạn / bước tiếp theo |
|---|---|---|
| Hash/header/definition workbook authoritative | NOT_RUN_SOURCE_ORIGINAL | File XLSX không có trong repository; cần đối chiếu khi nguồn có sẵn, không yêu cầu người dùng thao tác trong lượt này |
| Business schema/import/reimport/version/restart/ACL trên Google dev thật | NOT_RUN_GOOGLE | Chỉ HTTP stub. Chưa tạo tab hoặc import dữ liệu vào Google trong lượt này; việc chạy live sau phải xác minh dev đích/quyền/lease |
| Browser Razor desktop/mobile/loading/error/antiforgery/permission interactions | NOT_RUN_BROWSER | Render HTTP pass không thay browser; không giao checklist thủ công theo chỉ đạo hiện tại |
| Các module khác, mobile và host | OUT_OF_SCOPE | Không làm TASK-007 trở đi, không deploy |

Thiết kế atomic bounded thay chunk tổng quát cho TASK-006: tối đa 1800 KiB payload mỗi batch và 20000 dòng quản lý mỗi bảng. Nếu vượt, báo lỗi trước write; không báo PARTIAL/success giả. Khi mở rộng ngoài giới hạn cần bổ sung chunk/staging/resume và kiểm mới. Process lease hiện có + coordinator dùng chung, không có CAS/multi-instance guarantee trên Sheets.

## Kết luận

TASK-006 `IN_REVIEW`, phần local hoàn tất, chưa DONE. TASK-005 `IN_REVIEW`, nghiệm thu còn lại DEFERRED_BY_USER/runner STOPPED_BY_USER. TASK-003 IN_REVIEW, TASK-004 DONE. Bước kế hoạch tiếp theo là đối chiếu nguồn và bằng chứng Google/browser của TASK-006 khi được tiếp tục; TASK-007 vẫn TODO và còn gate rule mã/required fields. Không commit/push/deploy, không xóa tài khoản/audit, không ghi dữ liệu thật.

## Kiểm tra kết thúc

AI chạy `git diff --check`: exit0, không lỗi whitespace; `node --check .\src\DuyTien.AssetManagement.Server\wwwroot\js\category-import.js`: exit0, syntax pass (không là browser). Kiểm process/secret chỉ boolean: W13RunnerAbsent=True, TemporaryUatSecretPresent=False, BootstrapSecretPresent=False, WorkspaceBackendCount=0; command exit0. Các file người dùng đã thay đổi trước lượt vẫn được giữ; không reset/clean/stage/commit.
