# TASK-021 — bản đồ Google reads (16/10/2026)

Phạm vi: Categories/Assets Razor GET và reconnect; HEAD55ddab0 làm baseline. Đọc PageModel→application→stores→API; count đối chiếu fake handler Uris và telemetry Google trước. Không suy từ tên service, không đổi pacing/cooldown hoặc gate cũ.

| Đường | Trước (không touch) | Sau thiết kế thực hiện | Điều giữ nguyên |
|---|---|---|---|
| /Categories | auth1 + metadata business1 + category values1 =3 |3 | ListCore giữ permission/scoped tree, schema/headers/journal/digest/registry details |
| /Categories/Detail/id | auth memo1 + metadata memo1 + values list1 + values history1 =4 | auth1 + meta1 + values1 =3 | DetailWithHistory giữ scope hiện tại và scope riêng từng version lịch sử,404 nhánh ẩn |
| /Assets | auth1 + meta business memo1 + asset values1 + category values1 =4 | auth1 + meta1 + union values1 =3 | Browse+filters vốn đã chung Context; không thêm I/O cho component |
| /Assets/Detail/id (editor) | auth memo1 + meta memo1 +2asset values+2category values =6 | auth1 + meta1 + union values1 =3 | DetailWithChoices cùng read lease, READ + UPDATE permission/scopes riêng, version/tree/key không đổi |

Số trên không có retry/redirect/touch. Khi tới touch5ph, core auth vẫn read lại trong writer rồi persist theo policy; không gộp hoặc bỏ lượt bắt buộc này. Pacing1250ms, cooldown/429 retry semantics giữ nguyên. Snapshot CLI không gọi session validation/touch, không là page time.

## Truy vết nguồn và guard

- Program: RequestAuthStore.Begin + GoogleMetadataReadMemo.Begin chỉ cho vòng request. Opaque handler→ValidateAccessToken→GoogleAuth ReadTables batch sáu bảng động system (Users/assignments/scopes/sessions/refresh/security), rồi RememberAsync và application đọc lại qua cùng request memo; mỗi reader nhận clone. Không cross-request auth cache hoặc bỏ revoke/SecurityVersion/expiry.
- Category Index→ListAsync→writer.ReadAsync→auth permission→GoogleCategory Read metadata (sheetId/title/gridProperties), chọn đúng bảng managed và optional TreeChanges/TreeChangeDetails/TreeRegistryDetails, values batch→đủ headers/sizes/schema/journal/digest→current scoped tree→Razor. Header/sidebar/breadcrumb chỉ claims/cache proof RAM, không network.
- Asset Index→BrowseAsync→read lease→fresh auth→Load Assets + Categories→hierarchies ba chiều→scope trước filter/count/page, choices từ cùng Context. Asset parser đối soát Assets/counters/aliases/audit với journal, reference rule/org/location; category parser như trên.
- Capability batch chuẩn bị hai plan bằng metadata cùng nguồn/executor, một values.batchGet union các A1 ranges của business spreadsheet, rồi đưa đúng subset cho **hai parser cũ đầy đủ**. Giữ row limits5001/20001, optional schema semantics và errors; không thêm Items/Media bytes vào list query. JSON raw không ra client; chỉ app projection đã kiểm scope.
- Pair chỉ dùng trong read entrypoints. Application write Load/Prepare/Commit tiếp tục fresh expected revision checks. Full tree mutation vẫn đi GoogleTreeMutationStore.GuardMetadata→legacy validation→Categories/Assets/Inventory/Media→TreeRegistryIntegrity và checkpoint; không dùng snapshot list để thay full registry. Không sửa guard hoặc đối soát để giảm calls.
- System spreadsheet khác business: không thể gộp auth và business thành một values.batchGet. Metadata cấu trúc vẫn cần đọc để biết bảng optional/schema; không bỏ metadata để làm Categories root xuống2 mà thiếu bằng chứng tương đương.

## Lifetime / concurrency

Không global coalescing hoặc shared filtered cache giữa user/request. Pair chỉ sống trong một await/read lease, cùng executor + business URI được kiểm trước/sau fetch, facade category phải cùng source; toàn bộ materialization xong mới trả. Không cache failed/cancelled task; caller token truyền hết, scope disposed, request sau đọc lại. Các test source mismatch/cancel/fault/scopes/revoke/guards/version/read-count là gate local. Không tăng parallel Google requests.

## Background

/api/connection trả technical state/cache proof, không Google auth/store reads; monitor Internet probe tách riêng, không là Google business call. Trước: RECONNECTING→fetch refreshPath→discard HTML→location.assign current route, gây đọc business hai lần. Sau: ở trạng thái RECONNECTING, trang business readable, sạch hoặc /Offline điều hướng thẳng **một** GET để server xác minh auth/data rồi mở ghi; không prefetch. Trang form dirty/non-readable giữ input/key và chỉ verification read, không điều hướng/replay. pagehide abort GET/poll bằng lifetime signal; error giữ backoff, ONLINE không có background business read. Không coalesce navigation giữa các tab/user bằng cache cũ. SERVICE_UNAVAILABLE giữ verification fetch/backoff tới thành công rồi mới điều hướng; nhánh phục hồi lỗi này vẫn có thể gồm verification read + navigation, không gọi là đã loại mọi lượt trùng. Source/event doubles không là browser/mạng thật PASS.

Đo Google bằng catalog-read-diagnose nếu browser vẫn dừng URL-confidence: ghi rõ CLI_SERVICE_READ_NO_WEB_SESSION, auth không touch, cùng binary/separate wrapper vs batch, fresh từng sample, compare payload/authority/revision boolean. Không gửi cookie/profile/password hoặc tự mở lại browser đã bị stop.
