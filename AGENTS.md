# QuanLyTaiSan_DuyTien — hướng dẫn cho Codex

## Mục tiêu và nguồn yêu cầu
- Hỗ trợ Nguyễn Thanh Hiệp phát triển phần mềm quản lý tài sản Duy Tiến từng chức năng nhỏ, có kiểm thử và bằng chứng.
- Giao tiếp và tài liệu nghiệp vụ dùng tiếng Việt. Giữ tên mã nguồn theo quy ước hiện có.
- Người dùng được giao việc bằng ngôn ngữ tự nhiên. AI tự chuyển thành Requirement/Task; không bắt người dùng điền template.
- Tuân thủ chỉ dẫn cấp cao hơn và yêu cầu hiện tại của người dùng. Khi tài liệu dự án mâu thuẫn về nghiệp vụ, nêu rõ mâu thuẫn trước khi triển khai phần phụ thuộc.
- Tham chiếu gốc: docs/PROJECT-CONTEXT.md, docs/01-requirements/PRODUCT-BRIEF.md và yêu cầu liên quan. Các mục DRAFT, PROPOSED, TBD là thông tin chưa được xác lập, không phải nghiệp vụ đã duyệt.

## Ràng buộc sản phẩm đã xác lập
- Requirement authoritative hiện tại là `docs/01-requirements/REQ-001-asset-digitization.md`; đọc cùng `SOURCE-AUDIT.md` và các quyết định mới hơn của người dùng.
- Cây demo lấy từ Excel hiện tại gồm 15 nhóm F1 và vẫn ở trạng thái `DRAFT`; không âm thầm thay bằng cây 13 F1 trong DOCX, không coi STT/row/mã đoạn là ID hệ thống.
- Google Sheets là CSDL nghiệp vụ, Google Drive công ty lưu ảnh/DOCX/PDF. Không tự thay CSDL chính bằng SQL/SQLite; adapter giả chỉ dùng cho test và phải ghi rõ giới hạn bằng chứng.
- Sản phẩm có web và ứng dụng Android/iOS riêng, không coi web responsive là đã hoàn thành mobile. ADR-001 đã chốt ASP.NET Core .NET 10, Razor Pages, .NET MAUI và một backend instance cho demo; nhà cung cấp host/domain vẫn chưa xác lập.
- Không tự nhập nhân sự thành tài khoản, Item thành Asset, BOM thành tồn kho hoặc số lượng chưa biết thành 0.
- Bí mật khởi tạo admin phải đi qua cấu hình ngoài repo; không ghi giá trị nguyên văn vào tài liệu, mã nguồn, client, log hay build artifact.
- Task tích hợp Google và iOS chỉ được `DONE` khi có bằng chứng trên môi trường tương ứng; fake adapter hoặc Android pass không chứng minh Google/iOS pass.

## Bắt đầu mỗi công việc
1. Xác định thư mục gốc và đọc hướng dẫn có hiệu lực, kể cả AGENTS.override.md và hướng dẫn gần file sẽ sửa.
2. Kiểm tra git status nếu có Git, cấu trúc mã, tài liệu và công cụ sẵn có. Giữ nguyên thay đổi của người dùng.
3. Đọc docs/PROJECT-CONTEXT.md, docs/06-tasks/INDEX.md và tài liệu trực tiếp liên quan; không nạp toàn bộ docs/plugin cache.
4. Tóm tắt mục tiêu, phạm vi, điều kiện nghiệm thu và chọn đường xử lý theo docs/WORKFLOW.md.
5. Dùng docs/PLUGIN-ROUTING.md để tìm skill phù hợp trong danh sách của phiên; đọc SKILL.md thực tế trước khi áp dụng. Plugin name không phải tên lệnh hay skill.

## Tự chủ và làm rõ
- Tự làm các thao tác thông thường trong phạm vi đã giao: đọc, tạo/cập nhật tài liệu và Task, sửa mã, chạy build/test trên môi trường dev.
- Không xin xác nhận lặp lại cho việc người dùng đã yêu cầu hoặc lựa chọn đã xác lập.
- Chọn chi tiết kỹ thuật nhỏ theo quy ước hiện có; ghi giả định khi cần. Chỉ hỏi khi thông tin thiếu ảnh hưởng đáng kể đến nghiệp vụ, quyền, dữ liệu hoặc kiến trúc.
- Trong lúc chờ câu trả lời, tiếp tục các phần độc lập. Nếu yêu cầu chỉ là tư vấn, không tự triển khai mã.
- Không tự mở rộng module; không tự chốt web framework, mobile framework, database hay cách triển khai chỉ từ plugin đã cài.

## Điều phối có điều kiện
- Công việc nhỏ: chọn trực tiếp skill cần thiết, một Task ngắn; không thêm nhiều vòng điều phối.
- Feature cần phân tích: dùng năng lực phù hợp của conductor nếu có, lập spec/plan rồi thực hiện các Task nhỏ.
- Feature nhiều lớp: chỉ chọn một workflow thực thi chính sau khi đọc skill; có thể dùng ship-mate nếu thực tế phù hợp.
- Không chạy hai orchestrator đồng thời, không nạp cả 14 plugin cho một Task. Conductor và ship-mate có thể được dùng ở hai giai đoạn rõ ràng, không tự mặc định phải dùng cả hai.
- Chỉ dùng subagents khi người dùng yêu cầu hoặc skill áp dụng yêu cầu, và runtime có công cụ hỗ trợ. Cài agent-teams không chứng minh rằng đã bật hay đã chạy multi-agent. Nếu không hỗ trợ, thực hiện tuần tự và nói rõ.
- Khi có subagents: giao phạm vi, đầu vào, đầu ra và chủ sở hữu file; tránh hai agent sửa cùng file; agent chính tích hợp và chạy kiểm tra cuối.
- Skill thiếu/không tương thích: ghi rõ, dùng quy trình tương đương phù hợp; không giả vờ đã gọi plugin, agent hoặc công cụ.

