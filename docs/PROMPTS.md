# Prompt làm việc

Anh chỉ cần nêu yêu cầu. Codex tự dùng template và lưu Task; không cần nhắc tên tất cả plugin.

## Lần đầu sau khi cài bộ file
```text
Thực hiện TASK-001 bootstrap trong dự án này. Đọc hướng dẫn có hiệu lực, docs/00-bootstrap/installation-report.json nếu có và các file proposed để hợp nhất với tài liệu đang có. Giữ code và thay đổi của tôi. Kiểm tra repo, skill thực tế của phiên và cập nhật PROJECT-CONTEXT, COMMANDS, INDEX bằng dữ kiện thật. Phần chưa xác lập giữ TBD. Chỉ làm thiết lập quy trình và tài liệu, chưa code tính năng nghiệp vụ, chưa commit/push. Kiểm tra kết quả, báo cáo bằng chứng và điểm còn cần làm rõ.
```

## Xác lập demo khi repo chưa có yêu cầu
```text
Đọc AGENTS.md và tài liệu hiện có. Giúp tôi xác lập phạm vi demo quản lý tài sản. Đầu tiên tổng hợp điều đã biết; hỏi tối đa 3 câu ảnh hưởng trực tiếp đến demo đầu tiên. Dùng câu trả lời của tôi cập nhật PRODUCT-BRIEF, đề xuất một lát chức năng đầu tiên và lập Requirement/Plan/Task. Chưa tự chốt web/mobile/database nếu chưa có quyết định.
```

## Giao một feature
```text
Tôi muốn [chức năng], để [người dùng] có thể [kết quả]. Quy tắc cần tuân theo: [...]. Mẫu dữ liệu làm sạch: [...]. Đọc repo và tài liệu liên quan, tự tạo/cập nhật Requirement và Task. Nếu đủ thông tin, triển khai từng Task, kiểm thử và review. Chỉ hỏi những điểm nghiệp vụ/kiến trúc còn thiếu ảnh hưởng trực tiếp. Báo cách tôi chạy thử và bằng chứng test.
```

## Ví dụ yêu cầu đang được đề xuất, chưa giao thực thi
```text
Làm chức năng xem danh sách và thêm tài sản trên giao diện thuộc phạm vi demo đã xác lập. Các trường và quy tắc lấy từ Requirement hiện hành. Nếu chưa có quy tắc mã tài sản, hỏi tôi trước khi viết phần sinh mã. Dùng dữ liệu giả và môi trường dev.
```

## Sửa lỗi
```text
Lỗi: [...]. Bước tái hiện: [...]. Mong đợi: [...]. Thực tế: [...]. Log đã làm sạch: [...]. Tìm nguyên nhân, tạo Task sửa trong phạm vi lỗi, thêm kiểm tra hồi quy có ý nghĩa và chạy các kiểm tra liên quan. Không đổi kiến trúc nếu chưa cần.
```

## Tiếp tục
```text
Đọc INDEX, PROJECT-CONTEXT, Task chưa hoàn thành và git diff liên quan. Tiếp tục Task hiện tại từ checkpoint, kiểm tra lại điều còn chưa rõ. Không làm lại các bước đã có bằng chứng hợp lệ. Nếu bị chặn, làm các phần độc lập và báo nguyên nhân.
```

## Chỉ review
```text
Chỉ review Task [ID] và diff liên quan; chưa sửa mã. Đối chiếu acceptance criteria và bằng chứng test, kiểm tra lỗi/quyền/dữ liệu. Ghi findings có vị trí cụ thể và mức ảnh hưởng. Phân biệt test đã chạy với test chỉ được đề xuất.
```
