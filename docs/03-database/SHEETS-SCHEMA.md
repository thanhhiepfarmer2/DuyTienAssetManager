# Schema logic đề xuất cho Google Sheets

## TASK-007 local subset — 2026-10-13

ASSET_DEMO_V1 dùng business spreadsheet, tách system auth. Headers authoritative local trong GoogleAssetCatalogStore, không apply trên Google trong lượt. Sáu bảng ghi: AssetSchemaVersions(SchemaVersion/Revision), Assets(23 cột theo AssetRecord), IdCounters(Key/LastAllocated), LegacyAliases(AliasId/Namespace/OldCode/AssetId/IsActive), AssetOperations(op/key/actor/hash/action/entity/before/after/aliases/counter/reason/time), AssetAuditEvents(before/after/alias snapshot/actor/reason/time). CodeRules(CodeRuleId/CategoryRootId/Prefix/CCC/Status/ApprovedForDemo/Version), OrgUnits(Id/ParentId/Name/State/Version), Locations(Id/ParentId/OrgUnitId/Name/State/Version) chỉ đọc, không seed/update nguồn trong CRUD.

First explicit write mới tạo sáu tab nếu tất cả vắng; partial/header/journal lạ dừng, không ghi đè dữ liệu chưa xác định. CodeRules chỉ DEMO/ApprovedForDemo=true có CCC đúng root mới allocate; fixture true không là approval thật. Prefix+CCC counter không reset/reuse; ID/code/IssuedCCC/CodeRuleId bất biến. Legacy alias ngừng dùng vẫn giữ mapping/search/reservation.

Journal/audit riêng để không thay CATEGORY_V1/schema/Operations/AuditEvents TASK-006. Một atomic batch giữ counter/entity/aliases/operation/audit; lost response read-reconcile, key payload khác409, version stale409. Rebuild state theo ID hỗ trợ sort Assets/counters/aliases; không cho reorder/mất operation history. Chặn trên5000 dòng/bảng,45000 ký tự/ô,1800KiB batch trước write; không cắt dữ liệu hoặc ghi PARTIAL. Actual Google constraints/throughput/restart/ACL chưa nghiệm thu: NOT_RUN_GOOGLE. Xem TEST-REPORT-TASK-007 và ASSET-DEMO-DESIGN-TASK-007.


## TASK-006 local subset — 2026-10-10

Implementation subset theo phạm vi người dùng: Categories + TreeVersions + ImportJobs/ImportRows + Operations + AuditEvents + SchemaVersions, cùng business spreadsheet khác system. Header thực tế là `GoogleCategoryCatalogStore.Headers`; không triển khai các bảng còn lại chỉ vì được PROPOSED ở dưới. Schema marker CATEGORY_V1 chứa pointer DRAFT hiệu lực demo và Revision (số operation commit/no-op), khác SchemaVersion auth.
Category có SourceKey/SourceSheet/SourceRow/SourceSTT lưu mapping, CategoryId opaque bất biến; source code không trở thành ID. TreeVersions giữ SourceHash/MappingVersion/ContentHash/actor/time. Operations lưu key theo actor, RequestHash/source hash/mapping, AlreadyImported và COMPLETE; no-op vẫn lưu key/audit nhưng không thêm Category/version. ImportRows của no-op không nhân bản dòng; tham chiếu version đã có.
Batch bounded atomic (tối đa1800KiB; bảng <=20000 dòng, input <=1000 nút/10MB), append lịch sử và update pointer cùng request, stringValue chống formula execution. Mọi tab vắng mới được tạo trong explicit import, không ở startup; partial schema/header/journal lạ dừng để bảo vệ dữ liệu. Retry uncertain read ledger, không append mù. Không cần chunk PARTIAL cho subset bounded; vượt giới hạn bị chặn trước write, chưa có generalized chunk/resume. Chưa apply hoặc nghiệm thu schema này trên Google thật: NOT_RUN_GOOGLE. System schema TASK-005 dev đã tồn tại và được nghiệm thu trước, không init lại.


Status: PROPOSED — chưa triển khai toàn bộ schema. TASK-004 đã có resource PoC
dev theo bằng chứng người dùng; system spreadsheet TASK-005 chưa được tạo/chọn/
ghi. Không sử dụng sheet nhân sự nguồn như bảng Users.
Quy tắc chung: Id hệ thống immutable; code/text RAW giữ số 0; timestamp ISO UTC; Version integer; bool enum được chuẩn hóa; không dựa row index làm ID.

