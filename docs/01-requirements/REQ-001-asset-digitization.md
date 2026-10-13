# REQ-001 — Mã số hóa tài sản Công ty TNHH Duy Tiến

Ngày: 2026-10-07. Trạng thái: READY — các quyết định sản phẩm đủ để lập kế hoạch; mục OPEN/PROPOSED vẫn là gate của Task phụ thuộc, không phải quyết định đã duyệt.
Nguồn: yêu cầu người dùng ngày 06–07/10/2026; gói `DuyTien_Asset_Requirements`; kết quả phân tích ba file được ghi tại [SOURCE-AUDIT](SOURCE-AUDIT.md). TASK-002 không đọc lại ba file nguồn vì chúng không nằm trong gói.
Người xác lập nghiệp vụ: Nguyễn Thanh Hiệp.
Task/Plan liên quan: [TASK-002](../06-tasks/TASK-002.md), [PLAN-001](../02-architecture/PLAN-001.md), [INDEX](../06-tasks/INDEX.md).

## 1. Quyết định đã được người dùng xác lập
| ID | Nội dung | Nguồn |
|---|---|---|
| D-01 | Tên hiển thị: Mã số hóa tài sản Công ty TNHH Duy Tiến | Yêu cầu hiện tại |
| D-02 | Lấy cây và mã hiện tại từ Bảng mã(Nháp lần 5).xlsx làm nguồn demo, giữ dấu nháp và ghi mâu thuẫn với DOCX | Lựa chọn trong phiên |
| D-03 | Google Sheets là CSDL; tài liệu/ảnh nằm trên Drive công ty | Yêu cầu hiện tại |
| D-04 | Có ứng dụng web và app Android/iOS riêng; mobile nhập liệu, chụp ảnh | Yêu cầu và lựa chọn trong phiên; thay lựa chọn responsive-only trước đó |
| D-05 | Đăng nhập tài khoản; admin có toàn quyền và tạo/quản lý người dùng; quyền đọc/thêm/sửa/xóa theo chức năng và vị trí | Yêu cầu hiện tại |
| D-06 | Báo cáo “đang hết” là hết/sắp hết số lượng; không diễn giải thành hết bảo hành | Lựa chọn trong phiên |
| D-07 | Backend/API dùng ASP.NET Core .NET 10, web dùng Razor Pages, app Android/iOS dùng .NET MAUI; web/mobile dùng chung backend | Yêu cầu TASK-003 ngày 2026-10-06 |
| D-08 | Hệ thống dùng trong công ty và từ Internet qua HTTPS; demo chạy một backend instance để giữ single writer cho Sheets | Yêu cầu TASK-003 ngày 2026-10-06 |
| D-09 | Ảnh/tài liệu ở thư mục My Drive của tài khoản vận hành; backend dùng Google OAuth tách với tài khoản đăng nhập ứng dụng | Yêu cầu TASK-003 ngày 2026-10-06 |
| D-10 | Google integration chỉ request `drive.file` cho cả Sheets/Drive, không fallback quyền rộng; resource mới do app provisioning chủ động, resource có sẵn phải được người vận hành chọn qua Google Picker; nhập ID hoặc chọn folder cha không tự cấp quyền file con | Yêu cầu người dùng ngày 2026-10-07 |
| D-11 | Duyệt năm role/scope/chức danh và demo auth TASK-005; Location có ParentId; admin hoạt động phải có assignment Admin hiệu lực với đủ ba ALL; auth maintenance loại trừ writer liên tiến trình; mật khẩu chỉ yêu cầu tối thiểu 8 ký tự, không có rule độ mạnh khác | Yêu cầu người dùng ngày 2026-10-07 |

Google dev đã được setup và nghiệm thu TASK-004; OAuth/resource IDs ở ngoài
repo. Ngày 2026-10-09 người dùng cho phép provisioning/init/bootstrap system
spreadsheet dev riêng bằng dữ liệu giả cho TASK-005; provisioning/ACL/schema đã
đạt, bootstrap/phiên/quyền live và UI UAT còn thiếu. Nhà cung cấp hosting/domain,
rule mã demo và chi tiết thiết bị/signing vẫn chưa xác lập. Không tự chọn dịch vụ
trả phí hoặc deploy; tài nguyên Google chỉ thao tác trong phạm vi đã được giao.

