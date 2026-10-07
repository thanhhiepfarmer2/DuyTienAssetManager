# Chọn plugin/skill theo công việc

Danh sách do người dùng xác nhận đã cài. Bảng là gợi ý định tuyến của dự án, không phải chứng nhận nội dung/khả năng của từng bản plugin.
Không có cú pháp chung “gọi plugin”; cần chọn skill cụ thể từ phiên và đọc SKILL.md.

| Plugin @claude-code-workflows | Khi cân nhắc | Kiểm tra trước khi dùng |
|---|---|---|
| conductor | Phân tích yêu cầu, spec, plan cho feature nhiều bước | Skill có thật, quy trình/phân cấp tài liệu |
| ship-mate | Workflow thực hiện feature qua nhiều giai đoạn | Điều kiện Git/PR/QA; không tự push hoặc mở PR |
| agent-teams | Phân công công việc độc lập khi được yêu cầu | Runtime có subagent tools, skill tương thích, chủ sở hữu file |
| developer-essentials | Git, debug, tiện ích phát triển | Chọn đúng skill cho lỗi/công việc cụ thể |
| skill-forge-essentials | Chất lượng AI code và việc kéo dài qua nhiều phiên | Đọc hướng dẫn thật, chỉ áp dụng phần liên quan |
| dotnet-contribution | C#/.NET | Target framework, solution, test framework hiện có |
| backend-development | Thiết kế backend và logic phía server | Kiến trúc hiện có, scope Task |
| api-scaffolding | Hợp đồng/API endpoint | API đang có; không tạo GraphQL nếu chưa cần |
| database-design | Schema/ràng buộc/query | Database đã chọn; môi trường dev, migration |
| javascript-typescript | JS/TS khi repo dùng JS/TS | package.json, lockfile, framework thực tế |
| frontend-mobile-development | Web/mobile nếu công nghệ phù hợp | Không mặc định React Native thay MAUI |
| ui-design | Luồng giao diện, trạng thái và thiết kế | Yêu cầu người dùng, UI hiện có |
| security-scanning | Kiểm tra bảo mật khi thay auth/upload/quyền hoặc chuẩn bị release | Công cụ thực tế có sẵn, phạm vi dữ liệu, không upload code/data trái phạm vi |
| documentation-generation | Tài liệu kỹ thuật, ADR, API docs | Bản tài liệu authoritative; chỉ cập nhật phần liên quan |

## Ví dụ routing
- Thêm SerialNumber: nếu thay schema thì database skill + .NET skill; thêm web/mobile skill chỉ khi UI đó thuộc phạm vi. Quy tắc unique của serial phải được xác lập trước.
- Bàn giao tài sản: phân tích actor, trạng thái, quyền và việc trùng bàn giao; chọn workflow phân tích khi cần, chia Task/API/UI/test.
- Kiểm kê web/mobile: xác lập việc có offline/QR/đồng bộ hay không trước; lập plan nhiều Task, chọn một workflow thực thi chính.

## Bằng chứng sử dụng
Trong Task ghi plugin/skill đã đọc, lý do dùng và phạm vi áp dụng. Phân biệt “đề xuất dùng” với “đã dùng”; ghi rõ nếu phần agent/command của plugin không chạy trên Codex hiện tại.