## Requirement → Plan → Task → Code → Test → Review
1. Yêu cầu mới: tạo/cập nhật tài liệu theo docs/templates/REQUIREMENT.template.md, ghi nguồn và phần chưa rõ. Đổi hành vi đã xác lập thì cập nhật Requirement trước hoặc cùng thay đổi mã.
2. Tạo plan cho feature nhiều bước theo docs/templates/PLAN.template.md. Có thể bỏ plan riêng cho sửa nhỏ.
3. Tạo Task theo docs/templates/TASK.template.md trong docs/06-tasks/ và cập nhật INDEX.md. Dùng ID tăng dần TASK-001...; không ghi đè Task cũ.
4. Thực hiện một lát chức năng kiểm thử được từ đầu đến cuối. Nếu chỉ tạo tài liệu, tiêu chí phải là kiểm tra tài liệu, không giả build ứng dụng.
5. Chạy các lệnh thực tế đã ghi trong docs/COMMANDS.md. Nếu TBD, tìm từ solution/project, package scripts, CI và cập nhật lệnh chính xác trước khi chạy.
6. Ghi exit code, tổng test/failed/skipped khi có, kiểm tra bằng tay và hạn chế vào Task hoặc docs/07-testing/.
7. Review diff theo acceptance criteria, lỗi, quyền truy cập và tính nhất quán; sửa lỗi trong phạm vi rồi kiểm tra lại phần bị ảnh hưởng.
8. Cập nhật Task/INDEX, docs/PROJECT-CONTEXT.md nếu trạng thái dự án đổi, và báo cáo ngắn có bằng chứng.

## Quy tắc mã, dữ liệu và kiểm thử
- Giữ kiến trúc, framework, lockfile và SDK đã xác lập; thay đổi lớn phải có ADR. Không dựng microservices hoặc hạ tầng mới khi chưa có nhu cầu.
- Xác thực và phân quyền phải được kiểm tra phía server; không dùng việc ẩn nút UI thay cho kiểm tra quyền.
- Kiểm tra đầu vào, unique constraint và đồng thời ở tầng thích hợp. Dùng transaction cho nghiệp vụ nhiều thao tác cần nguyên tử.
- Không ghi secret/connection string có mật khẩu vào repo hoặc log. Dùng dữ liệu giả/đã làm sạch cho demo và test.
- Không tải dữ liệu doanh nghiệp, log chứa dữ liệu thật hay tài liệu nhạy cảm tới công cụ/dịch vụ bên ngoài khi chưa có phạm vi cho phép.
- Nếu cần sử dụng dữ liệu thật với AI/cloud, làm rõ phạm vi dữ liệu được phép trước khi đọc hoặc gửi. Việc ứng dụng chạy nội bộ không làm AI cloud trở thành offline.
- Với thay đổi .NET: build project/solution bị ảnh hưởng và chạy test liên quan/kiểm tra hồi quy thích hợp. Logic nghiệp vụ mới cần test hành vi thành công, lỗi và biên theo rủi ro; ưu tiên framework test hiện có.
- API thay đổi cần integration test phù hợp, đặc biệt validation và authorization. UI thay đổi cần kiểm tra tương tác, trạng thái lỗi/loading/empty và layout phù hợp.
- Mobile chỉ báo đã kiểm thử nền tảng thực sự chạy được; Android pass không chứng minh iOS pass.
- Không tạo test chỉ để lặp lại implementation hoặc cho chỉnh sửa tài liệu nhỏ. Không xóa, skip hoặc làm yếu test chỉ để pass.
- Test không chạy được phải ghi NOT_RUN kèm nguyên nhân và lệnh cần chạy; test suite không có test không được xem là đã chứng minh tính đúng của phần mềm.

## Trạng thái và Done
- TODO → IN_PROGRESS → IN_REVIEW → DONE; dùng BLOCKED khi có phụ thuộc chưa giải quyết.
- DONE chỉ khi acceptance criteria đạt, các kiểm tra bắt buộc đã pass, không còn lỗi thuộc phạm vi và tài liệu liên quan cập nhật.
- Nếu test/build bắt buộc chưa chạy, giữ IN_REVIEW hoặc BLOCKED, không ghi DONE. UAT do người dùng cần thực hiện thì cũng chưa DONE khi UAT là tiêu chí bắt buộc.
- Báo cáo cuối: Task ID; thay đổi; cách chạy/kiểm tra; kết quả; điều chưa xác minh; bước tiếp theo. Không báo “đã review bảo mật” nếu chỉ đọc checklist.

## Git và hành động bên ngoài
- Không reset hard/clean/xóa thay đổi của người dùng. Không tự commit, push, mở PR hay deploy khi chưa được giao việc đó.
- Nếu chưa có Git, ghi trạng thái và đề xuất khởi tạo; việc thiết lập workflow không tự tạo remote hoặc xuất dữ liệu lên GitHub.
- Migration có thể tạo trong dev; trước khi apply phải biết rõ đích là dev/test dùng cho Task. Thay đổi/xóa dữ liệu thật, force push hoặc deploy sản xuất cần phạm vi cho phép riêng nếu chưa được giao rõ.
- Giữ sandbox/approval của runtime; AGENTS.md không thay thế cấu hình quyền và không cho phép bỏ qua cơ chế bảo vệ.
