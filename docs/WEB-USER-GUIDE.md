> Runtime16/10: web mới đang chạy một instance tại https://localhost:7008; DuyTien-Web.cmd dùng lại instance đó. Không cần xóa cache hoặc reset tài khoản. UI cũ trong offline được đọc dưới dạng text chỉ đọc/asOf, mở online sẽ làm mới riêng trang đã xác minh. Tốc độ Google chưa đạt chứng cứ warm p95<500ms.

# Hướng dẫn giao diện web — PLAN-003 (2026-10-15)

Mở **DuyTien-Web.cmd**, dùng tài khoản trung tâm hiện có tại https://localhost:7008/Account/Login. Menu trái dẫn tới Danh mục, Tài sản, Hồ sơ, Vật tư, Số lượng và Báo cáo; phần quản trị chỉ hiện với quản trị viên. Menu trên màn hình hẹp có nút mở/đóng. Trang chủ chỉ là lối vào công việc, không hiển thị KPI chưa có nguồn.

Trong mỗi module, tìm/lọc rồi mở chi tiết. Cây hiển thị tối đa60 dòng/trang; nút thu gọn chỉ tác động các dòng đã tải. Bảng rộng có vùng cuộn ngang. Bộ lọc được giữ khi phân trang; xóa bộ lọc khi muốn trở về danh sách. Tab đang mở giữ vị trí cuộn và nhánh thu gọn theo đúng phiên/quyền. Không lưu dữ liệu nghiệp vụ hoặc mật khẩu vào sessionStorage.

Form đánh dấu trường sai, giữ nội dung và mã yêu cầu khi lỗi. Chờ trạng thái xử lý; không gửi lặp hoặc đổi nội dung của yêu cầu chưa rõ kết quả. Các thao tác nguy hiểm có xác nhận trước khi gửi, server vẫn kiểm lại quyền/phiên bản/CSRF. Upload có tên tệp, preview ảnh/PDF khi hỗ trợ và có thể hủy lựa chọn trước khi gửi. Chỉ thông báo lưu đã xác nhận mới coi hoàn tất.

Dữ liệu chưa duyệt/kiểm thử/nguồn chưa xác nhận giữ nhãn riêng, không là tên ứng dụng. Nguồn/rule/đơn vị/địa điểm/ngưỡng OPEN/TBD không tự phê duyệt. Trang Hướng dẫn trong web mô tả luồng đầy đủ, ngoại tuyến chỉ đọc, reconnect và xử lý conflict/thiếu cache. Bằng chứng cập nhật16/10: đã quan sát browser Home/Danh mục và login trung tâm hiện có, nhưng các module/narrow/download/network còn gate; xem Report021. Không coi HTTP rendered là browser pass. Phần lịch sử bên dưới giữ nguyên theo phiên bản, không dùng hướng dẫn v1 cho mô hình hiện hành.

---

# Web hiện hành — online mặc định / ngoại tuyến chỉ đọc (2026-10-15)

Mở **DuyTien-Web.cmd** (DuyTien-LocalOffline.cmd cũ cũng chuyển tới cùng launcher), URL https://localhost:7008/Account/Login. Dùng tài khoản trung tâm hiện có, gồm admin Google DEV với mật khẩu đang dùng; không LocalSetup hoặc admin riêng. Backend phải còn chạy trên máy.

Sau khi đăng nhập online, các trang/query/file đã đọc thành công được lưu riêng cho phiên. Mở launcher khi mất Internet sẽ tự tới /Offline nếu cookie/proof còn hạn; chưa có hoặc hết hạn thì cần kết nối online. Mất Internet đã xác định: chỉ đọc, có banner Ngoại tuyến — chỉ đọc và asOf từng trang. `/Offline` tìm không dấu/lọc module và phân trang60 trong các snapshot đã lưu. Không là bản sao toàn bộ dataset; query mới hoặc file chưa tải bytes báo thiếu. AVAILABLE là trạng thái file trung tâm tại asOf, nhãn Có offline/Chưa có file offline nói rõ bytes tại máy.

Có mạng lại: tự xác minh phiên/quyền và tải mới trang nghiệp vụ, rồi mới bật ghi. Hết hạn/revoke/khóa cần đăng nhập online lại. Quyền đổi loại cache cũ. Form đã nhập được giữ; version conflict vẫn cần xử lý theo dữ liệu mới. Nếu kết quả ghi chưa rõ: giữ form/key/payload; nút **Đối soát kết quả ghi** đọc journal và dùng reconcile media có quyền khi cần, không tự upload lại. Chỉ sau journal xác nhận mới retry form cũ; thiếu key/journal/bytes thì tiếp tục chặn, không đổi key để lách.

Đăng xuất online/offline làm mất khả năng truy cập offline của phiên trên máy. Chưa từng có phiên online hợp lệ thì không đăng nhập offline. Android dừng, iOS hoãn. Đây là hướng dẫn dùng, không yêu cầu thêm UAT thủ công.

---
## Hướng dẫn lịch sử v1

# Hướng dẫn web demo Duy Tiến

## LocalOffline trên Windows — 2026-10-14

Chạy `DuyTien-LocalOffline.cmd` sau khi đã chuẩn bị Release output một lần; launcher mở https://localhost:7008, chạy backend local không Internet/build/restore mỗi lần. Mode/workspace hiện đầu trang. Backend phải còn chạy; đây không là HTML độc lập/PWA đọc khi backend đã dừng. Tài nguyên UI đi kèm trên máy, không CDN.

