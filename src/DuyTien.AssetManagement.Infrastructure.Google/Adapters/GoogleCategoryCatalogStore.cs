using System.Security.Cryptography;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using ValueRange = DuyTien.AssetManagement.Infrastructure.Google.Adapters.GoogleReadValueRange;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

// All managed category tables share the BUSINESS spreadsheet, never the auth spreadsheet.
public sealed class GoogleCategoryCatalogStore(GoogleWorkspaceOptions options, GoogleApiRequestExecutor api) : ICategoryCatalogStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly IReadOnlyDictionary<string, string[]> Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["SchemaVersions"] = ["SchemaVersion", "AppliedAt", "Reason", "ActiveTreeVersionId", "Revision"],
        ["Categories"] = ["CategoryId", "TreeVersionId", "ParentId", "FLevel", "DisplayOrder", "Name", "SegmentCode", "FullCode", "Definition", "State", "Version", "SourceKey", "SourceSheet", "SourceRow", "SourceSTT"],
        ["TreeVersions"] = ["TreeVersionId", "State", "SourceHash", "MappingVersion", "ContentHash", "CreatedAt", "ActorUserId"],
        ["ImportJobs"] = ["JobId", "SourceHash", "MappingVersion", "State", "TotalRows", "CompletedRows", "ActorUserId", "CompletedAt"],
        ["ImportRows"] = ["JobId", "SourceSheet", "SourceRow", "SourceSTT", "State", "TargetId", "Message"],
        ["Operations"] = ["OperationId", "IdempotencyKey", "ActorUserId", "RequestHash", "Type", "State", "EntityId", "TotalRows", "CompletedAt", "SourceHash", "MappingVersion", "AlreadyImported"],
        ["AuditEvents"] = ["EventId", "OperationId", "ActorUserId", "EntityId", "Action", "BeforeJson", "AfterJson", "Reason", "Timestamp"]
    };
    public async Task<CategoryCatalogState> ReadAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await Metadata(cancellationToken).ConfigureAwait(false);
        return await Load(metadata, cancellationToken).ConfigureAwait(false);
    }
    public async Task CommitAsync(CategoryCatalogState candidate, long expectedRevision, CancellationToken cancellationToken = default)
    {
        var requests = await PrepareAsync(candidate, expectedRevision, cancellationToken);
        var body = JsonSerializer.SerializeToUtf8Bytes(new { requests }, Json);
        if (body.Length > 1800 * 1024) throw new CategoryValidationException("Batch demo vượt 1800 KiB; chưa commit. Cần tác vụ mở rộng chunk riêng khi lịch sử quá lớn.");
        try
        {
            // addSheet/appendDimension are not safe to retry blindly. Reconcile the ledger after uncertainty.
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, UriFor(":batchUpdate"))
            { Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } } },
                GoogleRequestSemantics.NonIdempotentWrite, cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalStorageException)
        {
            var reconciled = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (reconciled.Revision == candidate.Revision && reconciled.ActiveTreeVersionId == candidate.ActiveTreeVersionId &&
                JsonSerializer.Serialize(reconciled.TreeChanges, Json) == JsonSerializer.Serialize(candidate.TreeChanges, Json) && reconciled.Operations.SequenceEqual(candidate.Operations)) return;
            throw;
        }
    }
    internal async Task<List<object>> PrepareAsync(CategoryCatalogState candidate, long expectedRevision, CancellationToken cancellationToken)
    {
        var metadata = await Metadata(cancellationToken).ConfigureAwait(false);
        var previous = await Load(metadata, cancellationToken).ConfigureAwait(false);
        if (previous.Revision != expectedRevision || candidate.Revision != expectedRevision + 1)
            throw new CategoryConflictException("Version Google Sheets đã thay đổi; cần preview lại.");
        if (candidate.Categories.Count < previous.Categories.Count || (candidate.Versions.Count != previous.Versions.Count + 1 && candidate.Versions.Count != previous.Versions.Count) || candidate.Operations.Count + candidate.TreeChanges.Count != previous.Operations.Count + previous.TreeChanges.Count + 1 ||
            !previous.Categories.SequenceEqual(candidate.Categories.Take(previous.Categories.Count)) ||
            !previous.Versions.SequenceEqual(candidate.Versions.Take(previous.Versions.Count)) ||
            !previous.Operations.SequenceEqual(candidate.Operations.Take(previous.Operations.Count)) || JsonSerializer.Serialize(previous.TreeChanges, Json) != JsonSerializer.Serialize(candidate.TreeChanges.Take(previous.TreeChanges.Count), Json))
            throw new CategoryConflictException("Không được ghi đè lịch sử danh mục.");
        var tables = Tables(candidate);
        var previousTables = previous.Revision == 0 ? null : Tables(previous);
        var allHeaders = AllHeaders;
        foreach (var version in candidate.Versions) if (CategoryTreeShape.Validate(candidate.Categories.Where(n => n.TreeVersionId == version.TreeVersionId).ToArray()).Count > 0) throw new CategoryValidationException("Snapshot cây không hợp lệ.");
        var usedIds = metadata.Sheets.Select(s => s.Properties.SheetId).ToHashSet();
        var requests = new List<object>();
        foreach (var pair in tables)
        {
            var existing = metadata.Sheets.SingleOrDefault(s => s.Properties.Title == pair.Key)?.Properties;
            var allRows = tables[pair.Key];
            var startRow = existing is null || pair.Key == "SchemaVersions" ? 0 : (previousTables!.GetValueOrDefault(pair.Key)?.Count ?? 0);
            var rows = allRows.Skip(startRow).ToList();
            if (allRows.Any(r => r.Any(cell => cell.Length > 45000))) throw new CategoryValidationException("Ô lịch sử cây vượt45000 ký tự; chưa commit.");
            if (allRows.Count > 20000) throw new CategoryValidationException("Lịch sử bảng vượt giới hạn demo; chưa commit.");
            var sheetId = existing?.SheetId ?? NextId(usedIds);
            if (existing is null)
                requests.Add(new { addSheet = new { properties = new { sheetId, title = pair.Key, gridProperties = new { rowCount = Math.Max(100, allRows.Count), columnCount = allHeaders[pair.Key].Length } } } });
            else
            {
                if (existing.GridProperties.RowCount < allRows.Count)
                    requests.Add(new { appendDimension = new { sheetId, dimension = "ROWS", length = allRows.Count - existing.GridProperties.RowCount } });
                if (existing.GridProperties.ColumnCount < allHeaders[pair.Key].Length)
                    throw new ExternalStorageException("Schema danh mục không đủ số cột; dừng import.");
            }
            if (rows.Count > 0) requests.Add(new
            {
                updateCells = new
                {
                    start = new { sheetId, rowIndex = startRow, columnIndex = 0 },
                    rows = rows.Select(r => new { values = r.Select(v => new { userEnteredValue = new { stringValue = v } }).ToArray() }).ToArray(),
                    fields = "userEnteredValue"
                }
            });
        }
        return requests;
    }
    public static string[] TreeChangeHeader { get; } = ["OperationId", "IdempotencyKey", "Actor", "RequestHash", "Kind", "BeforeTreeVersionId", "TreeVersionId", "ExpectedTreeRevision", "ExpectedAssetRevision", "Reason", "CompletedAt", "PreviewHash", "RequestJson", "CategoryIdsJson", "AssetIdsJson", "DiffRef", "ResultJson"];
    public static string[] TreeChangeDetailHeader { get; } = ["OperationId", "CategoryId", "Name", "BeforePath", "AfterPath", "BeforeLevel", "AfterLevel", "BeforeState", "AfterState"];
    public static string[] TreeRegistryHeader { get; } = ["OperationId", "Kind", "EntityId", "RecordJson"];
    private static IReadOnlyDictionary<string, string[]> AllHeaders => Headers.Concat(new[] { new KeyValuePair<string, string[]>("TreeChanges", TreeChangeHeader), new KeyValuePair<string, string[]>("TreeChangeDetails", TreeChangeDetailHeader), new KeyValuePair<string, string[]>("TreeRegistryDetails", TreeRegistryHeader) }).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
    internal string ReadSourceKey => UriFor("").AbsoluteUri;
    internal GoogleApiRequestExecutor ReadExecutor => api;
    internal async Task<GooglePreparedRead<CategoryCatalogState>> PrepareReadAsync(CancellationToken ct)
    {
        var metadata = await Metadata(ct);
        var titles = AllHeaders.Keys.Where(t => metadata.Sheets.Any(sheet => sheet.Properties.Title == t));
        return new(titles.Select(t => "'" + t + "'!A1:AZ20001").ToArray(), (values, token) => Load(metadata, token, values));
    }
    private async Task<Spreadsheet> Metadata(CancellationToken ct)
    {
        if (options.GetMissingSheetsSettings().Count > 0 || string.IsNullOrWhiteSpace(options.SystemSpreadsheetId) || options.SpreadsheetId == options.SystemSpreadsheetId)
            throw new ExternalStorageException("Cấu hình business spreadsheet phải đầy đủ và khác system spreadsheet.");
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("?fields=sheets.properties(sheetId,title,gridProperties)")), GoogleRequestSemantics.SafeRead, ct).ConfigureAwait(false);
        var value = await ReadJson<Spreadsheet>(response, ct);
        if (value.Sheets is null || value.Sheets.Any(s => s?.Properties?.GridProperties is null || string.IsNullOrWhiteSpace(s.Properties.Title)) || value.Sheets.Select(s => s.Properties.Title).Distinct(StringComparer.Ordinal).Count() != value.Sheets.Length) throw new ExternalStorageException("Metadata danh mục không hợp lệ.");
        return value;
    }
    private async Task<CategoryCatalogState> Load(Spreadsheet metadata, CancellationToken ct, ValueRange[]? prefetched = null)
    {
        var existing = Headers.Keys.Where(t => metadata.Sheets.Any(s => s.Properties.Title == t)).ToArray();
        var hasChanges = metadata.Sheets.Any(s => s.Properties.Title == "TreeChanges");
        var hasDetails = metadata.Sheets.Any(s => s.Properties.Title == "TreeChangeDetails");
        var hasRegistry = metadata.Sheets.Any(s => s.Properties.Title == "TreeRegistryDetails");
        if (hasRegistry && !hasChanges) throw new ExternalStorageException("Registry checkpoint thiếu journal cây.");
        if (hasChanges != hasDetails) throw new ExternalStorageException("Journal cây thiếu bảng chi tiết; không ghi đè.");
        if (existing.Length == 0 && !hasChanges) return new();
        if (existing.Length != Headers.Count) throw new ExternalStorageException("Schema danh mục chưa đầy đủ; không tự ghi đè dữ liệu chưa xác định.");
        if (hasChanges) existing = existing.Append("TreeChanges").Append("TreeChangeDetails").ToArray();
        if (hasRegistry) existing = existing.Append("TreeRegistryDetails").ToArray();
        var allHeaders = AllHeaders;
        var query = string.Join('&', existing.Select(t => "ranges=" + Uri.EscapeDataString($"'{t}'!A1:AZ20001")));
        BatchValues value;
        if (prefetched is null)
        {
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("/values:batchGet?" + query + "&valueRenderOption=UNFORMATTED_VALUE")), GoogleRequestSemantics.SafeRead, ct).ConfigureAwait(false);
            value = await ReadJson<BatchValues>(response, ct);
        }
        else value = new(prefetched);
        if (value?.ValueRanges?.Length != existing.Length) throw new ExternalStorageException("Google trả thiếu bảng danh mục.");
        var tables = new Dictionary<string, string[][]>(StringComparer.Ordinal);
        for (var i = 0; i < existing.Length; i++)
        {
            var title = existing[i]; var range = value.ValueRanges[i];
            if (range?.Range is null || !range.Range.StartsWith("'" + title + "'!", StringComparison.Ordinal) && !range.Range.StartsWith(title + "!", StringComparison.Ordinal))
                throw new ExternalStorageException("Google trả range khác yêu cầu.");
            if (range.Values?.Any(r => r is null) == true) throw new ExternalStorageException("Dòng danh mục null.");
            var rows = (range.Values ?? []).Select(r => r.Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText()).ToArray()).ToArray();
            if (rows.Length == 0 || !rows[0].SequenceEqual(allHeaders[title]) || rows.Length > 20000)
                throw new ExternalStorageException("Header/schema hoặc kích thước bảng danh mục không hợp lệ; không overwrite.");
            if (rows.Skip(1).Any(r => r.Length != allHeaders[title].Length || r.Any(v => v.Length > 45000))) throw new ExternalStorageException("Dòng danh mục không đủ cột.");
            tables[title] = rows.Skip(1).ToArray();
        }
        try
        {
            var marker = tables["SchemaVersions"].Single();
            if (marker[0] != "CATEGORY_V1") throw new ExternalStorageException("Schema business chưa được hỗ trợ.");
            var state = new CategoryCatalogState { ActiveTreeVersionId = marker[3], Revision = long.Parse(marker[4], CultureInfo.InvariantCulture) };
            foreach (var r in tables["Categories"]) state.Categories.Add(new(r[0], r[1], Empty(r[2]), int.Parse(r[3]), int.Parse(r[4]), r[5], r[6], r[7], r[8], r[9], int.Parse(r[10]), r[11], r[12], int.Parse(r[13]), r[14]));
            foreach (var r in tables["TreeVersions"]) state.Versions.Add(new(r[0], r[1], r[2], r[3], r[4], DateTimeOffset.Parse(r[5], CultureInfo.InvariantCulture), r[6]));
            foreach (var r in tables["Operations"])
            {
                if (r[4] != "CATEGORY_IMPORT" || r[5] != "COMPLETE") throw new ExternalStorageException("Operation business không được hỗ trợ.");
                state.Operations.Add(new(r[0], r[1], r[2], r[3], r[6], int.Parse(r[7]), DateTimeOffset.Parse(r[8], CultureInfo.InvariantCulture), r[9], r[10], bool.Parse(r[11])));
            }
            TreeNodeImpact[] ReadDiff(string operationId, string reference)
            {
                if (reference != "DETAIL_ROWS") throw new ExternalStorageException("Diff reference không hợp lệ.");
                return tables["TreeChangeDetails"].Where(r => r[0] == operationId).Select(r => new TreeNodeImpact(r[1], r[2], r[3], r[4], int.Parse(r[5], CultureInfo.InvariantCulture), int.Parse(r[6], CultureInfo.InvariantCulture), r[7], r[8])).ToArray();
            }
            if (tables.TryGetValue("TreeChanges", out var changes))
                foreach (var r in changes) state.TreeChanges.Add(new(r[0], r[1], r[2], r[3], r[4], r[5], r[6], long.Parse(r[7], CultureInfo.InvariantCulture), long.Parse(r[8], CultureInfo.InvariantCulture), r[9], DateTimeOffset.Parse(r[10], CultureInfo.InvariantCulture), r[11], JsonSerializer.Deserialize<TreeMutationRequest>(r[12], Json)!, JsonSerializer.Deserialize<string[]>(r[13], Json)!, JsonSerializer.Deserialize<string[]>(r[14], Json)!, ReadDiff(r[0], r[15]), JsonSerializer.Deserialize<TreeMutationResult>(r[16], Json)!));
            if (state.Operations.Count + state.TreeChanges.Count != state.Revision || !state.Versions.Any(v => v.TreeVersionId == state.ActiveTreeVersionId) ||
                state.Versions.Any(v => v.State != "DRAFT") || state.Categories.Any(n => n.State is not ("DRAFT" or "DRAFT_INACTIVE"))) throw new ExternalStorageException("Pointer hoặc state danh mục không hợp lệ.");
            // Reject unmanaged edits instead of overwriting unknown rows at computed append offsets.
            if (state.TreeChanges.Any(o => o.Result is null)) throw new ExternalStorageException("Tree ledger result null; không ghi cây.");
            if (state.TreeChanges.Any(o => o.Result.RegistryReconciled))
            {
                if (!tables.TryGetValue("TreeRegistryDetails", out var registryRows)) throw new ExternalStorageException("Registry receipt thiếu bảng đối soát; không ghi cây.");
                for (var i = 0; i < state.TreeChanges.Count; i++)
                {
                    var op = state.TreeChanges[i]; if (!op.Result.RegistryReconciled) continue;
                    var rows = registryRows.Where(r => r[0] == op.OperationId).ToArray();
                    var checkpoints = rows.Where(r => r[1] == "CHECKPOINT" && r[2] == "CHECKPOINT").ToArray();
                    if (checkpoints.Length != 1 || rows.Any(r => r[1] is not ("CHECKPOINT" or "IMPACT"))) throw new ExternalStorageException("Registry receipt partial/unknown.");
                    var checkpoint = JsonSerializer.Deserialize<TreeRegistryCheckpoint>(checkpoints[0][3], Json)!;
                    var impacts = rows.Where(r => r[1] == "IMPACT").Select(r => JsonSerializer.Deserialize<TreeRegistryImpact>(r[3], Json)!).ToArray();
                    if (checkpoint is null || checkpoint.InventoryRevision < 0 || checkpoint.MediaRevision < 0 || impacts.Any(x => x is null || x.Kind is not ("ITEM" or "STOCK" or "DOCUMENT" or "VERSION" or "PHOTO" or "DOCX" or "PDF") || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.BeforeCategoryId) || string.IsNullOrWhiteSpace(x.AfterCategoryId) || x.ExpectedVersion < 1) || impacts.Select(x => x.Kind + ":" + x.Id).Distinct(StringComparer.Ordinal).Count() != impacts.Length || impacts.Length != op.Result.RegistryImpactCount || rows.Length != impacts.Length + 1) throw new ExternalStorageException("Registry receipt/diff không hợp lệ.");
                    state.TreeChanges[i] = op with { RegistryCheckpoint = checkpoint, RegistryImpacts = impacts };
                }
            }
            else if (hasRegistry) throw new ExternalStorageException("Registry details không có receipt hợp lệ.");
            var expectedTables = Tables(state);
            foreach (var table in tables)
                if (!table.Value.Select(r => string.Join("\0", r)).SequenceEqual(expectedTables[table.Key].Skip(1).Select(r => string.Join("\0", r))))
                    throw new ExternalStorageException("Journal/provenance danh mục không nhất quán; cần đối soát trong chế độ bảo trì.");
            foreach (var version in state.Versions)
            {
                var nodes = state.Categories.Where(n => n.TreeVersionId == version.TreeVersionId).ToArray();
                if (CategoryTreeShape.Validate(nodes).Count > 0) throw new ExternalStorageException("Cây danh mục lưu trữ không hợp lệ.");
                if (version.MappingVersion == "TASK008_TREE_V1" && Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(nodes, Json))) != version.ContentHash) throw new ExternalStorageException("Snapshot cây không khớp digest đã commit.");

            }
            foreach (var change in state.TreeChanges)
            {
                if (change.Request is null || change.Result is null || change.CategoryIds is null || change.AssetIds is null || change.Diff is null || change.Request.Kind != change.Kind || change.Request.Reason != change.Reason || change.Result.TreeRevision != change.ExpectedTreeRevision + 1 || change.Result.AssetRevision != change.ExpectedAssetRevision + change.Result.ChangedAssetCount || change.Diff.Length != change.Result.ChangedCategoryCount || change.CategoryIds.Distinct(StringComparer.Ordinal).Count() != change.CategoryIds.Length || change.AssetIds.Distinct(StringComparer.Ordinal).Count() != change.AssetIds.Length || !state.Versions.Any(v => v.TreeVersionId == change.BeforeTreeVersionId) || !state.Versions.Any(v => v.TreeVersionId == change.TreeVersionId)) throw new ExternalStorageException("Tree change ledger không hợp lệ.");
                var beforeIds = state.Categories.Where(n => n.TreeVersionId == change.BeforeTreeVersionId).Select(n => n.CategoryId).ToHashSet(StringComparer.Ordinal);
                if (!beforeIds.SetEquals(state.Categories.Where(n => n.TreeVersionId == change.TreeVersionId).Select(n => n.CategoryId))) throw new ExternalStorageException("Không được mất/đổi ID qua tree change.");
            }
            return state;
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException or InvalidOperationException or ArgumentException)
        { throw new ExternalStorageException("Dữ liệu schema danh mục không hợp lệ."); }
    }
    private static string TreeResultJson(TreeMutationResult result) => result.RegistryReconciled
        ? JsonSerializer.Serialize(result, Json)
        : JsonSerializer.Serialize(new { result.TreeVersionId, result.TreeRevision, result.AssetRevision, result.ChangedCategoryCount, result.ChangedAssetCount, result.Replayed }, Json);
    private static Dictionary<string, List<string[]>> Tables(CategoryCatalogState s)
    {
        var tables = Headers.ToDictionary(p => p.Key, p => new List<string[]> { p.Value }, StringComparer.Ordinal);
        var last = s.Versions.Last();
        tables["SchemaVersions"].Add(["CATEGORY_V1", last.CreatedAt.ToString("O"), "TASK-006 DRAFT snapshot", s.ActiveTreeVersionId!, s.Revision.ToString(CultureInfo.InvariantCulture)]);
        tables["Categories"].AddRange(s.Categories.Select(n => new[] { n.CategoryId, n.TreeVersionId, n.ParentId ?? "", n.FLevel.ToString(CultureInfo.InvariantCulture), n.DisplayOrder.ToString(CultureInfo.InvariantCulture), n.Name, n.SegmentCode, n.FullCode, n.Definition, n.State, n.Version.ToString(CultureInfo.InvariantCulture), n.SourceKey, n.SourceSheet, n.SourceRow.ToString(CultureInfo.InvariantCulture), n.SourceStt }));
        tables["TreeVersions"].AddRange(s.Versions.Select(v => new[] { v.TreeVersionId, v.State, v.SourceHash, v.MappingVersion, v.ContentHash, v.CreatedAt.ToString("O"), v.ActorUserId }));
        foreach (var o in s.Operations)
        {
            var version = s.Versions.Single(v => v.TreeVersionId == o.TreeVersionId);
            tables["Operations"].Add([o.OperationId, o.IdempotencyKey, o.ActorUserId, o.RequestHash, "CATEGORY_IMPORT", "COMPLETE", o.TreeVersionId, o.TotalRows.ToString(CultureInfo.InvariantCulture), o.CompletedAt.ToString("O"), o.SourceHash, o.MappingVersion, o.AlreadyImported.ToString()]);
            tables["ImportJobs"].Add([o.OperationId, o.SourceHash, o.MappingVersion, "COMPLETE", o.TotalRows.ToString(CultureInfo.InvariantCulture), o.TotalRows.ToString(CultureInfo.InvariantCulture), o.ActorUserId, o.CompletedAt.ToString("O")]);
            if (!o.AlreadyImported) tables["ImportRows"].AddRange(s.Categories.Where(n => n.TreeVersionId == o.TreeVersionId).Select(n => new[] { o.OperationId, n.SourceSheet, n.SourceRow.ToString(CultureInfo.InvariantCulture), n.SourceStt, "COMPLETE", n.CategoryId, "DRAFT; giữ riêng tên nhiều mã" }));
            tables["AuditEvents"].Add(["AUDIT-" + o.OperationId, o.OperationId, o.ActorUserId, o.TreeVersionId, o.AlreadyImported ? "CATEGORY_IMPORT_NOOP" : "CATEGORY_IMPORT", "{}", JsonSerializer.Serialize(new { o.TotalRows, State = "DRAFT" }), "Commit preview đã xác nhận", o.CompletedAt.ToString("O")]);
        }
        if (s.TreeChanges.Count > 0)
        {
            tables["TreeChanges"] = new() { TreeChangeHeader };
            tables["TreeChangeDetails"] = new() { TreeChangeDetailHeader };
            if (s.TreeChanges.Any(o => o.Result.RegistryReconciled)) tables["TreeRegistryDetails"] = new() { TreeRegistryHeader };
            foreach (var o in s.TreeChanges)
            {
                if (o.Result.RegistryReconciled)
                {
                    if (o.RegistryCheckpoint is null || o.RegistryImpacts is null || o.RegistryImpacts.Length != o.Result.RegistryImpactCount) throw new CategoryValidationException("Registry receipt chưa đầy đủ.");
                    tables["TreeRegistryDetails"].Add([o.OperationId, "CHECKPOINT", "CHECKPOINT", JsonSerializer.Serialize(o.RegistryCheckpoint, Json)]);
                    tables["TreeRegistryDetails"].AddRange(o.RegistryImpacts.Select(i => new[] { o.OperationId, "IMPACT", i.Kind + ":" + i.Id, JsonSerializer.Serialize(i, Json) }));
                }
                tables["TreeChanges"].Add([o.OperationId, o.IdempotencyKey, o.Actor, o.RequestHash, o.Kind, o.BeforeTreeVersionId, o.TreeVersionId, o.ExpectedTreeRevision.ToString(CultureInfo.InvariantCulture), o.ExpectedAssetRevision.ToString(CultureInfo.InvariantCulture), o.Reason, o.CompletedAt.ToString("O"), o.PreviewHash, JsonSerializer.Serialize(o.Request, Json), JsonSerializer.Serialize(o.CategoryIds, Json), JsonSerializer.Serialize(o.AssetIds, Json), "DETAIL_ROWS", TreeResultJson(o.Result)]);
                tables["TreeChangeDetails"].AddRange(o.Diff.Select(d => new[] { o.OperationId, d.CategoryId, d.Name, d.BeforePath, d.AfterPath, d.BeforeLevel.ToString(CultureInfo.InvariantCulture), d.AfterLevel.ToString(CultureInfo.InvariantCulture), d.BeforeState, d.AfterState }));
                tables["AuditEvents"].Add(["AUDIT-" + o.OperationId, o.OperationId, o.Actor, o.TreeVersionId, o.Kind, JsonSerializer.Serialize(new { o.BeforeTreeVersionId, o.ExpectedTreeRevision }), JsonSerializer.Serialize(new { o.TreeVersionId, Result = JsonSerializer.Deserialize<JsonElement>(TreeResultJson(o.Result)), DiffCount = o.Diff.Length, DetailOperationId = o.OperationId }), o.Reason, o.CompletedAt.ToString("O")]);
            }
        }
        return tables;
    }
    private static async Task<T> ReadJson<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new ExternalStorageException("Google trả thiếu dữ liệu danh mục."); }
        catch (JsonException) { throw new ExternalStorageException("Google trả JSON/schema danh mục không hợp lệ."); }
        catch (HttpRequestException e) { throw new ExternalStorageTransientException("Không đọc được response danh mục Google.", false, e); }
        catch (IOException e) { throw new ExternalStorageTransientException("Không đọc được response danh mục Google.", false, e); }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested) { throw new ExternalStorageTransientException("Response danh mục Google timeout.", false, e); }
    }
    private Uri UriFor(string suffix) => new(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + suffix);
    private static int NextId(HashSet<int> ids) { var id = 6000; while (!ids.Add(id)) id++; return id; }
    private static string? Empty(string value) => value.Length == 0 ? null : value;
    private sealed record Spreadsheet(Sheet[] Sheets);
    private sealed record Sheet(Properties Properties);
    private sealed record Properties(int SheetId, string Title, Grid GridProperties);
    private sealed record Grid(int RowCount, int ColumnCount);
    private sealed record BatchValues(ValueRange[] ValueRanges);

}
