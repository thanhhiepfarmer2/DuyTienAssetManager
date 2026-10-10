# Màn hình dự kiến

Web: đăng nhập/đổi mật khẩu; dashboard report; cây danh mục (tree+detail); danh sách tài sản (filter+grid); form tài sản; chi tiết+ảnh+hồ sơ+lịch sử; import preview; snapshot số lượng; hồ sơ tài liệu/biểu mẫu+versions; admin users/roles/scopes; TXT export.
Ứng dụng Android/iOS riêng bằng .NET MAUI: đăng nhập; tìm/lọc/list; chi tiết; tạo/sửa trong scope; chụp ảnh/chọn thư viện/preview/retry; xem mẫu PDF/tải DOCX theo khả năng thiết bị; report cơ bản. Quản lý hàng loạt cây/import/user ưu tiên web Razor Pages, thêm vào mobile chỉ khi Task cần.
UI ẩn/disable thao tác theo quyền, nhưng API vẫn là nơi enforce. Có loading/empty/error/forbidden/conflict và trạng thái DRAFT/UNKNOWN/FAILED rõ.
Thao tác xóa/chuyển cây có màn preview đếm children/assets/docs và phạm vi; giữ ID hiện rõ, lý do bắt buộc cho bulk.
Mobile online-first, giữ draft form trong phiên khi lỗi mạng; không tuyên bố offline sync. File tạm được cleanup, không lưu Google secret/cache nhạy cảm tùy ý.
Ngôn ngữ: tiếng Việt; chưa có yêu cầu song ngữ cho ứng dụng này.

TASK-006 local đã có `/Categories` (tree/tìm/empty/error/TXT), `/Categories/Detail/{id}` (path chỉ trong scope/definition/provenance), `/Categories/Import` (upload→preview→diff/duplicate names→confirm DRAFT→commit; conflict/loading). Razor có antiforgery/no-store và cùng service API. HTML render/POST tự động pass; browser desktop/mobile/JS loading chưa nghiệm thu, không coi là browser UAT.
