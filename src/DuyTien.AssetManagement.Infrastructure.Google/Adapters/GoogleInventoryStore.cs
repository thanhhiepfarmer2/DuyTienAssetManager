using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleInventoryStore(GoogleWorkspaceOptions options, GoogleApiRequestExecutor api) : IInventoryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] Materialized = ["Items", "StockBalances", "ItemImportJobs", "ItemImportPlan"];
    public static readonly IReadOnlyDictionary<string, string[]> Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
    { ["InventorySchemaVersions"] = ["SchemaVersion", "Revision"], ["Items"] = ["ItemId", "RecordJson"], ["StockBalances"] = ["BalanceId", "RecordJson"], ["ItemImportJobs"] = ["JobId", "RecordJson"], ["ItemImportPlan"] = ["PlanId", "RecordJson"], ["InventoryOperations"] = ["OperationId", "RecordJson"], ["InventoryDetails"] = ["DetailId", "RecordJson"], ["InventoryAuditEvents"] = ["OperationId", "Actor", "Action", "Reason", "Timestamp"] };
    public async Task<InventoryState> ReadAsync(CancellationToken ct = default) => await Load(await Metadata(ct), ct);
    public async Task CommitAsync(InventoryState candidate, long expectedRevision, CancellationToken ct = default)
    {
        var requests = await PrepareAsync(candidate, expectedRevision, ct);
        var body = JsonSerializer.SerializeToUtf8Bytes(new { requests }, ResponseJson); if (body.Length > 1800 * 1024) throw new InventoryValidationException("Chunk atomic vượt1800KiB; chưa ghi. Giữ job để đối soát, không báo COMPLETE.");
        try { using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, UriFor(":batchUpdate")) { Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } } }, GoogleRequestSemantics.NonIdempotentWrite, ct); }
        catch (ExternalStorageException) { var actual = await ReadAsync(ct); if (actual.Revision == candidate.Revision && JsonSerializer.Serialize(actual.Operations.LastOrDefault(), Json) == JsonSerializer.Serialize(candidate.Operations.Last(), Json)) return; throw; }
    }
    internal async Task<List<object>> PrepareAsync(InventoryState candidate, long expectedRevision, CancellationToken ct, IEnumerable<int>? extraSheetIds = null)
    {
        var meta = await Metadata(ct); var previous = await Load(meta, ct);
        if (previous.Revision != expectedRevision || candidate.Revision != expectedRevision + 1 || candidate.Operations.Count != previous.Operations.Count + 1 || JsonSerializer.Serialize(previous.Operations, Json) != JsonSerializer.Serialize(candidate.Operations.Take(previous.Operations.Count), Json)) throw new InventoryConflictException("Revision/journal đổi; không overwrite.");
        InventoryIntegrity.Validate(candidate); var tables = Tables(candidate); var oldTables = Tables(previous);
        foreach (var title in Materialized)
        {
            var wanted = tables[title].Skip(1).ToDictionary(r => r[0], StringComparer.Ordinal); var order = oldTables[title].Skip(1).Select(r => r[0]).ToHashSet(StringComparer.Ordinal);
            if (order.Any(k => !wanted.ContainsKey(k))) throw new InventoryConflictException("Không xóa ID/plan đã lưu.");
            tables[title] = new() { Headers[title] }; tables[title].AddRange(oldTables[title].Skip(1).Select(r => wanted[r[0]])); tables[title].AddRange(wanted.Where(p => !order.Contains(p.Key)).Select(p => p.Value));
        }
        var used = meta.Sheets.Select(s => s.Properties.SheetId).Concat(extraSheetIds ?? []).ToHashSet(); var requests = new List<object>();
        foreach (var pair in tables)
        {
            var rows = pair.Value; if (rows.Count > 100001 || rows.Any(r => r.Any(v => v.Length > 45000))) throw new InventoryValidationException("Inventory vượt100000rows/tab hoặc45000chars/cell; chưa ghi.");
            var existing = meta.Sheets.SingleOrDefault(s => s.Properties.Title == pair.Key)?.Properties; var id = existing?.SheetId ?? NextId(used);
            if (existing is null) requests.Add(new { addSheet = new { properties = new { sheetId = id, title = pair.Key, gridProperties = new { rowCount = Math.Max(100, rows.Count), columnCount = Headers[pair.Key].Length } } } });
            else { if (existing.GridProperties.ColumnCount < Headers[pair.Key].Length) throw new ExternalStorageException("Schema thiếu cột."); if (existing.GridProperties.RowCount < rows.Count) requests.Add(new { appendDimension = new { sheetId = id, dimension = "ROWS", length = rows.Count - existing.GridProperties.RowCount } }); }
            var old = oldTables[pair.Key];
            for (var row = 0; row < rows.Count;)
            {
                if (existing is not null && row < old.Count && rows[row].SequenceEqual(old[row], StringComparer.Ordinal)) { row++; continue; }
                var start = row; var changed = new List<string[]>(); while (row < rows.Count && (existing is null || row >= old.Count || !rows[row].SequenceEqual(old[row], StringComparer.Ordinal))) { changed.Add(rows[row]); row++; }
                requests.Add(new { updateCells = new { start = new { sheetId = id, rowIndex = start, columnIndex = 0 }, rows = changed.Select(r => new { values = r.Select(v => new { userEnteredValue = new { stringValue = v } }).ToArray() }).ToArray(), fields = "userEnteredValue" } });
            }
        }
        return requests;
    }
    private async Task<InventoryState> Load(Spreadsheet meta, CancellationToken ct)
    {
        var present = Headers.Keys.Where(k => meta.Sheets.Any(s => s.Properties.Title == k)).ToArray(); if (present.Length == 0) return new(); if (present.Length != Headers.Count) throw new ExternalStorageException("Partial inventory schema; không overwrite.");
        var query = string.Join('&', present.Select(t => "ranges=" + Uri.EscapeDataString("'" + t + "'!A1:AZ100002"))); using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("/values:batchGet?" + query + "&valueRenderOption=UNFORMATTED_VALUE")), GoogleRequestSemantics.SafeRead, ct);
        var values = await ReadJson<Batch>(response, ct); if (values.ValueRanges?.Length != present.Length) throw new ExternalStorageException("Inventory thiếu bảng."); var tables = new Dictionary<string, string[][]>(StringComparer.Ordinal);
        for (var i = 0; i < present.Length; i++)
        {
            var title = present[i]; var range = values.ValueRanges[i]; if (range?.Range is null || !range.Range.StartsWith("'" + title + "'!", StringComparison.Ordinal) && !range.Range.StartsWith(title + "!", StringComparison.Ordinal) || range.Values is null || range.Values.Any(r => r is null)) throw new ExternalStorageException("Inventory range/null row sai.");
            var rows = range.Values.Select(r => r.Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText()).ToArray()).ToArray(); if (rows.Length is < 1 or > 100001 || !rows[0].SequenceEqual(Headers[title]) || rows.Skip(1).Any(r => r.Length != Headers[title].Length || r.Any(v => v.Length > 45000))) throw new ExternalStorageException("Inventory header/cell/size sai."); tables[title] = rows.Skip(1).ToArray();
        }
        try
        {
            var marker = tables["InventorySchemaVersions"].Single(); if (marker[0] is not ("INVENTORY_V1" or "INVENTORY_V1_TREE")) throw new InventoryValidationException("Schema version không hỗ trợ."); var s = new InventoryState { Revision = long.Parse(marker[1], CultureInfo.InvariantCulture) };
            T Decode<T>(string[] row, Func<T, string> identity) { var value = JsonSerializer.Deserialize<T>(row[1], Json) ?? throw new InventoryValidationException("Null row."); if (identity(value) != row[0]) throw new InventoryValidationException("Identity mismatch."); return value; }
            s.Items.AddRange(tables["Items"].Select(r => Decode<ItemRecord>(r, i => i.ItemId))); s.Balances.AddRange(tables["StockBalances"].Select(r => Decode<StockBalance>(r, b => b.BalanceId))); s.Jobs.AddRange(tables["ItemImportJobs"].Select(r => Decode<ItemImportJob>(r, j => j.JobId))); s.Plan.AddRange(tables["ItemImportPlan"].Select(r => Decode<ItemImportPlanRow>(r, p => p.JobId + ":" + p.Row.SourceRow)));
            var details = tables["InventoryDetails"].Select(r => Decode<Detail>(r, d => d.Id)).ToLookup(d => d.OperationId, StringComparer.Ordinal);
            foreach (var row in tables["InventoryOperations"])
            {
                var op = Decode<InventoryOperation>(row, o => o.OperationId); if (op.Plan is null || op.Items is null || op.Plan.Length != 0 || op.Items.Length != 0) throw new InventoryValidationException("Operation metadata không đúng."); var part = details[op.OperationId].ToArray();
                if (part.Any(d => d.Kind is not ("PLAN" or "ITEM")) || part.GroupBy(d => d.Kind).Any(g => !g.OrderBy(d => d.Index).Select(d => d.Index).SequenceEqual(Enumerable.Range(0, g.Count())))) throw new InventoryValidationException("Detail kind/index sai.");
                s.Operations.Add(op with { Plan = part.Where(d => d.Kind == "PLAN").OrderBy(d => d.Index).Select(d => JsonSerializer.Deserialize<ItemImportPlanRow>(d.Json, Json)!).ToArray(), Items = part.Where(d => d.Kind == "ITEM").OrderBy(d => d.Index).Select(d => JsonSerializer.Deserialize<ItemRecord>(d.Json, Json)!).ToArray() });
            }
            InventoryIntegrity.Validate(s); var expected = Tables(s);
            foreach (var t in tables) { var actual = t.Value.Select(r => string.Join("\0", r)); var wanted = expected[t.Key].Skip(1).Select(r => string.Join("\0", r)); if (Materialized.Contains(t.Key, StringComparer.Ordinal)) { actual = actual.Order(StringComparer.Ordinal); wanted = wanted.Order(StringComparer.Ordinal); } if (!actual.SequenceEqual(wanted)) throw new ExternalStorageException("Inventory journal/audit/materialized không khớp."); }
            return s;
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException or InvalidOperationException or ArgumentException or InventoryValidationException) { throw new ExternalStorageException("Inventory schema/dữ liệu sai; cần đối soát, không overwrite."); }
    }
    private sealed record Detail(string Id, string OperationId, string Kind, int Index, string Json);
    private static Dictionary<string, List<string[]>> Tables(InventoryState s)
    {
        var t = Headers.ToDictionary(p => p.Key, p => new List<string[]> { p.Value }, StringComparer.Ordinal); t["InventorySchemaVersions"].Add([s.Operations.Any(o => o.Action == "ITEM_RECLASSIFY") ? "INVENTORY_V1_TREE" : "INVENTORY_V1", s.Revision.ToString(CultureInfo.InvariantCulture)]);
        t["Items"].AddRange(s.Items.Select(i => new[] { i.ItemId, JsonSerializer.Serialize(i, Json) })); t["StockBalances"].AddRange(s.Balances.Select(b => new[] { b.BalanceId, JsonSerializer.Serialize(b, Json) })); t["ItemImportJobs"].AddRange(s.Jobs.Select(j => new[] { j.JobId, JsonSerializer.Serialize(j, Json) })); t["ItemImportPlan"].AddRange(s.Plan.Select(p => new[] { p.JobId + ":" + p.Row.SourceRow, JsonSerializer.Serialize(p, Json) }));
        foreach (var op in s.Operations)
        {
            t["InventoryOperations"].Add([op.OperationId, JsonSerializer.Serialize(op with { Plan = [], Items = [] }, Json)]);
            foreach (var (kind, data) in new[] { ("PLAN", op.Plan.Select(p => JsonSerializer.Serialize(p, Json)).ToArray()), ("ITEM", op.Items.Select(i => JsonSerializer.Serialize(i, Json)).ToArray()) })
                for (var i = 0; i < data.Length; i++) { var d = new Detail(op.OperationId + ":" + kind + ":" + i, op.OperationId, kind, i, data[i]); t["InventoryDetails"].Add([d.Id, JsonSerializer.Serialize(d, Json)]); }
            t["InventoryAuditEvents"].Add([op.OperationId, op.Actor, op.Action, op.Reason, op.At.ToString("O")]);
        }
        return t;
    }
    private async Task<Spreadsheet> Metadata(CancellationToken ct)
    {
        if (options.GetMissingSheetsSettings().Count > 0 || string.IsNullOrWhiteSpace(options.SystemSpreadsheetId) || options.SystemSpreadsheetId == options.SpreadsheetId) throw new ExternalStorageException("Inventory business spreadsheet phải khác system và đủ config."); using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("?fields=sheets.properties(sheetId,title,gridProperties)")), GoogleRequestSemantics.SafeRead, ct); var value = await ReadJson<Spreadsheet>(response, ct); if (value.Sheets is null || value.Sheets.Any(s => s?.Properties?.GridProperties is null || string.IsNullOrWhiteSpace(s.Properties.Title)) || value.Sheets.Select(s => s.Properties.Title).Distinct(StringComparer.Ordinal).Count() != value.Sheets.Length) throw new ExternalStorageException("Inventory metadata sai."); return value;
    }
    private static async Task<T> ReadJson<T>(HttpResponseMessage response, CancellationToken ct)
    { try { return await response.Content.ReadFromJsonAsync<T>(ResponseJson, ct) ?? throw new ExternalStorageException("Inventory response null."); } catch (JsonException) { throw new ExternalStorageException("Inventory JSON sai."); } catch (IOException e) { throw new ExternalStorageTransientException("Inventory response lỗi đọc.", false, e); } catch (HttpRequestException e) { throw new ExternalStorageTransientException("Inventory response lỗi đọc.", false, e); } catch (OperationCanceledException e) when (!ct.IsCancellationRequested) { throw new ExternalStorageTransientException("Inventory timeout.", false, e); } }
    private Uri UriFor(string suffix) => new(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + suffix);
    private static int NextId(HashSet<int> ids) { var id = 12000; while (!ids.Add(id)) id++; return id; }
    private sealed record Spreadsheet(Sheet[] Sheets); private sealed record Sheet(Properties Properties); private sealed record Properties(int SheetId, string Title, Grid GridProperties); private sealed record Grid(int RowCount, int ColumnCount); private sealed record Batch(ValueRange[] ValueRanges); private sealed record ValueRange(string Range, JsonElement[][]? Values);
}
