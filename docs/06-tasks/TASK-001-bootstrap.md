# TASK-001 — Kiểm tra repo và tích hợp workflow

- Status: DONE
- Type: DOCS / SETUP
- Requirement/Plan: yêu cầu trực tiếp của người dùng ngày 2026-10-05; không cần Plan riêng vì đây là một lát thiết lập tài liệu ngắn.
- Created / Updated: 2026-10-05 / 2026-10-05

## Mục tiêu và phạm vi
Đọc repo local, đối chiếu report/proposal, tích hợp `AGENTS.md`, `README.md` và
tài liệu authoritative; cập nhật bối cảnh, lệnh và danh sách Task bằng dữ kiện
thật. Không tạo mã nghiệp vụ, không khởi tạo Git, không commit/push và không chốt
các quyết định đang là TBD.

## Đầu vào, phụ thuộc và giả định
- Hướng dẫn có hiệu lực: `AGENTS.md` ở root; không có `AGENTS.override.md` hoặc hướng dẫn lồng khác.
- Report bootstrap mới nhất: `docs/00-bootstrap/installation-report.json`, `created_utc` = `2026-10-05T09:16:12.8941855Z`.
- Report ghi toàn bộ file là `CREATE`; đủ 20/20 file có SHA-256 khớp trước khi tích hợp. Không có thư mục `docs/00-bootstrap/proposed/` vì không có collision.
- `TASK-001` chính là Task bootstrap ở trạng thái TODO và chưa được dùng cho công việc khác, nên không cấp ID mới.

## Acceptance criteria
- [x] AC-01: Đọc cấu trúc repo, hướng dẫn có hiệu lực và trạng thái Git nếu có; giữ nguyên code/thay đổi cũ.
- [x] AC-02: Tích hợp nội dung đề xuất vào hướng dẫn authoritative; không có `AGENTS.override.md` làm bộ mới vô hiệu.
- [x] AC-03: Đối chiếu tài liệu sẵn có, điền PROJECT-CONTEXT bằng dữ kiện thật; chưa rõ giữ TBD.
- [x] AC-04: Tìm skill thực tế của phiên, đọc phần liên quan; ghi năng lực thiếu hoặc không tương thích.
- [x] AC-05: Điền COMMANDS từ project/scripts thật. Vì chưa có code, ghi rõ NOT_APPLICABLE cho build/test ứng dụng.
- [x] AC-06: Kiểm tra link nội bộ, không có nguồn spec trùng/mâu thuẫn, báo cáo diff và cách tiếp tục.

## Routing đã dùng
- Skill: `ship-mate:scan` từ plugin `ship-mate` 1.0.1.
- Lý do: skill dành cho bootstrap/scan repo phục vụ agent.
- Phạm vi áp dụng: kiểm tra scan mode và dùng native fallback để rà file/cấu trúc vì `understand-anything` và `context-mode` không có trong danh sách năng lực phiên.
- Điều chỉnh tương thích: không tạo `.claude/pipeline/project-doc.md` hoặc ghi đè `AGENTS.md`; repo đã có hệ tài liệu authoritative trong `docs/` từ yêu cầu người dùng, chưa có code để sinh mô tả kiến trúc, và việc tạo nguồn song song sẽ trái quy tắc dự án về tránh spec trùng.

## Các bước
1. Đọc hướng dẫn, context, workflow, routing, Task/INDEX và report mới nhất.
2. Kiểm tra toàn bộ file/report bằng SHA-256, Git, cấu trúc và công cụ môi trường.
3. Giữ `AGENTS.md` vì khớp proposal và phù hợp; cập nhật README/tài liệu trạng thái bằng dữ kiện thật.
4. Kiểm tra link, placeholder hợp lệ, tính nhất quán Task/INDEX và rà soát thay đổi cuối.

## Bằng chứng kiểm tra
| AC / kiểm tra | Lệnh hoặc thao tác | Working directory / môi trường | Kết quả | Bằng chứng |
|---|---|---|---|---|
| AC-01 | `rg --files -uu` và kiểm tra Git có điều kiện | Repo root / Windows | PASS | Exit code 0; 20 file payload + 1 report; `NOT_A_GIT_WORKTREE`; không có code để ảnh hưởng |
| AC-02/03 | Đọc `installation-report.json`, đối chiếu existence + SHA-256 | Repo root / PowerShell | PASS | Exit code 0; 20/20 file tồn tại và hash khớp report trước tích hợp; action đều là `CREATE` |
| AC-04 | Đọc `ship-mate:scan/SKILL.md` thực tế | Skill cache / phiên Codex | PASS | Skill bản 1.0.1; dùng native fallback; hai optional enhancement không có trong phiên |
| AC-05 | `dotnet --info`; `codex --version`; tìm manifest bằng `rg --files` | Repo root / Windows | PASS | Exit code 0; SDK 10.0.400, Codex 0.160.0; không có project/manifest |
| Build/test ứng dụng | Đối chiếu repo | Repo root | NOT_APPLICABLE | Docs-only, chưa có mã nguồn/project/test; không tuyên bố build/test pass |
| AC-06 | Phân giải link Markdown local bằng script PowerShell chỉ đọc; đối chiếu trạng thái Task/INDEX và nguồn authoritative | Repo root / PowerShell | PASS | Exit code 0; 20 file Markdown, 15 link local đã kiểm tra, 0 link hỏng; Task/INDEX cùng trạng thái DONE; không tạo spec song song |
| Giữ nguyên ngoài phạm vi | Đối chiếu SHA-256 cuối với report | Repo root / PowerShell | PASS | 20/20 payload còn tồn tại; 14 file giữ nguyên hash, đúng 6 file tài liệu chủ đích thay đổi; `AGENTS.md` vẫn khớp report |

## Review
Self-review theo acceptance criteria. `AGENTS.md`, template, requirement draft và
report gốc được giữ nguyên; chỉ cập nhật tài liệu có dữ kiện trạng thái thay đổi.
Chưa phát hiện xung đột nghiệp vụ vì không có feature/spec đã duyệt. Kiểm tra cuối
không có link hỏng; các mục TBD/PROPOSED còn lại đều mô tả đúng phần chưa được
xác lập, không bị trình bày thành quyết định.

## Checkpoint và phần còn lại
- Đã xác minh proposal chính là 20 file hiện có; không có file cũ bị ghi đè hay thư mục proposed cần hợp nhất.
- Đã kiểm tra link nội bộ, marker TBD/PROPOSED và đồng bộ trạng thái Task/INDEX.
- Các quyết định nghiệp vụ, stack, database, Git/remote và phạm vi demo tiếp tục là TBD; đây không phải blocker của Task docs-only.

## Kết quả cuối
- Cập nhật: `README.md`, `docs/PROJECT-CONTEXT.md`, `docs/COMMANDS.md`,
  `docs/02-architecture/README.md`, `docs/06-tasks/INDEX.md` và Task này.
- Giữ nguyên: `AGENTS.md`, report gốc, PRODUCT-BRIEF, WORKFLOW, PLUGIN-ROUTING,
  PROMPTS, các template và tài liệu domain chưa có dữ kiện mới.
- Build/test ứng dụng: NOT_APPLICABLE vì repo docs-only; kiểm tra tài liệu: PASS.
- Trạng thái: DONE. Bước tiếp theo là xác lập phạm vi demo trong PRODUCT-BRIEF,
  không phải bắt đầu code toàn bộ hệ thống.
