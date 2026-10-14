# TASK-014 — Pha Web: tích hợp registry và hoàn thiện luồng

Ngày2026-10-14, theo chỉ đạo mới: Web trước → Android sau → iOS DEFERRED_BY_USER do chưa có môi trường/thiết bị. TASK-012 IN_REVIEW giữ local42tests/build; phát triển/runtime mobile tạm hoãn. Gate mobile không chặn pha Web; các gate Google/nguồn/rule/UAT/timeout đã hoãn vẫn giữ. Không Google live/real writes/SQL/deploy/commit/push.

## Danh sách thiếu/lỗi ghi nhận trước triển khai

1. Trang chủ vẫn mô tả scaffold TASK-003; menu Items/Snapshot/Reports xuất hiện cả khi anonymous, collapse target/aria không đồng bộ; chưa có luồng hướng dẫn/gate rõ.
2. Category detail chưa nối tài sản/Items/report trong nhánh; Asset/detail/photo và import/snapshot cần điều hướng thống nhất, giữ filter/context.
3. GoogleTreeMutationStore chặn mọi Items/StockBalances/Files/Documents theo guard TASK-008; hiện các module đã có schema/ledger nhưng chưa tích hợp impact/CAS/commit. Không tắt guard; thay nguồn đánh giá từ hiện diện tab thành đối soát đầy đủ.
4. Category import chỉ đối soát Asset/Item; Document và ảnh thừa kế scope có nguy cơ mất reference khi full import thay cây. Phải kiểm metadata registry và chặn thay ancestor/state/mất Category có owner trước import.
5. Các trạng thái POST/409/503/empty/loading/giữ form và callback preview cần kiểm tích hợp/bằng chứng UI. Browser nếu không có provider thì ghi NOT_RUN, không thay bằng HTTP rendered.

## Registry/tree — phương án kỹ thuật trước code

- Production joint store đọc Categories/Assets/Inventory/Media cùng shared writer; từng adapter kiểm schema/header/journal/materialized/audit. Metadata chỉ chấp nhận các bảng managed biết rõ. Partial/unknown schema, thiếu adapter cho registry hiện hữu, owner/Category/Location mồ côi hoặc invalid dữ liệu đều fail-closed trước preview/write. Bảng thực sự vắng được xác minh bằng metadata, không giả row zero.
- Snapshot/digest preview bao gồm revision và nội dung Inventory/Media. Commit đọc lại, so cả bốn revision với checkpoint, cùng actor/payload/key, không chỉ tree/asset. Preview chỉ có impacts đã qua quyền; không trả cells nguồn/qty/Drive ID/checksum/raw ledger. Quyền mọi owner cả trước/sau, nhiều assignment không ghép phạm vi.
- EDIT/MOVE cây giữ ItemId/source code/20cells/StockBalance/FileId/DriveId/DocumentVersion và Asset ObjectCode. Item/Document giữ CategoryId khi chỉ đổi hierarchy. Stock/file thừa kế owner theo cây hiện tại, không tạo scope riêng.
- ARCHIVE RELOCATE: chuyển CategoryId của Asset, Item và Document sang đích DRAFT, giữ IDs/mã/data state; tăng version đối tượng thay fields, append journal/audit. Snapshot và file/version không đổi bytes/qty/unit/asOf/identity. Archive SOFT_DELETE dùng hành vi Asset/Document đã có; **Item chưa có semantics xóa/ngừng chính thức**, nhánh có Item phải chọn RELOCATE, không tự tạo trạng thái Item mới. EMPTY_ONLY đối soát cả registry, kể cả lịch sử/xóa mềm.
- Item reclassification yêu cầu ITEMS_IMPORT + ITEMS_READ hai phía; snapshot kèm STOCK_READ/UPDATE đầy đủ Org/Location/Category. Documents yêu cầu MANAGE/READ và file DOWNLOAD hai phía; ảnh ASSET_FILES_UPLOAD/DOWNLOAD theo Asset scope hai phía. Không thay role/permission catalog hoặc auth policy.
- Job import chưa COMPLETE chặn tạo tree version để không làm job mất TreeVersionId và stranded; không lộ global progress/count. Hoàn tất job trước đổi cây. MOVE_ASSETS không đổi tree pointer nên không bị gate này.
- Joint batch đúng một Sheets batchUpdate cho tree/asset/inventory/media delta+journals/audit; không commit từng store rồi publish pointer. Lost response đọc đối soát tất cả revisions + ledger/pointer; retry không append mù. Cap1800KiB/45000chars-cell và các row limits giữ; plan lớn bị chặn trước write, không partial publish.
- Thêm optional table TreeRegistryDetails (operation/type/id/record JSON) cho checkpoint bốn registry và mỗi impact một dòng, giữ TreeChanges/TreeChangeDetails cũ. Legacy tree journals vẫn đọc được; receipt mới phải có đủ detail/digest/count, unknown/partial details fail. Không apply schema/live migration trong lượt.
- Inventory journal bổ sung ITEM_RECLASSIFY chỉ cho CategoryId/version, kiểm immutable source/20cells/tracking/state và +1version. Marker INVENTORY_V1_TREE sau action mới; reader hỗ trợ V1 và V1_TREE, phiên bản lạ dừng. Media DOCUMENT_RELOCATE chỉ CategoryId/version/update metadata, không published content; marker MEDIA_V1_TREE khi có action mới. Direct Document metadata update vẫn không đổi Category.
- Import cây không đi vòng mutation: Document owner/ảnh và Item/Asset có reference chặn đổi/mất/ancestor/state qua import. Import thuần nếu registry chưa hợp lệ phải dừng; không tắt guard để nạp cây.