## Spreadsheet nghiệp vụ (các tab)
| Tab | Trường chính | Ràng buộc ở backend |
|---|---|---|
| SchemaVersions | SchemaVersion, appliedAt, reason | Migrations có version và không xóa dữ liệu ngoài phạm vi |
| TreeVersions | TreeVersionId, state, publishedAt, sourceHash | Một active pointer; bản nháp tách version |
| Categories | CategoryId, TreeVersionId, ParentId, FLevel, DisplayOrder, Name, SegmentCode, FullCode, Definition, State, Version | Cha tồn tại, no cycle, F<=10, fullCode unique/version; CategoryId giữ qua version |
| CodeRules | CodeRuleId, Prefix, CCC, Format, Status, version, approvedForDemo | Không cấp mã trước rule demo được xác lập |
| IdCounters | CounterKey, LastAllocated, Format, Version | Counter bền vững, không reset theo row/order |
| Assets | AssetId, ObjectCode, IssuedCCC, CategoryId, Name, Type, ItemId?, OrgUnitId, LocationId, CustodianRef, Serial, Model, Condition, Notes, IsDeleted, CreatedBy/At, UpdatedBy/At, Version | Unique code, khóa ID; scope/server validate |
| Items | ItemId, ObjectCode?, CategoryId?, Name, Nature, PrimaryUnit, SourceCode, DefaultWarehouseRef, StockTracked, IsDeleted, Version | SourceCode trong namespace, không mặc định Asset |
| ItemConversions | ConversionId, ItemId, FromUnit, ToUnit, Factor, Operator, Description | Validate conversion trước đổi số lượng |
| ItemCommercialFields | ItemId, ManufacturerCode, SalePrice1/2/3, FixedPrice | Giá bán tham chiếu; không dùng tính nguyên giá tài sản |
| LegacyAliases | AliasId, namespace, OldCode, ObjectType, ObjectId, Status, sourceHash, reason | Mapping xác minh một alias → một ID; unresolved riêng |
| Locations | LocationId, ParentId?, Name, OrgUnitId, State, Version | ParentId trỏ Location tồn tại, không self/cycle; root dùng null; descendant chỉ theo ID/ParentId, không theo tên/prefix |
| OrgUnits | OrgUnitId, ParentId, Name, state | Có actor/role scope tương ứng |
| StockBalances | BalanceId, ItemId, LocationId, Unit, Quantity?, Minimum?, AsOf, Source, UpdatedBy, Version | UNKNOWN !=0; snapshot không là sổ giao dịch |
| Documents | DocumentId, ObjectCode?, CategoryId, BusinessCode, Name, currentPublishedVersionId, state, scopeFields, Version | Mã QT/BM lưu riêng với ObjectCode |
| DocumentVersions | DocumentVersionId, DocumentId, Label, DocxDriveFileId?, PdfDriveFileId?, EffectiveAt, state, checksum, UpdatedBy | Version cặp DOCX/PDF thống nhất, không ghi đè lịch sử |
| Files | AttachmentId, EntityType, EntityId, DriveFileId, Kind, Mime, Size, checksum, state, IsPrimary, Version | File access kiểm quyền qua owner entity; không binary |
| AuditEvents | EventId, OperationId, actor, entity, action, BeforeJson, AfterJson, reason, timestamp | Không secret; độ dài/số cell phải kiểm soát khi batch |
| Operations | OperationId, IdempotencyKey, actor, RequestHash, type, state, EntityId, resultDigest, timestamps | Một key không được dùng cho payload khác |
| ImportJobs | JobId, sourceHash, mappingVersion, state, totals, progress, Actor, timestamps | Preview/VALIDATED/COMMITTING/PARTIAL/COMPLETE/FAILED |
| ImportRows | JobId, SourceSheet, SourceRow, SourceSTT, status, TargetId?, validationMessage | Source row là provenance; không primary key permanent |

## Spreadsheet hệ thống riêng (ACL hạn chế)

Status phần này: `ACCEPTED` cho TASK-005 ngày 2026-10-07; adapter local đã build/test,
nhưng chưa tạo hoặc ghi Google. Spreadsheet
dùng cấu hình `GoogleWorkspace:SystemSpreadsheetId`, tách khỏi spreadsheet nghiệp
vụ và chỉ tài khoản vận hành/backend được truy cập.