## 2. Phạm vi sản phẩm và cách tách bản ghi
- Categories: cây phân loại dùng chung cho 15 F1 hiện tại; cấp sâu mở rộng tới F10 khi có dữ liệu/rule đã xác lập.
- Assets: từng tài sản cần quản lý độc lập (máy, thiết bị, phương tiện, công cụ, tài sản số...). Không tạo 20.744 Assets bằng cách coi mọi mã vật tư là một chiếc tài sản.
- Items: danh mục vật tư/hàng hóa/dịch vụ tham chiếu từ Excel; một Item có thể liên kết nhiều Assets hoặc tồn theo kho, tùy nghiệp vụ.
- Documents: bản ghi hồ sơ tài liệu và các phiên bản/file liên quan; biểu mẫu có file DOCX để tải và PDF chuẩn để xem/đối chiếu.
- StockBalances: số lượng hiện có của một Item tại một địa điểm, đơn vị chuẩn, ngưỡng tối thiểu và thời điểm cập nhật. Là dữ liệu bổ sung riêng vì Excel chưa có tồn thực tế.
- Nhóm Nhân sự/Đối tác/Công trình vẫn hiện trong cây chung; quản lý nghiệp vụ đầy đủ của các nhóm này không thuộc bản demo tài sản. Dữ liệu nhân viên trong workbook không tự nhập thành tài khoản đăng nhập.

## 3. FR-01 — Đăng nhập và tài khoản
Chi tiết chính sách password/session/bootstrap/system spreadsheet đã được duyệt
tại [AUTH-DESIGN-PROPOSED](../02-architecture/AUTH-DESIGN-PROPOSED.md) ngày
2026-10-07 (tên file được giữ vì lịch sử liên kết).

- Có màn hình đăng nhập, đăng xuất, xem tài khoản và đổi mật khẩu. Admin tạo, sửa, khóa/mở khóa, reset mật khẩu, gán vai trò và phạm vi người dùng.
- Tài khoản khởi tạo tên `admin`, toàn quyền. Mật khẩu ban đầu nhập qua cấu hình bí mật ngoài repo khi vận hành; không gửi trong chat hoặc chép vào tài liệu, repo, client, Sheet dạng plaintext hay log.
- Khởi tạo admin chỉ một lần khi chưa có tài khoản đó; không reset mật khẩu/quyền mỗi lần startup. Có quy trình phục hồi khi mất quyền admin.
- Lưu password hash chuẩn và metadata xác thực trong vùng Sheets hệ thống hạn chế quyền Google; dùng cơ chế băm đã kiểm chứng của framework, không tự viết thuật toán.
- Bắt buộc đổi mật khẩu admin sau lần đầu, lockout/rate limiting, thời hạn session và thu hồi session khi khóa tài khoản/đổi mật khẩu. Yêu cầu mật khẩu duy nhất là tối thiểu 8 ký tự; không complexity/max/blocklist/username rule.
- Không cho xóa/khóa/hạ quyền admin hoạt động cuối cùng. Admin là tài khoản ứng dụng; không tự tạo Google account cho mỗi user.

## 4. FR-02 — Quyền theo chức năng và phạm vi
Ma trận năm role và scope theo loại dữ liệu đã `ACCEPTED` tại
[PERMISSIONS](PERMISSIONS.md); đây không phải cơ cấu/chức danh thật của công ty.

