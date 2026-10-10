# Schema logic đề xuất cho Google Sheets

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