Workspace mới: mở liên kết **Khởi tạo workspace mới**, đặt mật khẩu admin riêng qua ô password, đăng nhập và đổi mật khẩu bắt buộc. Không có password mặc định hoặc tài khoản Google được copy. Sau đó **Quản trị → Demo GIẢ local** để chủ động tạo3nút DRAFT/đơn vị/địa điểm/rule LOCAL GIẢ nếu muốn sử dụng demo ngay. Không tự seed20744Items hoặc nguồn HTML. Dữ liệu có trước khác fixture sẽ bị chặn, không overwrite.

Các luồng danh mục, tài sản, import Items20cột, snapshot, ảnh/DOCX/PDF và R01–R04 dùng cùng API/quyền/validation. Chưa có tồn vẫn UNKNOWN; DRAFT/demo chưa chính thức. Giữ key/form/version khi lỗi,409 cần reload có chủ ý, không đổi key để retry. Báo lưu thành công chỉ sau durable commit. Cây hiển thị60nút/trang; Assets/Items/report phân trang; browser chỉ lưu vị trí cuộn theo workspace/user/session, không cache dataset hoặc credential.

Google mode giữ riêng cho quy trình chủ động về sau. Chưa có migration Google→local được chạy/chứng minh; không tự dùng Drive metadata nếu thiếu bytes. Workbook/rule/OrgLocation/ngưỡng OPEN/TBD không đổi.


Checkpoint 2026-10-14: đã nối Google DEV và đang kiểm tích hợp thật bằng dữ liệu giả; xem [report hiện hành](07-testing/TEST-REPORT-TASK-014-GOOGLE-DEV.md). Browser và nguồn/rule công ty chưa nghiệm thu. Đây là hướng dẫn sử dụng, không phải yêu cầu người dùng thực hiện UAT. Web được ưu tiên; mobile tạm hoãn.

1. Đăng nhập bằng tài khoản ứng dụng và đổi mật khẩu khi được yêu cầu. Mật khẩu tối thiểu 8 ký tự. Viewer tra cứu; AssetEditor quản lý tài sản/ảnh; CatalogManager quản lý cây/Items/snapshot; DocumentManager quản lý hồ sơ; Admin quản trị. Mỗi hành động còn phụ thuộc assignment và scope được server kiểm tra.
2. Trang chủ và menu nối Danh mục, Tài sản, Hồ sơ, Items, Snapshot, R01–R04 và Quản trị. Chi tiết danh mục liên kết đến tài sản, Items và báo cáo trong nhánh; chi tiết tài sản liên kết đến ảnh, danh mục và R01. Liên kết không thay thế kiểm quyền.
3. Import cây theo luồng file → preview/diff/tên nhiều mã → xác nhận DRAFT. Workbook và rule chưa xác minh giữ OPEN/TBD. Full import không được làm mất danh mục đang có owner hoặc thay cấu trúc/trạng thái của nhánh đó; dùng preview sửa/chuyển/ngừng để đối soát registry.
4. Tạo/sửa tài sản demo bằng các choices trong phạm vi được cấp. ID và mã do server cấp hoặc bảo toàn. Khi 409, tải bản đã lưu hoặc preview mới và kiểm lại nội dung trước yêu cầu mới. Khi 503/mất mạng, giữ payload, version và key để retry/đối soát cùng yêu cầu.
5. Ảnh tài sản dùng PNG/JPEG; hồ sơ dùng DOCX/PDF cùng DocumentVersion. Chỉ AVAILABLE xác nhận upload thành công; PENDING/FAILED cần retry/đối soát fileId. Bản PUBLISHED_DEMO có nội dung bất biến và ngày hiệu lực UTC. JavaScript giữ File khi lỗi; sau reload hoặc khi không có JavaScript, chọn lại đúng bytes cũ và giữ key/version. Không xóa file Drive vật lý để xử lý lỗi.
6. Import Items theo mapping/quyết định mã trùng → preview → staging/job → tiếp tục các chunk đến COMPLETE. Hoàn tất job trước khi tạo tree version mới. Item là bản ghi danh mục, không phải từng Asset. Nhập snapshot riêng từ chi tiết Item với unit/asOf/source tường minh; quantity rỗng là UNKNOWN, minimum rỗng là chưa có ngưỡng. Form cập nhật nạp giá trị/version hiện hành và giữ nội dung đã gửi khi lỗi. Không suy tồn từ BOM/kho hoặc gán unknown thành 0.
7. Sửa/chuyển/ngừng cây luôn có preview owner/registry/scope trước commit. RELOCATE giữ ID, mã, 20 cells nguồn, stock và bytes; thay phân loại với version/audit. EMPTY_ONLY kiểm cả lịch sử. SOFT_DELETE không tự xóa Item khi chưa có semantics được duyệt; nhánh có Item cần RELOCATE. Schema không rõ/thiếu, owner mồ côi, job dở hoặc batch quá lớn cần đối soát, không xóa tab hay bypass guard.
8. R01/R02 đếm Assets riêng; R03/R04 phân biệt tồn 0, thấp, UNKNOWN, không theo dõi tồn và thiếu ngưỡng/nguồn. Bộ lọc, phân trang, tổng và CSV áp dụng scope trước tính toán. Không cộng số lượng khác đơn vị. asOf thể hiện snapshot hiện hành, không phải truy vấn lịch sử/định giá.

Trang `/Help` có hướng dẫn trong web. 403 báo thiếu quyền; 404 có thể là ngoài phạm vi; 422 báo validation; 409 báo phiên bản cũ; 503 báo dịch vụ chưa xác nhận kết quả. Việc ẩn nút không chứng minh quyền. Không gửi password/token/cookie vào chat hoặc audit.

Giới hạn hiện tại nằm trong [Test Report](07-testing/TEST-REPORT-TASK-014-WEB.md). TLS blocked là checkpoint cũ; chứng chỉ hiện đã trust và strict HTTPS đạt. Bằng chứng Google từng module được ghi riêng trong report mới; browser, nguồn/rule và restore thật vẫn còn gate.