- Mỗi hành động có permission riêng (READ, CREATE, UPDATE, DELETE, MOVE, IMPORT, EXPORT, UPLOAD, DOWNLOAD, MANAGE_USERS...). Vai trò chỉ là nhóm permission.
- “Vị trí” được mô hình hóa dự kiến thành chức danh/vị trí công việc, đơn vị/phòng ban và địa điểm quản lý; cần xác nhận ý nghĩa thực tế trước gán quyền nhân viên.
- Quyền hiệu lực = permission hành động AND phạm vi bản ghi. Danh sách, chi tiết, tìm kiếm, báo cáo, export và file phải cùng áp dụng bộ lọc quyền server.
- Người dùng chỉ đọc không thêm/sửa/xóa hoặc tự nâng quyền bằng gọi API trực tiếp. Người sửa dữ liệu không mặc nhiên quản lý tài khoản.
- Di chuyển đối tượng/danh mục cần quyền ở cả nguồn và đích; xử lý trước sự thay đổi phạm vi nhìn thấy sau khi chuyển.
- Người dùng thường không nhận quyền truy cập trực tiếp bảng CSDL/file Drive. File được tải qua backend kiểm tra quyền; không dùng “anyone with link” để đơn giản hóa demo.

## 5. FR-03 — Nạp và quản lý cây danh mục
- Import workbook hiện tại qua preview: 15 F1, 98 F2, 552 F3, tổng 665 dòng nút tại sheet F1-F3 Mã ngang. Dữ liệu staging kèm row/STT/hash nguồn.
- Không lấy “Liệt kê/Ví dụ” hay ghi chú trong F2/F3 tự động làm các nút sâu. Đây là nội dung mô tả chưa đủ cấu trúc.
- STT và mã đoạn không phải khóa hệ thống. Mỗi nút có CategoryId ổn định, ParentId, tên, mã đoạn, mã đầy đủ, cấp, trạng thái và version.
- Tạo/sửa tên, định nghĩa và mã nháp; kiểm tra trùng đường dẫn, thiếu cha, chu trình, giới hạn F10. Đổi mã/nhánh phải preview tác động, lưu alias/mapping và lịch sử.
- Excel có 39 tên xuất hiện với nhiều mã: giữ từng nút và mã nguồn trong chế độ nháp; không tự gom hay đổi mã. Cơ chế từ điển canonical theo DOCX đang là điểm cần rà soát, không coi bản nháp là mã đã phê duyệt.
- Chuyển một nút kéo cả cây con; không đổi CategoryId hay ID các Assets. Mã đường dẫn phân loại được cập nhật/tính lại theo phiên bản cây.
- Xuất TXT UTF-8 dạng cây: thụt lề theo cấp, STT hiển thị/tên/mã đoạn/mã đầy đủ/trạng thái; chỉ xuất nhánh có quyền. Không kèm thông tin tài sản bị ẩn do quyền.
- Xóa danh mục rỗng: ngừng dùng/xóa mềm. Danh mục còn con/đối tượng: chặn xóa trực tiếp, hiển thị tác động; người có quyền chọn chuyển sang danh mục đích hoặc xóa mềm đối tượng và ngừng dùng nhánh. Cần xác nhận thao tác hàng loạt trên UI.

## 6. FR-04 — Nhập, sửa, xóa và tra cứu tài sản
- Tạo tài sản từ danh mục hiệu lực demo, nhập tên và thuộc tính theo loại; xem chi tiết, sửa dữ liệu, di chuyển phân loại, ngừng dùng/xóa mềm và khôi phục khi có quyền.
- Tập trường nền đề xuất: AssetId hệ thống, ObjectCode (mã hiển thị), tên, CategoryId, loại, serial/model/hãng nếu có, đơn vị quản lý, người quản lý, địa điểm, ngày ghi nhận, tình trạng, ghi chú, Created/Updated và Version. Trường bắt buộc theo loại được xác lập trước Task form.
- Khóa AssetId và ObjectCode sau cấp; sửa tên/vị trí/danh mục không tính lại ID. Số zero đầu được giữ như text.
- Áp dụng hình thức GM-CCC-NNNNNN của DOCX như quy tắc demo được đề xuất, với CCC từ nhóm Excel hiện tại; prefix/format phải được chốt trong hồ sơ rule demo trước khi chạy allocator. Chưa phải ban hành mã chính thức.
- Khi đối tượng chuyển nhóm F1 nhưng vẫn là cùng một đối tượng, giữ ID đã cấp; hiển thị nhóm cấp mã ban đầu riêng với phân loại hiện tại.
- Xóa trên UI là xóa mềm, giữ mã và lịch sử, không cấp lại mã cũ; đây là cách thực hiện nhu cầu xóa phù hợp nguyên tắc truy vết trong tài liệu nguồn.
- Tìm theo mã hiện tại/mã cũ/tên/alias/serial; lọc cây gồm cây con, tình trạng, đơn vị, địa điểm và người quản lý; phân trang. Chuẩn hóa tìm tiếng Việt không dấu là đề xuất UX.
- Lưu đồng thời: kiểm tra Version; bản cũ trả lỗi xung đột và cách reload, không tự ghi đè.