| Tab | Trường chính đề xuất | Ràng buộc backend |
|---|---|---|
| SystemSchemaVersions | SchemaVersion, AppliedAt, Reason, AppliedBy | Init/migration có version; không tự chạy khi web startup |
| Users | UserId, Username, NormalizedUsername, DisplayName, PasswordHash, State, MustChangePassword, FailedAccessCount, FailedWindowStartedAt, LockoutEndAt, SecurityVersion, PasswordChangedAt, CreatedAt/By, UpdatedAt/By, Version | UserId immutable; NormalizedUsername unique; không lưu plaintext/reset secret |
| Roles | RoleId, Code, NameVi, Description, IsSystem, State, Version | Năm system role cố định trong demo; Code unique |
| Permissions | PermissionId, Code, Resource, Action, Description, Version | Catalog cố định theo [PERMISSIONS](../01-requirements/PERMISSIONS.md) |
| RolePermissions | RoleId, PermissionId, Version | Unique RoleId+PermissionId; Admin chứa toàn catalog |
| Positions | PositionId, Code, Name, State, Version | Metadata/template; không tự cấp quyền |
| PositionRoleTemplates | PositionId, RoleId, Note, Version | Chỉ gợi ý; Admin phải tạo assignment thật |
| UserRoleAssignments | AssignmentId, UserId, RoleId, PositionId?, Source, State, EffectiveFrom, EffectiveTo?, CreatedAt/By, Version | Assignment đang hiệu lực mới cấp quyền; permission là union giữa assignment |
| RoleAssignmentScopes | AssignmentId, ScopeType, ScopeId, IncludeDescendants, Version | Scope gắn assignment, không gắn global user; ALL phải tường minh từng chiều |
| Sessions | SessionId, UserId, ClientType, AccessTokenHash, RefreshFamilyId?, SecurityVersion, CreatedAt, LastSeenAt, IdleExpiresAt, AbsoluteExpiresAt, RevokedAt?, RevokedReason?, Version | Chỉ hash token; unique active access hash; kiểm expiry/revoke/security version |
| RefreshTokens | RefreshTokenId, SessionId, RefreshFamilyId, TokenHash, State, CreatedAt, IdleExpiresAt, AbsoluteExpiresAt, RotatedAt?, ReplacedByRefreshTokenId?, RevokedAt?, RevokedReason?, Version | Giữ marker `ROTATED`/`REVOKED`; reuse token đã rotate revoke toàn family trong cùng mutation |
| SecurityEvents | EventId, ActorUserId?, TargetUserId?, SessionId?, Action, Outcome, FailureCode?, CorrelationId, Timestamp | Không password/token/body reset; login unknown user dùng actor null |
| AuthOperations | OperationId, IdempotencyKey, ActorUserId?, RequestHash, Type, State, ResultDigest, CreatedAt, CompletedAt? | Một key không dùng payload khác; hỗ trợ retry admin mutation an toàn |

Các enum `ACCEPTED`:

- User State: `ACTIVE`, `LOCKED`.
- Assignment State: `ACTIVE`, `REVOKED`.
- ScopeType: `ORG_UNIT`, `LOCATION`, `CATEGORY_SUBTREE`.
- ClientType: `WEB`, `MOBILE`.
- Refresh token State: `ACTIVE`, `ROTATED`, `REVOKED`.
- Source: `MANUAL`, `POSITION_TEMPLATE` (template vẫn cần Admin duyệt).

Không có bảng UserPermissions/UserScopes global trong demo. Cách cũ có thể làm
permission của role hẹp ghép với scope rộng của role khác. Scope phải đi qua
`RoleAssignmentScopes` và được đánh giá trong cùng `UserRoleAssignment`.

Không coi hidden tab/protected range là cách che dữ liệu khỏi Google viewer. ACL
phải bảo vệ cả spreadsheet; app user không có quyền Google trực tiếp.

## Atomicity và quan hệ
Mọi operation thay Counter/Asset/Audit/Operation liên quan đặt trong cùng spreadsheet nghiệp vụ để batchUpdate có atomic commit. Mọi mutation user/role/scope/session/audit liên quan đặt trong một batch của spreadsheet hệ thống khi cần nguyên tử, đặc biệt last-admin protection. System và nghiệp vụ khác file không có transaction chung; state/reconciliation là bắt buộc khi flow cắt qua hai file.
Row locator được rebuild từ Id sau sort/delete/maintenance. No client writes; Version+single writer giữ consistency trong giả định demo. Không bảo đảm CAS chỉ bằng một cột Version.
Các tab có thể tạo dần theo Task, không yêu cầu tạo 30 tab và nghiệp vụ chưa dùng ngay ở bước đầu.

