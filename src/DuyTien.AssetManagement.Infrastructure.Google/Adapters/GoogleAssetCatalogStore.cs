using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

// Explicit asset writes only; never seeds reference data/rules and never touches auth/category journals.
public sealed class GoogleAssetCatalogStore(GoogleWorkspaceOptions options, GoogleApiRequestExecutor api) : IAssetCatalogStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly IReadOnlyDictionary<string, string[]> Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["AssetSchemaVersions"] = ["SchemaVersion", "Revision"],
        ["Assets"] = ["AssetId", "ObjectCode", "IssuedCCC", "CodeRuleId", "CategoryId", "Name", "Type", "OrgUnitId", "LocationId", "CustodianRef", "Serial", "Model", "Manufacturer", "RecordedOn", "Condition", "Notes", "DataState", "IsDeleted", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Version"],
        ["IdCounters"] = ["CounterKey", "LastAllocated"],
        ["LegacyAliases"] = ["AliasId", "Namespace", "OldCode", "AssetId", "IsActive"],
        ["AssetOperations"] = ["OperationId", "IdempotencyKey", "Actor", "RequestHash", "Action", "EntityId", "BeforeJson", "AfterJson", "AliasesAfterJson", "CounterKey", "CounterValue", "Reason", "CompletedAt"],
        ["AssetAuditEvents"] = ["EventId", "OperationId", "Actor", "EntityId", "Action", "BeforeJson", "AfterJson", "AliasesAfterJson", "Reason", "Timestamp"]
    };
    public static readonly IReadOnlyDictionary<string, string[]> ReferenceHeaders = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["CodeRules"] = ["CodeRuleId", "CategoryRootId", "Prefix", "CCC", "Status", "ApprovedForDemo", "Version"],
        ["OrgUnits"] = ["OrgUnitId", "ParentId", "Name", "State", "Version"],
        ["Locations"] = ["LocationId", "ParentId", "OrgUnitId", "Name", "State", "Version"]
    };
    public async Task<AssetCatalogState> ReadAsync(CancellationToken ct = default) => await Load(await Metadata(ct), ct);
    public async Task CommitAsync(AssetCatalogState candidate, long expectedRevision, CancellationToken ct = default)
    {
        var requests = await PrepareAsync(candidate, expectedRevision, ct);
        var body = JsonSerializer.SerializeToUtf8Bytes(new { requests }, Json);
        if (body.Length > 1800 * 1024) throw new AssetValidationException("Atomic batch vượt 1800 KiB demo; không ghi một phần.");
        try
        {
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, UriFor(":batchUpdate")) { Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } } }, GoogleRequestSemantics.NonIdempotentWrite, ct);
        }
        catch (ExternalStorageException)
        {
            // Never repeat addSheet/counter allocation blindly after a lost response.
            var actual = await ReadAsync(ct);
            if (actual.Revision == candidate.Revision && Equal(actual.Operations.LastOrDefault(), candidate.Operations.Last())) return;
            throw;
        }
    }
    internal async Task<List<object>> PrepareAsync(AssetCatalogState candidate, long expectedRevision, CancellationToken ct, IEnumerable<int>? reservedIds = null)
    {
        var metadata = await Metadata(ct); var previous = await Load(metadata, ct);
        if (previous.Revision != expectedRevision || candidate.Operations.Count <= previous.Operations.Count || candidate.Revision != expectedRevision + candidate.Operations.Count - previous.Operations.Count ||
            !Equal(previous.Operations, candidate.Operations.Take(previous.Operations.Count)) || !previous.Rules.SequenceEqual(candidate.Rules) ||
            !previous.OrgUnits.SequenceEqual(candidate.OrgUnits) || !previous.Locations.SequenceEqual(candidate.Locations))
            throw new AssetConflictException("Version hoặc reference Google đã thay đổi; không ghi đè.");
        ValidateJournal(candidate);
        var tables = Tables(candidate); var used = metadata.Sheets.Select(s => s.Properties.SheetId).ToHashSet(); if (reservedIds is not null) used.UnionWith(reservedIds); var requests = new List<object>();
        foreach (var pair in Headers)
        {
            var old = metadata.Sheets.SingleOrDefault(s => s.Properties.Title == pair.Key)?.Properties;
            var rows = tables[pair.Key];
            if (rows.Count > 5000) throw new AssetValidationException("Bảng vượt giới hạn 5000 dòng demo; chưa ghi.");
            if (rows.Any(row => row.Any(cell => cell.Length > 45000))) throw new AssetValidationException("Lịch sử vượt giới hạn 45000 ký tự một ô demo; chưa ghi, không bỏ alias lịch sử.");
            var id = old?.SheetId ?? NextId(used);
            if (old is null) requests.Add(new { addSheet = new { properties = new { sheetId = id, title = pair.Key, gridProperties = new { rowCount = Math.Max(100, rows.Count), columnCount = pair.Value.Length } } } });
            else
            {
                if (old.GridProperties.ColumnCount < pair.Value.Length) throw new ExternalStorageException("Schema tài sản thiếu cột.");
                if (old.GridProperties.RowCount < rows.Count) requests.Add(new { appendDimension = new { sheetId = id, dimension = "ROWS", length = rows.Count - old.GridProperties.RowCount } });
            }
            requests.Add(new { updateCells = new { start = new { sheetId = id, rowIndex = 0, columnIndex = 0 }, rows = rows.Select(r => new { values = r.Select(v => new { userEnteredValue = new { stringValue = v } }).ToArray() }).ToArray(), fields = "userEnteredValue" } });
        }
        return requests;
    }
    private async Task<Spreadsheet> Metadata(CancellationToken ct)
    {
        if (options.GetMissingSheetsSettings().Count > 0 || string.IsNullOrWhiteSpace(options.SystemSpreadsheetId) || options.SpreadsheetId == options.SystemSpreadsheetId)
            throw new ExternalStorageException("Business spreadsheet phải đầy đủ và khác system spreadsheet.");
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("?fields=sheets.properties(sheetId,title,gridProperties)")), GoogleRequestSemantics.SafeRead, ct);
        var metadata = await ReadJson<Spreadsheet>(response, ct);
        if (metadata.Sheets is null || metadata.Sheets.Any(s => s?.Properties?.GridProperties is null || string.IsNullOrWhiteSpace(s.Properties.Title)) || metadata.Sheets.Select(s => s.Properties.Title).Distinct(StringComparer.Ordinal).Count() != metadata.Sheets.Length || metadata.Sheets.Select(s => s.Properties.SheetId).Distinct().Count() != metadata.Sheets.Length)
            throw new ExternalStorageException("Metadata bảng tài sản không hợp lệ.");
        return metadata;
    }
    private async Task<AssetCatalogState> Load(Spreadsheet metadata, CancellationToken ct)
    {
        var managed = Headers.Keys.Where(t => metadata.Sheets.Any(s => s.Properties.Title == t)).ToArray();
        if (managed.Length != 0 && managed.Length != Headers.Count) throw new ExternalStorageException("Schema tài sản chưa đầy đủ; không overwrite dữ liệu chưa xác định.");
        var titles = managed.Concat(ReferenceHeaders.Keys.Where(t => metadata.Sheets.Any(s => s.Properties.Title == t))).ToArray();
        var tables = new Dictionary<string, string[][]>(StringComparer.Ordinal);
        if (titles.Length > 0)
        {
            var query = string.Join('&', titles.Select(t => "ranges=" + Uri.EscapeDataString($"'{t}'!A1:AZ5001")));
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("/values:batchGet?" + query + "&valueRenderOption=UNFORMATTED_VALUE")), GoogleRequestSemantics.SafeRead, ct);
            var result = await ReadJson<BatchValues>(response, ct);
            if (result.ValueRanges?.Length != titles.Length) throw new ExternalStorageException("Google trả thiếu bảng tài sản.");
            for (var i = 0; i < titles.Length; i++)
            {
                var title = titles[i]; var range = result.ValueRanges[i]; var header = Headers.TryGetValue(title, out var h) ? h : ReferenceHeaders[title];
                if (range?.Range is null || !range.Range.StartsWith("'" + title + "'!", StringComparison.Ordinal) && !range.Range.StartsWith(title + "!", StringComparison.Ordinal)) throw new ExternalStorageException("Range tài sản khác yêu cầu.");
                if (range.Values?.Any(row => row is null) == true) throw new ExternalStorageException("Google trả dòng tài sản null.");
                var rows = (range.Values ?? []).Select(r => r.Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText()).ToArray()).ToArray();
                if (rows.Length == 0 || rows.Length > 5000 || !rows[0].SequenceEqual(header) || rows.Skip(1).Any(r => r.Length != header.Length)) throw new ExternalStorageException("Header/dòng/schema tài sản không hợp lệ; dừng để đối soát.");
                tables[title] = rows.Skip(1).ToArray();
            }
        }
        try
        {
            var state = new AssetCatalogState();
            foreach (var r in Rows("CodeRules")) { Positive(r[6]); state.Rules.Add(new(r[0], r[1], r[2], r[3], r[4], bool.Parse(r[5]), long.Parse(r[6], CultureInfo.InvariantCulture))); }
            foreach (var r in Rows("OrgUnits")) { Positive(r[4]); state.OrgUnits.Add(new(r[0], Empty(r[1]), r[2], Active(r[3]))); }
            foreach (var r in Rows("Locations")) { Positive(r[5]); state.Locations.Add(new(r[0], Empty(r[1]), r[2], r[3], Active(r[4]))); }
            ValidateReferences(state);
            if (managed.Length == 0) return state;
            var marker = tables["AssetSchemaVersions"].Single();
            if (marker[0] != "ASSET_DEMO_V1") throw new ExternalStorageException("Schema tài sản chưa được hỗ trợ.");
            state.Revision = long.Parse(marker[1], CultureInfo.InvariantCulture);
            foreach (var r in Rows("AssetOperations"))
            {
                var before = r[6] == "null" ? null : Deserialize<AssetRecord>(r[6]); var after = Deserialize<AssetRecord>(r[7]);
                if (r[5] != after.AssetId) throw new ExternalStorageException("Operation entity không nhất quán.");
                state.Operations.Add(new(r[0], r[1], r[2], r[3], r[4], before, after, Deserialize<AssetAlias[]>(r[8]), r[9], long.Parse(r[10], CultureInfo.InvariantCulture), r[11], DateTimeOffset.Parse(r[12], CultureInfo.InvariantCulture)));
            }
            // Derive the authoritative state from the append-only journal; compare every managed row.
            foreach (var group in state.Operations.GroupBy(o => o.Result.AssetId))
            {
                var last = group.Last(); state.Assets.Add(last.Result); state.Aliases.AddRange(last.AliasesAfter);
            }
            state.Counters.AddRange(state.Operations.Where(o => o.Action == "CREATE").GroupBy(o => o.CounterKey).Select(g => new AssetCounter(g.Key, g.Last().CounterValue)));
            ValidateJournal(state);
            var expected = Tables(state);
            foreach (var pair in Headers)
                {
                    var actualRows = Rows(pair.Key).Select(r => string.Join("\0", r));
                    var expectedRows = expected[pair.Key].Skip(1).Select(r => string.Join("\0", r));
                    if (pair.Key is "Assets" or "IdCounters" or "LegacyAliases") { actualRows = actualRows.Order(StringComparer.Ordinal); expectedRows = expectedRows.Order(StringComparer.Ordinal); }
                    if (!actualRows.SequenceEqual(expectedRows)) throw new ExternalStorageException("Journal/counter/alias/audit không nhất quán; cần bảo trì, không overwrite.");
                }
            return state;
            string[][] Rows(string title) => tables.GetValueOrDefault(title) ?? [];
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException or InvalidOperationException or ArgumentException or AuthValidationException or AssetValidationException)
        { throw new ExternalStorageException("Dữ liệu/schema tài sản không hợp lệ; cần đối soát."); }
    }
    private static void ValidateReferences(AssetCatalogState s)
    {
        _ = ScopeHierarchy.Create(AuthScopeType.OrgUnit, s.OrgUnits.Select(n => new ScopeNode(n.Id, n.ParentId)));
        _ = ScopeHierarchy.Create(AuthScopeType.Location, s.Locations.Select(n => new ScopeNode(n.Id, n.ParentId)));
        if (s.Locations.Any(l => !s.OrgUnits.Any(o => o.Id == l.OrgUnitId)) || s.Rules.Select(r => r.RuleId).Distinct(StringComparer.Ordinal).Count() != s.Rules.Count || s.Rules.Any(r => string.IsNullOrWhiteSpace(r.RuleId) || r.Status is not ("DEMO" or "PROPOSED" or "RETIRED"))) throw new ExternalStorageException("Reference/rule tài sản không hợp lệ.");
    }
    private static void ValidateJournal(AssetCatalogState s)
    {
        if (s.Revision != s.Operations.Count || s.Operations.Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count() != s.Operations.Count || s.Operations.Select(o => (o.Actor, o.IdempotencyKey)).Distinct().Count() != s.Operations.Count) throw new ExternalStorageException("Ledger tài sản không hợp lệ.");
        var assets = new Dictionary<string, AssetRecord>(StringComparer.Ordinal); var aliases = new Dictionary<string, AssetAlias>(StringComparer.Ordinal); var counters = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var op in s.Operations)
        {
            if (op.AliasesAfter.Any(a => a is null)) throw new ExternalStorageException("Alias snapshot không hợp lệ.");
            var a = op.Result; var before = assets.GetValueOrDefault(a.AssetId);
            _ = AssetCrudService.ValidateInput(new(a.Name, a.CategoryId, a.OrgUnitId, a.LocationId, a.Type, a.CustodianRef, a.Serial, a.Model, a.Manufacturer, a.RecordedOn, a.Condition, a.Notes, op.AliasesAfter.Where(x => x.IsActive).Select(x => x.OldCode).ToArray()));
            if (op.Before != before || a.DataState != "DRAFT_DEMO" || a.Type != "MACHINE" || a.Version != (before?.Version ?? 0) + 1 || a.UpdatedBy != op.Actor || a.UpdatedAt != op.CompletedAt || string.IsNullOrWhiteSpace(op.IdempotencyKey) || string.IsNullOrWhiteSpace(op.RequestHash)) throw new ExternalStorageException("Version/history tài sản không hợp lệ.");
            if (before is null)
            {
                if (op.Action != "CREATE" || a.IsDeleted || a.CreatedBy != op.Actor || a.CreatedAt != op.CompletedAt || !op.CounterKey.EndsWith("-" + a.IssuedCcc, StringComparison.Ordinal) || op.CounterValue <= counters.GetValueOrDefault(op.CounterKey) || op.CounterValue > 999999 || a.ObjectCode != op.CounterKey + "-" + op.CounterValue.ToString("D6", CultureInfo.InvariantCulture)) throw new ExternalStorageException("Counter cấp mã không hợp lệ.");
                counters[op.CounterKey] = op.CounterValue;
            }
            else
            {
                if (a.ObjectCode != before.ObjectCode || a.IssuedCcc != before.IssuedCcc || a.CodeRuleId != before.CodeRuleId || a.CreatedAt != before.CreatedAt || a.CreatedBy != before.CreatedBy || op.CounterKey != "" || op.CounterValue != 0 ||
                    (op.Action == "MOVE" ? a.IsDeleted != before.IsDeleted || a != (before with { CategoryId = a.CategoryId, OrgUnitId = a.OrgUnitId, LocationId = a.LocationId, UpdatedBy = a.UpdatedBy, UpdatedAt = a.UpdatedAt, Version = a.Version }) : op.Action == "UPDATE" ? before.IsDeleted || a.IsDeleted : op.Action == "DELETE" ? before.IsDeleted || !a.IsDeleted : op.Action == "RESTORE" ? !before.IsDeleted || a.IsDeleted : true)) throw new ExternalStorageException("ID/code/lifecycle không bất biến.");
            }
            foreach (var prior in aliases.Values.Where(x => x.AssetId == a.AssetId).ToArray())
                if (!op.AliasesAfter.Any(x => x.AliasId == prior.AliasId && x.OldCode == prior.OldCode && x.AssetId == prior.AssetId)) throw new ExternalStorageException("Không được mất mapping alias lịch sử.");
            foreach (var alias in op.AliasesAfter)
            {
                if (alias.AssetId != a.AssetId || alias.Namespace != "DEMO" || string.IsNullOrWhiteSpace(alias.OldCode) || aliases.Values.Any(x => string.Equals(x.OldCode, alias.OldCode, StringComparison.OrdinalIgnoreCase) && x.AssetId != alias.AssetId)) throw new ExternalStorageException("Alias mapping không unique.");
                aliases[alias.AliasId] = alias;
            }
            assets[a.AssetId] = a;
        }
        if (aliases.Values.Select(a => a.OldCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != aliases.Count || aliases.Values.Any(x => assets.Values.Any(a => a.AssetId != x.AssetId && string.Equals(a.ObjectCode, x.OldCode, StringComparison.OrdinalIgnoreCase)))) throw new ExternalStorageException("Alias/mã tài sản không unique.");
        if (assets.Values.Select(a => a.ObjectCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != assets.Count || !Equal(assets.Values.OrderBy(a => a.AssetId, StringComparer.Ordinal), s.Assets.OrderBy(a => a.AssetId, StringComparer.Ordinal)) ||
            !Equal(aliases.Values.OrderBy(a => a.AliasId, StringComparer.Ordinal), s.Aliases.OrderBy(a => a.AliasId, StringComparer.Ordinal)) ||
            !Equal(counters.Select(p => new AssetCounter(p.Key, p.Value)).OrderBy(c => c.Key, StringComparer.Ordinal), s.Counters.OrderBy(c => c.Key, StringComparer.Ordinal))) throw new ExternalStorageException("Trạng thái tài sản khác journal.");
    }
    private static Dictionary<string, List<string[]>> Tables(AssetCatalogState s)
    {
        var t = Headers.ToDictionary(p => p.Key, p => new List<string[]> { p.Value }, StringComparer.Ordinal);
        t["AssetSchemaVersions"].Add(["ASSET_DEMO_V1", s.Revision.ToString(CultureInfo.InvariantCulture)]);
        t["Assets"].AddRange(s.Assets.OrderBy(a => a.AssetId, StringComparer.Ordinal).Select(a => new[] { a.AssetId, a.ObjectCode, a.IssuedCcc, a.CodeRuleId, a.CategoryId, a.Name, a.Type, a.OrgUnitId, a.LocationId, a.CustodianRef, a.Serial, a.Model, a.Manufacturer, a.RecordedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", a.Condition, a.Notes, a.DataState, a.IsDeleted.ToString(), a.CreatedAt.ToString("O"), a.CreatedBy, a.UpdatedAt.ToString("O"), a.UpdatedBy, a.Version.ToString(CultureInfo.InvariantCulture) }));
        t["IdCounters"].AddRange(s.Counters.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => new[] { c.Key, c.LastAllocated.ToString(CultureInfo.InvariantCulture) }));
        t["LegacyAliases"].AddRange(s.Aliases.OrderBy(a => a.AliasId, StringComparer.Ordinal).Select(a => new[] { a.AliasId, a.Namespace, a.OldCode, a.AssetId, a.IsActive.ToString() }));
        foreach (var o in s.Operations)
        {
            var before = JsonSerializer.Serialize(o.Before, Json); var after = JsonSerializer.Serialize(o.Result, Json); var aliases = JsonSerializer.Serialize(o.AliasesAfter, Json);
            t["AssetOperations"].Add([o.OperationId, o.IdempotencyKey, o.Actor, o.RequestHash, o.Action, o.Result.AssetId, before, after, aliases, o.CounterKey, o.CounterValue.ToString(CultureInfo.InvariantCulture), o.Reason, o.CompletedAt.ToString("O")]);
            t["AssetAuditEvents"].Add(["AUDIT-" + o.OperationId, o.OperationId, o.Actor, o.Result.AssetId, o.Action, before, after, aliases, o.Reason, o.CompletedAt.ToString("O")]);
        }
        return t;
    }
    private static async Task<T> ReadJson<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new ExternalStorageException("Google trả thiếu dữ liệu tài sản."); }
        catch (JsonException) { throw new ExternalStorageException("Google trả JSON/schema tài sản không hợp lệ."); }
        catch (HttpRequestException e) { throw new ExternalStorageTransientException("Không đọc được response tài sản Google.", false, e); }
        catch (IOException e) { throw new ExternalStorageTransientException("Không đọc được response tài sản Google.", false, e); }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested) { throw new ExternalStorageTransientException("Response tài sản Google timeout.", false, e); }
    }
    private static bool Equal<T>(T a, T b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Json) ?? throw new ExternalStorageException("Thiếu snapshot tài sản.");
    private static void Positive(string s) { if (long.Parse(s, CultureInfo.InvariantCulture) < 1) throw new FormatException(); }
    private static bool Active(string state) => state switch { "ACTIVE" => true, "INACTIVE" => false, _ => throw new FormatException() };
    private static string? Empty(string s) => s.Length == 0 ? null : s;
    private static int NextId(HashSet<int> used) { var id = 7000; while (!used.Add(id)) id++; return id; }
    private Uri UriFor(string suffix) => new(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + suffix);
    private sealed record Spreadsheet(Sheet[] Sheets);
    private sealed record Sheet(Properties Properties);
    private sealed record Properties(int SheetId, string Title, Grid GridProperties);
    private sealed record Grid(int RowCount, int ColumnCount);
    private sealed record BatchValues(ValueRange[] ValueRanges);
    private sealed record ValueRange(string Range, JsonElement[][]? Values);
}