## 7. FR-05 — Ảnh và tài liệu trên Drive
- Mobile chụp ảnh hoặc chọn ảnh thư viện; web tải ảnh. Cho preview/đổi ảnh/hủy, nén theo cấu hình, mô tả ảnh và chọn ảnh chính.
- File được lưu trong khu vực Drive công ty đã xác lập; Sheets chỉ giữ DriveFileId và metadata, không chứa binary/base64 ảnh hoặc DOCX/PDF.
- Backend kiểm tra quyền trên đối tượng trước mọi upload/view/download/delete, kiểm tra file type/size thực tế, không chỉ đuôi file. Giới hạn đề xuất: ảnh 10 MB, DOCX/PDF 25 MB; chốt cấu hình trước triển khai.
- Ghi trạng thái PENDING/AVAILABLE/FAILED cho file; lỗi Drive hoặc Sheets không báo thành công. Nếu upload xong nhưng metadata chưa commit, ghi Operation để có thể đối soát/cleanup.
- Không xóa file Drive ngay khi xóa mềm tài sản; lưu lịch sử và retention. Chỉ cleanup file rác qua tác vụ có phạm vi rõ.
- Mất camera permission phải có hướng dẫn và chọn file thay thế; mất mạng không làm mất form đang nhập. MVP online-first, chưa có yêu cầu đồng bộ offline.

## 8. FR-06 — Hồ sơ tài liệu và biểu mẫu
- Có khu vực Hồ sơ tài liệu → Biểu mẫu lấy từ cây Excel tương ứng. Mỗi mẫu có tên, mã nghiệp vụ, category, phiên bản, trạng thái, ngày hiệu lực và quyền đọc/tải.
- Một phiên bản biểu mẫu liên kết DOCX chỉnh sửa và PDF mẫu chuẩn cùng phiên bản. Hiển thị rõ file thiếu hoặc chưa phát hành; không ghép DOCX v2 với PDF v1.
- Người có DOWNLOAD tải DOCX; xem/tải PDF theo quyền. Người quản lý tài liệu có quyền upload/thay phiên bản, không ghi đè mất bản cũ.
- Nhập DOCX không tự bảo đảm đã có PDF chuẩn; MVP dùng PDF chuẩn được tải riêng. Tự chuyển PDF hoặc điền mẫu DOCX là phần bổ sung cần Task riêng.
- Chỉ bản đã phát hành được làm mẫu chuẩn; file đang nháp đánh dấu rõ.

## 9. FR-07 — Vật tư và số lượng
- Nạp Excel VTHH thành Items qua preview; mã MISA/mã cũ giữ nguyên text trong mapping, không là ObjectCode mới.
- Một mã nguồn bị trùng hai dòng CĐKEM cần review; không tự overwrite/double count. Nếu đồng nhất thì merge có quyết định/audit, nếu khác thì tạo source identity/mapping khác được xác lập.
- Cột 20 “Số lượng” nằm trong nhóm thông tin nguyên vật liệu của biểu mẫu và hiện trống toàn bộ; không dùng làm số tồn. Kho ngầm định cũng không chứng minh tài sản đang ở kho đó.
- Nhập/cập nhật snapshot số lượng hiện có riêng theo Item, địa điểm/kho và đơn vị chuẩn; lưu thời điểm, nguồn và người cập nhật, ngưỡng tối thiểu.
- Không có số lượng: UNKNOWN/Chưa có dữ liệu, không quy đổi thành 0. Dịch vụ và tài sản theo từng chiếc không đưa vào báo cáo tồn vật tư như cùng một đơn vị đo.
- Chưa triển khai sổ nhập/xuất kho hoặc tồn thời gian thực; báo cáo ghi rõ đây là snapshot thời điểm cập nhật.

