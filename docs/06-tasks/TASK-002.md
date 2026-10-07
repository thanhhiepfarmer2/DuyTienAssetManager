# TASK-002 — Xác lập yêu cầu và hợp nhất tài liệu

- Status: DONE
- Type: DOCS
- Requirement/Plan: [REQ-001](../01-requirements/REQ-001-asset-digitization.md) / [PLAN-001](../02-architecture/PLAN-001.md)
- Created / Updated: 2026-10-06 / 2026-10-06

## Mục tiêu và phạm vi
Đọc gói `DuyTien_Asset_Requirements`, hợp nhất nội dung vào hệ tài liệu hiện tại,
giữ TASK-001 `DONE` và phân biệt quyết định sản phẩm với `PROPOSED`/`TBD`.
Chỉ thay đổi tài liệu; không code, không tạo Google resource/Git, không
commit/push và không sao chép đè các file append/addendum.

## Đầu vào, phụ thuộc và giả định
- Hướng dẫn có hiệu lực: `AGENTS.md` ở root; không có override/hướng dẫn lồng.
- Gói đầu vào: `C:\Users\May 03\Downloads\DuyTienReq-20261006-095830\DuyTien_Asset_Requirements`.
- Ba file nguồn doanh nghiệp được nêu trong `SOURCE-AUDIT.md` không nằm trong gói; TASK này kiểm tra manifest và tài liệu dẫn xuất, không tuyên bố đã phân tích lại file nguồn hay xác nhận hiệu lực ISO.
- Trước tích hợp chỉ có TASK-001, nên TASK-002–TASK-014 không cần cấp lại ID.

## Acceptance criteria
- [x] AC-01: Đọc hướng dẫn, trạng thái repo, README và toàn bộ tài liệu/dữ liệu trong gói; 29/29 hash khớp manifest.
- [x] AC-02: PRODUCT-BRIEF, PROJECT-CONTEXT, REQ-001, PLAN-001, Task và INDEX phản ánh đúng cây Excel nháp 15 F1, Sheets/Drive và web + app Android/iOS riêng.
- [x] AC-03: Các append/addendum được tích hợp có chọn lọc, không copy đè file authoritative; TASK-001 vẫn `DONE`, ID/link/phụ thuộc nhất quán.
- [x] AC-04: .NET/Razor/MAUI, host, Google target và các chi tiết chưa chốt vẫn là `PROPOSED`/`TBD`; không ghi secret nguyên văn hay tuyên bố có code/build/test app.
- [x] AC-05: Link local và dữ liệu staging được kiểm tra; giới hạn nguồn và tối đa ba câu cần làm rõ cho TASK-003 được ghi nhận.

## Routing đã dùng
- Skill: `conductor:context-driven-development` 1.0.0.
- Lý do: TASK là bước Context → Specification → Plan cho feature nhiều giai đoạn.
- Phạm vi áp dụng: đọc context trước, duy trì single source of truth, liên kết quyết định và đồng bộ Product/Requirement/Plan/Task.
- Điều chỉnh tương thích: không tạo `conductor/` vì dự án đã chọn `docs/` làm nguồn authoritative; không dùng subagent hay workflow triển khai code.

## Các bước
1. Đọc hướng dẫn/repo hiện tại và toàn bộ gói; kiểm tra hash, JSON và xung đột ID.
2. Sao chép 25 file mới; hấp thụ ba file append/addendum vào file hiện tại, không đưa bản trung gian vào repo.
3. Cập nhật nguồn chuẩn và trạng thái; sửa liên kết `INDEX.append` thành `INDEX.md`.
4. Kiểm tra link, quyết định/trạng thái, staging JSON, secret marker và phạm vi docs-only; self-review theo AC.

## Bằng chứng kiểm tra
| AC / kiểm tra | Lệnh hoặc thao tác | Working directory / môi trường | Kết quả | Bằng chứng |
|---|---|---|---|---|
| AC-01 | Đọc file bằng `Get-Content`; đối chiếu `Get-FileHash -Algorithm SHA256` với `SHA256.json` | Repo + gói local / PowerShell | PASS | Exit code 0; 29/29 file khớp; package README và mọi Markdown/JSON đã được đọc hoặc parse |
| AC-01/05 | Parse `category-candidates.json`, kiểm level/status/ID/parent/unique | Repo root / PowerShell | PASS | Exit code 0; 665 nút = 15/98/552; 665 DRAFT; 0 authoritative ID/orphan; 665 STT/full code duy nhất; 610 tên/39 nhóm nhiều mã khi phân biệt hoa thường |
| AC-02/03 | Đối chiếu Product/Context/REQ/Plan/TASK/INDEX và kiểm tra ID | Repo root / PowerShell | PASS | Exit code 0; 14 Task/14 ID duy nhất; TASK-001/002 = DONE; TASK-003–014 = TODO; 0 lệch trạng thái INDEX |
| AC-04 | Tìm marker `PROPOSED/TBD/DRAFT`, secret literal và dấu vết code/project | Repo root / PowerShell | PASS | Exit code 0; kỹ thuật chưa chốt còn marker; 0 secret assignment/private key/client secret; 0 app manifest |
| AC-05 | Phân giải link Markdown local từ thư mục file chứa link | Repo root / PowerShell | PASS | Exit code 0; 44 Markdown/90 link local, 0 link hỏng; 0 link tới file append/addendum |
| Build/test ứng dụng | Đối chiếu phạm vi và repo | Repo root | NOT_APPLICABLE | Docs-only, chưa có mã/project/test; không tuyên bố build/test pass |
| Phân tích lại 3 file nguồn | Kiểm tra thành phần gói | Gói local | NOT_RUN | File nguồn không nằm trong gói; chỉ lưu hash/kết quả audit đã cung cấp, không cản trở việc hợp nhất tài liệu |

## Review
Self-review theo acceptance criteria và nguồn quyết định. Không phát hiện xung đột
ID; ba file append/addendum không được sao chép vào repo. Các điểm kỹ thuật chưa
duyệt được giữ `PROPOSED`/`TBD`; thay đổi của TASK-001 và trạng thái `DONE` được
bảo toàn. Source audit được ghi đúng là tài liệu dẫn xuất, không nâng thành chứng
nhận cây đã ban hành.

## Checkpoint và phần còn lại
- Tài liệu cho toàn bộ roadmap đã được hợp nhất; chỉ TASK-002 được thực hiện.
- Chưa tạo code, Google resource, Git, commit/push; TASK-003–TASK-014 vẫn `TODO`.
- Các gate cần chốt tiếp nằm trong PROJECT-CONTEXT/REQ-001 và ba câu hỏi bàn giao cho TASK-003.

## Kết quả cuối
- Thêm Requirement/ADR/Plan/schema/API/UI/testing và TASK-002–TASK-014; cập nhật AGENTS, README, PRODUCT-BRIEF, PROJECT-CONTEXT và INDEX.
- Kiểm tra tài liệu: PASS; build/test ứng dụng: NOT_APPLICABLE.
- Trạng thái: DONE. Bước tiếp theo là TASK-003, không phải triển khai đồng thời toàn bộ roadmap.