## TASK-008 local — 2026-10-13

CATEGORY_V1 và bảy header TASK-006 giữ tương thích. Khi explicit tree-change commit đầu tiên, thêm hai tab trong cùng atomic batch, không auto init ở startup: TreeChanges (17 cột: OperationId/IdempotencyKey/Actor/RequestHash/Kind/BeforeTreeVersionId/TreeVersionId/ExpectedTreeRevision/ExpectedAssetRevision/Reason/CompletedAt/PreviewHash/RequestJson/CategoryIdsJson/AssetIdsJson/DiffRef/ResultJson) và TreeChangeDetails (9 cột: OperationId/CategoryId/Name/BeforePath/AfterPath/BeforeLevel/AfterLevel/BeforeState/AfterState). DiffRef=DETAIL_ROWS, mỗi diff một dòng, audit giữ link/count/result để không vượt cell khi cây lớn. Cặp tab thiếu một/header lạ/journal lạ bị chặn, không overwrite.

TreeVersions luôn DRAFT; Category DRAFT_INACTIVE là node đã ngừng, vẫn giữ ID/provenance/lịch sử; API thường chỉ trả DRAFT. Category revision bằng số import operation + tree-change; Asset revision tăng theo từng Asset operation. Sửa/move/archive append complete snapshot rồi đổi pointer cùng batch với Assets/MOVE hoặc DELETE, AssetOperations/AssetAuditEvents; ID/code/alias/counter không tái cấp. MappingVersion TASK008_TREE_V1 kiểm digest của complete stored nodes. MOVE_ASSETS chỉ thêm ledger, giữ treeVersion nếu cây không đổi. Giới hạn combined1800KiB/45000chars-cell, tree1000/table20000, asset-table5000; không PARTIAL/chunk tự động. Chưa apply/verify Google live. [Thiết kế](../02-architecture/TREE-MUTATION-DESIGN-TASK-008.md), [report](../07-testing/TEST-REPORT-TASK-008.md).

## TASK-009 local — 2026-10-13

MEDIA_V1 subset trong business spreadsheet khác system: MediaSchemaVersions(SchemaVersion/Revision); Files(FileId/RecordJson), Documents(DocumentId/RecordJson), DocumentVersions(VersionId/RecordJson), MediaOperations(OperationId/RecordJson), MediaAuditEvents(OperationId/Actor/Action/Reason/Timestamp). Typed JSON lưu metadata, không binary/base64. Headers actual GoogleMediaCatalogStore; không overwrite tab Files/Documents của schema khác. Chỉ explicit mutation mới tạo cả6tab nếu tất cả vắng; partial/header/range/materialized/journal/audit khác dừng.

ManagedFile chứa owner immutable ASSET hoặc DOCUMENT_VERSION, kind PHOTO/DOCX/PDF, safe name/MIME/size/file checksum/DriveFileId, PENDING/AVAILABLE/FAILED, soft-delete/primary/description/version, UploadOperationId và timestamps/actor. Document DRAFT_DEMO có tên/business code/category/description/currentPublished pointer/version; DocumentVersion DRAFT/PUBLISHED_DEMO có label/effectiveOn/pair FileIds/version. Published version immutable, old files và mã/Asset IDs không thay. File SHA/request digest không là password hash, chỉ backend registry; DTO/error không trả checksum/Drive ID/session URI.

Mỗi event append journal/audit và materialized snapshots trong một atomic batch; read replay journal kiểm mọi rows/refs/versions/primary/counterpart, cho sort materialized theo ID nhưng không reorder history. Cap5000rows/tab/45000chars-cell/1800KiB batch; vượt cap không write hoặc cắt dữ liệu. Upload pending persist trước Drive create tại pre-generatedID; chỉ commit AVAILABLE sau Drive metadata+bytes verified. Drive/Sheets không transaction chung; pending/failed có operation để reconcile/retry, không hard-delete. Chưa apply schema Google. Guard TASK-008 giữ nguyên, có Files/Documents/Versions vẫn block tree mutation, không bypass bằng đổi tab/resource. [Thiết kế](../02-architecture/MEDIA-DESIGN-TASK-009.md).