## 10. FR-08 — Báo cáo và đề xuất
Các báo cáo xác lập: toàn bộ tài sản, theo danh mục (gồm cây con), hết/sắp hết số lượng.
Các báo cáo bổ sung đề xuất chi tiết tại REPORT-CATALOG; không tự bật báo cáo chưa có trường nguồn.
Mọi số liệu/report/export áp dụng quyền, không cộng tổng từ bản ghi ngoài phạm vi. Báo thời điểm dữ liệu, đơn vị đo và điều kiện lọc.
Không cộng m2, kg, cái thành một con số “tổng số lượng”. Chỉ cộng giá trị tiền khi cùng tiền tệ/phương pháp đã xác lập; báo khấu hao để giai đoạn sau khi có quy tắc kế toán.

## 11. Yêu cầu phi chức năng
- Cùng API, rules và dữ liệu cho web/Android/iOS; giao diện tiếng Việt.
- Mọi kết nối người dùng/backend/Google qua HTTPS ở môi trường sử dụng; không nhúng Google credential vào app/web.
- Backend là kênh ghi dữ liệu ứng dụng duy nhất; file Sheets hệ thống hạn chế quyền ngoài app. Admin vận hành có thể chỉnh Sheet phải theo chế độ bảo trì và đối soát.
- Cache/phân trang/batch và retry có giới hạn; không kéo toàn bộ 20.744 Items về mọi màn hình hoặc gọi Google mỗi keystroke.
- Ghi audit trước/sau/actor/reason/timestamp, loại trừ password/token. Backup/version và diễn tập restore trước nghiệm thu sử dụng thật.
- Web, Android và iOS có bằng chứng test riêng. Không ghi iOS DONE dựa trên Android pass.

## 12. Acceptance criteria mức sản phẩm
| ID | Điều kiện nghiệm thu | Bằng chứng |
|---|---|---|
| AC-01 | Import cây hiện tại dựng đủ 665 nút với đúng 15/98/552, không thiếu cha; cảnh báo 39 tên nhiều mã | Report + integration test |
| AC-02 | Ghi cùng request import lần hai không tạo bản sao; import phiên bản Excel mới có preview diff | Integration test |
| AC-03 | Admin bootstrap một lần, đăng nhập được; restart không reset; không lộ mật khẩu nguyên văn | Auth test + kiểm tra log/build output |
| AC-04 | Viewer bị chặn CREATE/UPDATE/DELETE qua API; Editor không quản lý user; scope chặn detail/report/file ngoài phạm vi | API tests |
| AC-05 | Hai request tạo mới đồng thời không trùng ObjectCode; retry sau timeout không tạo đối tượng thứ hai | Concurrency/idempotency tests |
| AC-06 | Sửa tên/chuyển nhánh/soft delete/restore giữ ID; mã xóa không tái cấp | Unit + integration |
| AC-07 | Chuyển cây không tạo cycle/vượt F10; preview đúng con/đối tượng; export TXT giữ Unicode và quyền | Tree tests |
| AC-08 | Form tài sản tạo/sửa/tìm được; cập nhật bản cũ bị 409; thiếu trường được báo đúng | API + web UAT |
| AC-09 | Chụp/chọn ảnh trên Android và iOS lưu DriveFileId; lỗi quyền/mạng/Drive có fallback và không báo save thành công | Test từng nền tảng |
| AC-10 | Tải đúng DOCX và xem PDF chuẩn cùng version; user ngoài scope không truy cập file qua API | Integration + UAT |
| AC-11 | Import Items nhận diện trùng CĐKEM, không coi Item là Asset hay dữ liệu BOM là tồn | Import tests |
| AC-12 | Snapshot chưa có số lượng hiển thị UNKNOWN; =0 là hết; >0 và <=min là sắp hết; tách đơn vị | Report tests |
| AC-13 | 429/timeouts có retry giới hạn; timeout ghi được đối soát; app không báo pass khi Google chưa ghi được | Failure injection + live Google |
| AC-14 | Có backup và một lần restore dev kiểm chứng IDs, quyền và file links; không xóa dữ liệu thật để thử | Restore evidence |
| AC-15 | OAuth request chỉ có `drive.file`; resource mới do app provisioning hoặc resource có sẵn qua Picker truy cập được, còn file control có thật chưa cấp quyền bị từ chối; không fallback scope rộng | Request-boundary tests + live Google access-boundary evidence |