## Phạm vi hoàn thiện web / bằng chứng

Menu/Home/hướng dẫn tiếng Việt, liên kết nhóm thao tác và dữ liệu DRAFT; không seed/rule công ty hoặc report ngoài R01–R04. Build/test local (core/Google fake/HTTP/Razor/chéo) theo COMMANDS, không lặp full nghiệm thu đã hoãn. Browser tự kiểm nếu runtime có provider; fixture GIẢ/isolated local không chứng minh Google/nguồn/browser thật. Hướng dẫn vận hành nêu single backend/process lease/không edit Sheet ngoài bảo trì, môi trường chưa xác minh. Không tuyên bố production-ready hoặc backup/restore thật PASS nếu chưa chạy.

## Findings bổ sung từ test chéo

Form cập nhật snapshot phải GET giá trị/version đã lưu và POST lỗi giữ version/key cũ. HTML input-number gửi invariant; culture vi-VN từng parse6.25 thành625, sửa binder nullable invariant, không nhận hàng nghìn/đổi rule stock. Media prepare dùng delta giữ physical row order để không rewrite file/hồ sơ ngoài tác động hoặc sai dòng sau reorder; cap atomic vẫn giữ.

Public /api/system/status được làm đúng config-only như contract scaffold: không tự probe Google anonymous, state WEB_DEMO_LOCAL không là lifecycle Task, liveVerified=false cho endpoint này. CLI/live gate có chủ đích giữ, không gọi trong lượt.

## Google DEV — kế hoạch trước code, 2026-10-14

Người dùng cho phép xác minh/init/migration và dữ liệu hoàn toàn giả trên DEV hiện có. Chỉ một writer/lease, không reset auth/PoC hoặc resource trùng. CLI mới kiểm DEV name/MIME/owner/ACL/distinct resources trước write; adapter kiểm version/header/journal/ownership. Init chỉ tạo module hoàn toàn vắng với revision 0 (Assets/Inventory/Media); Category cần import DRAFT hợp lệ, không tạo marker rỗng sai pointer. Schema partial/lạ dừng, không overwrite. Marker/receipt tree tạo theo explicit mutation đúng ledger. Fixture/reference/rule chỉ nhánh GIẢ TASK-014, không là quy tắc công ty.

Legacy TASK004_POC phải giữ nguyên; nếu hiện diện, chỉ được công nhận sau kiểm hợp đồng header/rows, không tắt guard cho tab bất kỳ. Backup trước write: Sheets values/formulas và Drive manifest được DPAPI CurrentUser bảo vệ ngoài Git, round-trip decrypt cục bộ; manifest chưa là backup bytes hoặc diễn tập restore. Restore chỉ đích DEV riêng khi có kiểm xung đột; không ghi đè resource đang dùng. API/Razor live dùng credential prompt hiện có, TLS validation bật và xóa khóa tạm trong finally. Không timeout dài/mobile/commit/push.

Resource legacy vừa quan sát: business `DuyTien Assets DEV`, folder `DuyTien Assets DEV Files`, system tên provisioning hiện có; ba resource distinct/owner-only/exact drive.file. Business có Sheet1 và TASK004_POC, chưa có các module mới trước init. Sheet1 chỉ được nhận khi full used-range rỗng; PoC kiểm header/record kind/identity/state, không chạy lại PoC hoặc đổi rows.

## Lỗi quota live và phương án — trước sửa, 2026-10-14

Sau joint tree commit/replay 200, Asset read 503; safe logger cho thấy Google GET 429 liên tiếp. Sheets giới hạn read/write riêng 60 requests/phút/user/project theo [Google](https://developers.google.com/workspace/sheets/api/limits). Full registry validation phát nhiều reads; không bỏ validation/cache authority để đạt test.

Bổ sung budget operational dùng chung executor: cách nhau tối thiểu 1250 ms cho Sheets reads/writes riêng, lưu hai timestamp cạnh process lease ngoài Git để restart/CLI không reset pacing. File reservation lock + async cancellation; không lưu business rows/ID/token, không SQL/offline DB hoặc đổi auth policy. 429 ghi cooldown 60 s và giữ retry semantics cũ, không retry mù write chưa xác định. Đây là bảo vệ quota, chưa tối ưu latency/full snapshot batching hoặc chứng minh tải thực.
