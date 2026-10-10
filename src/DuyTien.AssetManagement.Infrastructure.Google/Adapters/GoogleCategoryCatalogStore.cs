using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
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
        var metadata = await Metadata(cancellationToken).ConfigureAwait(false);
        var previous = await Load(metadata, cancellationToken).ConfigureAwait(false);
        if (previous.Revision != expectedRevision || candidate.Revision != expectedRevision + 1)
            throw new CategoryConflictException("Version Google Sheets đã thay đổi; cần preview lại.");
        if (candidate.Categories.Count < previous.Categories.Count || (candidate.Versions.Count != previous.Versions.Count + 1 && candidate.Versions.Count != previous.Versions.Count) || candidate.Operations.Count != previous.Operations.Count + 1 ||
            !previous.Categories.SequenceEqual(candidate.Categories.Take(previous.Categories.Count)) ||
            !previous.Versions.SequenceEqual(candidate.Versions.Take(previous.Versions.Count)) ||
            !previous.Operations.SequenceEqual(candidate.Operations.Take(previous.Operations.Count)))
            throw new CategoryConflictException("Không được ghi đè lịch sử danh mục.");
        var tables = Tables(candidate);
        var previousTables = previous.Revision == 0 ? null : Tables(previous);
        var usedIds = metadata.Sheets.Select(s => s.Properties.SheetId).ToHashSet();
        var requests = new List<object>();
        foreach (var pair in Headers)
        {
            var existing = metadata.Sheets.SingleOrDefault(s => s.Properties.Title == pair.Key)?.Properties;
            var allRows = tables[pair.Key];
            var startRow = existing is null || pair.Key == "SchemaVersions" ? 0 : previousTables![pair.Key].Count;
            var rows = allRows.Skip(startRow).ToList();
            if (allRows.Count > 20000) throw new CategoryValidationException("Lịch sử bảng vượt giới hạn demo; chưa commit.");
            var sheetId = existing?.SheetId ?? NextId(usedIds);
            if (existing is null)
                requests.Add(new { addSheet = new { properties = new { sheetId, title = pair.Key, gridProperties = new { rowCount = Math.Max(100, allRows.Count), columnCount = pair.Value.Length } } } });
            else
            {
                if (existing.GridProperties.RowCount < allRows.Count)
                    requests.Add(new { appendDimension = new { sheetId, dimension = "ROWS", length = allRows.Count - existing.GridProperties.RowCount } });
                if (existing.GridProperties.ColumnCount < pair.Value.Length)
                    throw new ExternalStorageException("Schema danh mục không đủ số cột; dừng import.");
            }
            if (rows.Count > 0) requests.Add(new { updateCells = new { start = new { sheetId, rowIndex = startRow, columnIndex = 0 },
                rows = rows.Select(r => new { values = r.Select(v => new { userEnteredValue = new { stringValue = v } }).ToArray() }).ToArray(), fields = "userEnteredValue" } });
        }
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
            var operation = candidate.Operations.Last();
            if (reconciled.Revision == candidate.Revision && reconciled.ActiveTreeVersionId == candidate.ActiveTreeVersionId &&
                reconciled.Operations.Contains(operation)) return;
            throw;
        }
    }
    private async Task<Spreadsheet> Metadata(CancellationToken ct)
    {
        if (options.GetMissingSheetsSettings().Count > 0 || string.IsNullOrWhiteSpace(options.SystemSpreadsheetId) || options.SpreadsheetId == options.SystemSpreadsheetId)
            throw new ExternalStorageException("Cấu hình business spreadsheet phải đầy đủ và khác system spreadsheet.");
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("?fields=sheets.properties(sheetId,title,gridProperties)")), GoogleRequestSemantics.SafeRead, ct).ConfigureAwait(false);
        var value = await response.Content.ReadFromJsonAsync<Spreadsheet>(Json, ct).ConfigureAwait(false);
        return value ?? throw new ExternalStorageException("Không đọc được metadata danh mục.");
    }
    private async Task<CategoryCatalogState> Load(Spreadsheet metadata, CancellationToken ct)
    {
        var existing = Headers.Keys.Where(t => metadata.Sheets.Any(s => s.Properties.Title == t)).ToArray();
        if (existing.Length == 0) return new();
        if (existing.Length != Headers.Count) throw new ExternalStorageException("Schema danh mục chưa đầy đủ; không tự ghi đè dữ liệu chưa xác định.");
        var query = string.Join('&', existing.Select(t => "ranges=" + Uri.EscapeDataString($"'{t}'!A1:O20001")));
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("/values:batchGet?" + query + "&valueRenderOption=UNFORMATTED_VALUE")), GoogleRequestSemantics.SafeRead, ct).ConfigureAwait(false);
        var value = await response.Content.ReadFromJsonAsync<BatchValues>(Json, ct).ConfigureAwait(false);
        if (value?.ValueRanges.Length != Headers.Count) throw new ExternalStorageException("Google trả thiếu bảng danh mục.");
        var tables = new Dictionary<string, string[][]>(StringComparer.Ordinal);
        for (var i = 0; i < existing.Length; i++)
        {
            var title = existing[i]; var range = value.ValueRanges[i];
            if (!range.Range.StartsWith("'" + title + "'!", StringComparison.Ordinal) && !range.Range.StartsWith(title + "!", StringComparison.Ordinal))
                throw new ExternalStorageException("Google trả range khác yêu cầu.");
            var rows = (range.Values ?? []).Select(r => r.Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText()).ToArray()).ToArray();
            if (rows.Length == 0 || !rows[0].SequenceEqual(Headers[title]) || rows.Length > 20000)
                throw new ExternalStorageException("Header/schema hoặc kích thước bảng danh mục không hợp lệ; không overwrite.");
            if (rows.Skip(1).Any(r => r.Length != Headers[title].Length)) throw new ExternalStorageException("Dòng danh mục không đủ cột.");
            tables[title] = rows.Skip(1).ToArray();
        }
        try
        {
            var marker = tables["SchemaVersions"].Single();
            if (marker[0] != "CATEGORY_V1") throw new ExternalStorageException("Schema business chưa được hỗ trợ.");
            var state = new CategoryCatalogState { ActiveTreeVersionId = marker[3], Revision = long.Parse(marker[4], CultureInfo.InvariantCulture) };
            foreach (var r in tables["Categories"]) state.Categories.Add(new(r[0],r[1],Empty(r[2]),int.Parse(r[3]),int.Parse(r[4]),r[5],r[6],r[7],r[8],r[9],int.Parse(r[10]),r[11],r[12],int.Parse(r[13]),r[14]));
            foreach (var r in tables["TreeVersions"]) state.Versions.Add(new(r[0],r[1],r[2],r[3],r[4],DateTimeOffset.Parse(r[5],CultureInfo.InvariantCulture),r[6]));
            foreach (var r in tables["Operations"])
            {
                if (r[4] != "CATEGORY_IMPORT" || r[5] != "COMPLETE") throw new ExternalStorageException("Operation business không được hỗ trợ.");
                state.Operations.Add(new(r[0],r[1],r[2],r[3],r[6],int.Parse(r[7]),DateTimeOffset.Parse(r[8],CultureInfo.InvariantCulture),r[9],r[10],bool.Parse(r[11])));
            }
            if (state.Operations.Count != state.Revision || !state.Versions.Any(v => v.TreeVersionId == state.ActiveTreeVersionId) ||
                state.Versions.Any(v => v.State != "DRAFT") || state.Categories.Any(n => n.State != "DRAFT")) throw new ExternalStorageException("Pointer hoặc state danh mục không hợp lệ.");
            // Reject unmanaged edits instead of overwriting unknown rows at computed append offsets.
            var expectedTables = Tables(state);
            foreach (var table in tables)
                if (!table.Value.Select(r => string.Join("\0", r)).SequenceEqual(expectedTables[table.Key].Skip(1).Select(r => string.Join("\0", r))))
                    throw new ExternalStorageException("Journal/provenance danh mục không nhất quán; cần đối soát trong chế độ bảo trì.");
            foreach (var version in state.Versions)
            {
                var nodes = state.Categories.Where(n => n.TreeVersionId == version.TreeVersionId).ToArray();
                var byId = nodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
                if (nodes.Any(n => n.ParentId is not null && !byId.ContainsKey(n.ParentId))) throw new ExternalStorageException("Danh mục thiếu cha.");
                var inputs = nodes.Select(n => new CategoryInput(n.SourceSheet,n.SourceRow,n.SourceStt,n.ParentId is null ? null : byId[n.ParentId].SourceStt,n.FLevel,n.Name,n.SegmentCode,n.Definition)).ToArray();
                if (CategoryCatalogService.Validate(inputs).Count > 0) throw new ExternalStorageException("Cây danh mục lưu trữ không hợp lệ.");
            }
            return state;
        }
        catch (Exception e) when (e is FormatException or OverflowException or InvalidOperationException or ArgumentException)
        { throw new ExternalStorageException("Dữ liệu schema danh mục không hợp lệ."); }
    }
    private static Dictionary<string, List<string[]>> Tables(CategoryCatalogState s)
    {
        var tables = Headers.ToDictionary(p => p.Key, p => new List<string[]> { p.Value }, StringComparer.Ordinal);
        var last = s.Versions.Last();
        tables["SchemaVersions"].Add(["CATEGORY_V1", last.CreatedAt.ToString("O"), "TASK-006 DRAFT snapshot", s.ActiveTreeVersionId!, s.Revision.ToString(CultureInfo.InvariantCulture)]);
        tables["Categories"].AddRange(s.Categories.Select(n => new[] {n.CategoryId,n.TreeVersionId,n.ParentId??"",n.FLevel.ToString(CultureInfo.InvariantCulture),n.DisplayOrder.ToString(CultureInfo.InvariantCulture),n.Name,n.SegmentCode,n.FullCode,n.Definition,n.State,n.Version.ToString(CultureInfo.InvariantCulture),n.SourceKey,n.SourceSheet,n.SourceRow.ToString(CultureInfo.InvariantCulture),n.SourceStt}));
        tables["TreeVersions"].AddRange(s.Versions.Select(v => new[] {v.TreeVersionId,v.State,v.SourceHash,v.MappingVersion,v.ContentHash,v.CreatedAt.ToString("O"),v.ActorUserId}));
        foreach (var o in s.Operations)
        {
            var version = s.Versions.Single(v => v.TreeVersionId == o.TreeVersionId);
            tables["Operations"].Add([o.OperationId,o.IdempotencyKey,o.ActorUserId,o.RequestHash,"CATEGORY_IMPORT","COMPLETE",o.TreeVersionId,o.TotalRows.ToString(CultureInfo.InvariantCulture),o.CompletedAt.ToString("O"),o.SourceHash,o.MappingVersion,o.AlreadyImported.ToString()]);
            tables["ImportJobs"].Add([o.OperationId,o.SourceHash,o.MappingVersion,"COMPLETE",o.TotalRows.ToString(CultureInfo.InvariantCulture),o.TotalRows.ToString(CultureInfo.InvariantCulture),o.ActorUserId,o.CompletedAt.ToString("O")]);
            if (!o.AlreadyImported) tables["ImportRows"].AddRange(s.Categories.Where(n => n.TreeVersionId == o.TreeVersionId).Select(n => new[] {o.OperationId,n.SourceSheet,n.SourceRow.ToString(CultureInfo.InvariantCulture),n.SourceStt,"COMPLETE",n.CategoryId,"DRAFT; giữ riêng tên nhiều mã"}));
            tables["AuditEvents"].Add(["AUDIT-"+o.OperationId,o.OperationId,o.ActorUserId,o.TreeVersionId,o.AlreadyImported?"CATEGORY_IMPORT_NOOP":"CATEGORY_IMPORT","{}",JsonSerializer.Serialize(new {o.TotalRows,State="DRAFT"}),"Commit preview đã xác nhận",o.CompletedAt.ToString("O")]);
        }
        return tables;
    }
    private Uri UriFor(string suffix) => new(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + suffix);
    private static int NextId(HashSet<int> ids) { var id = 6000; while (!ids.Add(id)) id++; return id; }
    private static string? Empty(string value) => value.Length == 0 ? null : value;
    private sealed record Spreadsheet(Sheet[] Sheets);
    private sealed record Sheet(Properties Properties);
    private sealed record Properties(int SheetId, string Title, Grid GridProperties);
    private sealed record Grid(int RowCount, int ColumnCount);
    private sealed record BatchValues(ValueRange[] ValueRanges);
    private sealed record ValueRange(string Range, JsonElement[][]? Values);
}