## 13. Các mục còn cần xác lập
- OPEN-01: Gate Google dev của TASK-004 đã đạt bằng evidence user-run/manual;
  không chạy lại PoC. TASK-005 đã xác minh system spreadsheet DEV riêng/owner-only
  ACL/drive.file và init schema ngày 2026-10-09; bootstrap/phiên/quyền live và
  browser UAT vẫn còn mở. Giá trị OAuth/resource IDs tiếp tục ngoài repo.
- OPEN-02: Nhà cung cấp host/domain/TLS cho mạng công ty + Internet; rule cấp mã demo và người phê duyệt/công bố cây.
- OPEN-03: Năm role/scope, Asset demo đủ OrgUnit/Location/Category và chức danh
  chỉ là template đã ACCEPTED ngày 2026-10-07. Mapping cơ cấu vai trò/đơn vị/
  địa điểm thật và tập trường required theo từng loại tài sản vẫn chưa xác lập;
  lượt TASK-005 chỉ dùng fixture giả.
- OPEN-04: Snapshot tồn lấy từ nguồn nào, ai cập nhật, ngưỡng theo kho nào.
- OPEN-05: Thiết bị Android/iPhone và OS mục tiêu; xác minh Mac/macOS/Xcode, signing team/profile và cách phân phối app nội bộ.
- OPEN-06: Mẫu DOCX/PDF thực tế và chính sách retention/backup.
Các Task độc lập vẫn được phân tích trong lúc làm rõ; không tự đánh dấu toàn bộ spec READY_TO_IMPLEMENT khi các gate phụ thuộc còn mở.

## Checkpoint triển khai local FR-04 — 2026-10-13

Người dùng cho phép local TASK-007 khi TASK-006 IN_REVIEW, giữ nghiệm thu thủ công hoãn. [Thiết kế fixture TASK-007](../02-architecture/ASSET-DEMO-DESIGN-TASK-007.md) triển khai một loại MACHINE giả, DRAFT_DEMO; technical fields đủ kiểm scope, không thay required nghiệp vụ/GM-prefix đang OPEN. Fixture TEST/TST và CodeRule true trong test không là phê duyệt rule công ty. Không tự publish DRAFT, nạp personnel/Items/BOM thành tài sản hoặc coi quantity thiếu là0. Local pass không đóng các AC Google/web UAT.

## TASK-008 local — 2026-10-13

Theo ngoại lệ người dùng, làm local FR-03/FR-04 (sửa/move/ngừng cây và bulk Asset) khi TASK-005/006/007 IN_REVIEW. Publish kỹ thuật chỉ active pointer sang snapshot DRAFT đầy đủ; DRAFT_INACTIVE là nhánh ngừng dùng, không phê duyệt mã/cây. Giữ ID/code đã cấp, old code/path mapping và provenance; STT nguồn không là đường dẫn hiện tại. Asset subset TASK-007, không coi Items/Documents/Files chưa triển khai là rỗng. Rule mã/fields/source đơn vị/địa điểm vẫn OPEN/TBD; fixture giả không xác lập nghiệp vụ công ty. Local AC-06/07 pass không thay gate Google/browser/source. [Thiết kế](../02-architecture/TREE-MUTATION-DESIGN-TASK-008.md).

## TASK-009 local — 2026-10-13

Người dùng cho phép local FR-05/FR-06 dù TASK-005–008 IN_REVIEW, giữ hoãn trước. Ảnh web, registry/versions, cặp DOCX/PDF, scoped gateway và durable upload operation/reconcile/retention đã triển khai local. DRAFT_DEMO/PUBLISHED_DEMO là trạng thái demo có confirm, không ban hành mẫu công ty; file nguồn thật chưa có trong repo. Không chốt format business code, rule Asset/nguồn đơn vị/địa điểm/workbook gốc vẫn OPEN/TBD.

Giới hạn10MiB ảnh/25MiB document là config kỹ thuật demo theo đề xuất, chưa policy vận hành. PNG/JPEG subset, DOCX không macro/embedded OLE/external relationship, PDF signature/trailer validation; chưa antivirus/PDF sanitizer, không tự convert/native Docs. Dữ liệu thiếu không ghép version khác để làm counterpart. File Drive giữ khi soft-delete/cancel/Asset delete/archive; cleanup/retention thời hạn công ty chưa xác lập. Không camera/mobile/TASK-010. Local tests không đóng AC Google/browser, guard tree metadata giữ nguyên. [Thiết kế](../02-architecture/MEDIA-DESIGN-TASK-009.md).

## Lát local FR-07 TASK-010 — 2026-10-13

Theo ngoại lệ người dùng, local khiTASK-005–009 IN_REVIEW. Items dùng mapping20ordinal giữ code/hai mô tả/conversion/commercial/BOM reference, duplicate decision có lý do/identity/audit, Category DRAFT và QUARANTINED không global; không tự tạo Asset/ObjectCode. Snapshot nhập riêng có unit/asOf/source/actor/min nullable, quantity chưa có UNKNOWN không0; stockTracked cần tường minh và không cho dịch vụ/đơn vị thiếu. Không phê duyệt nguồn/rule/ngưỡng/Org-Location, fixture GIẢ chỉ test. [Thiết kế TASK-010](../02-architecture/INVENTORY-DESIGN-TASK-010.md). Local pass không đóng gate workbook/Google/browser, không triển khai báo cáo TASK-011.

## Lát local FR-08 TASK-011 — 2026-10-13

Người dùng cho phép local khi TASK-007/010 IN_REVIEW, gates/task trước giữ. Chỉ R01–R04: Assets unique count và Category subtree scoped, Item snapshots0/low/UNKNOWN/nontracked/missing-min/source rõ, sum riêng exact unit, filters/paging/readAsOf/per-snapshotAsOf. Nguồn/rule/ngưỡng/OrgLocation chưa chốt vẫn OPEN/TBD, DRAFT/demo không phê duyệt. CSV UTF8BOM được chọn trong Task, source/action scopes kiểm cùng assignment trước read/export; không R05–R15/stale-policy/MAUI. [Thiết kế](../02-architecture/REPORT-DESIGN-TASK-011.md), [Test Report](../07-testing/TEST-REPORT-TASK-011.md).

## TASK-012 local — 2026-10-13

Ngoại lệ user cho Android local khi TASK-005/007/009 IN_REVIEW, gates/task trước giữ. MAUI online-first dùng cùng backend: login/SecureStorage/rotation/logout/revoke, scoped Asset list/form/version/idempotency DRAFT_DEMO, camera/pick/preview/upload/retry. Không offline DB/rule mới/Google credential trong app hoặc tự phê duyệt DRAFT. AC-04/08/09 local42tests và Android compile có bằng chứng; device/UI/camera/OS permission/SecureStorage/TLS/Google NOT_RUN, không iOS. [Thiết kế](../02-architecture/MOBILE-ANDROID-DESIGN-TASK-012.md), [Report](../07-testing/TEST-REPORT-TASK-012.md).

## Quyết định ưu tiên — 2026-10-14

Người dùng chọn Web trước, Android sau, iOS DEFERRED_BY_USER vì thiếu môi trường/thiết bị. Không hủy kiến trúc mobile hoặc xóa code; gate mobile không chặn hoàn thiện Web. Cho phép tích hợp registry đã có vào tree mutation theo [thiết kế Web](../02-architecture/WEB-COMPLETION-DESIGN-TASK-014.md), không bypass guard/đổi scope/rule chính thức. Các gate hoãn trước giữ; không chạy Google thật/dữ liệu thật/UAT thủ công/timeout mới hoặc commit/push/deploy.
