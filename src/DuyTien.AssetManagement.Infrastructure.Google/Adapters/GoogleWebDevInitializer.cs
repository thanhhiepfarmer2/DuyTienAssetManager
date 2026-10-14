using System.Text.Json;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed record WebDevInitResult(int CreatedTables, bool NoOp);
// Explicit operator CLI only. Full adapters validate before and after; no auth/PoC changes.
public sealed class GoogleWebDevInitializer(GoogleWorkspaceOptions options, GoogleApiRequestExecutor api,
    GoogleTreeMutationStore joint, GoogleAssetCatalogStore assets)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task VerifyDevAsync(CancellationToken ct = default)
    {
        if (new[] { options.SpreadsheetId, options.SystemSpreadsheetId, options.DriveFolderId }.Distinct(StringComparer.Ordinal).Count() != 3 || new[] { options.SpreadsheetId, options.SystemSpreadsheetId, options.DriveFolderId }.Any(string.IsNullOrWhiteSpace)) throw new ExternalStorageException("DEV resources thiếu/trùng.");
        var resources = new[] { (options.SpreadsheetId!, new[] { "DuyTien Assets DEV", "DuyTien Asset Management - DEV" }, "spreadsheet"), (options.SystemSpreadsheetId!, new[] { "DuyTien Asset Management - DEV System" }, "spreadsheet"), (options.DriveFolderId!, new[] { "DuyTien Assets DEV Files", "DuyTien Asset Management - DEV Files" }, "folder") };
        foreach (var (id, names, kind) in resources)
        {
            var uri = new Uri(new Uri(options.DriveApiBaseUrl), "drive/v3/files/" + Uri.EscapeDataString(id) + "?fields=name,mimeType,ownedByMe,trashed,shared,capabilities(canEdit),permissions(type,role)");
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), GoogleRequestSemantics.SafeRead, ct);
            using var d = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct); var f = d.RootElement;
            if (!names.Contains(f.GetProperty("name").GetString()) || f.GetProperty("mimeType").GetString() != "application/vnd.google-apps." + kind || !f.GetProperty("ownedByMe").GetBoolean() || f.GetProperty("trashed").GetBoolean() || f.GetProperty("shared").GetBoolean() || !f.GetProperty("capabilities").GetProperty("canEdit").GetBoolean()) throw new ExternalStorageException("Resource chưa đúng DEV/owner/ACL.");
            var acl = f.GetProperty("permissions"); if (acl.GetArrayLength() != 1 || acl[0].GetProperty("type").GetString() != "user" || acl[0].GetProperty("role").GetString() != "owner") throw new ExternalStorageException("DEV ACL không owner-only.");
        }
    }
    public async Task<WebDevInitResult> InitializeAsync(CancellationToken ct = default)
    {
        await VerifyDevAsync(ct); var before = await joint.ReadAsync(ct); var meta = await Metadata(ct);
        var headers = GoogleAssetCatalogStore.Headers.Concat(GoogleAssetCatalogStore.ReferenceHeaders).Concat(GoogleInventoryStore.Headers).Concat(GoogleMediaCatalogStore.Headers).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var missing = headers.Keys.Where(t => !meta.Any(m => m.Title == t)).ToArray();
        if (missing.Length == 0) return new(0, true);
        var requests = new List<object>(); var id = meta.Select(m => m.Id).DefaultIfEmpty(0).Max();
        foreach (var title in missing)
        {
            id = checked(id + 1); var rows = new List<string[]> { headers[title] };
            if (title == "AssetSchemaVersions") rows.Add(["ASSET_DEMO_V1", "0"]);
            if (title == "InventorySchemaVersions") rows.Add(["INVENTORY_V1", "0"]);
            if (title == "MediaSchemaVersions") rows.Add(["MEDIA_V1", "0"]);
            requests.Add(new { addSheet = new { properties = new { sheetId = id, title, gridProperties = new { rowCount = 100, columnCount = headers[title].Length } } } });
            requests.Add(Cells(id, 0, rows));
        }
        var body = JsonSerializer.SerializeToUtf8Bytes(new { requests }, Json);
        if (body.Length > 1800 * 1024) throw new ExternalStorageException("Init batch quá lớn; chưa ghi.");
        try { using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, SheetsUri(":batchUpdate")) { Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } } }, GoogleRequestSemantics.NonIdempotentWrite, ct); }
        catch (ExternalStorageException) { var actual = await Metadata(ct); if (!missing.All(t => actual.Any(m => m.Title == t))) throw; }
        var after = await joint.ReadAsync(ct);
        if (JsonSerializer.Serialize(before, Json) != JsonSerializer.Serialize(after, Json)) throw new ExternalStorageException("Init không được đổi dữ liệu/revision đã có.");
        return new(missing.Length, false);
    }
    public async Task<WebDevInitResult> SeedFakeReferencesAsync(string[] categoryIds, CancellationToken ct = default)
    {
        await VerifyDevAsync(ct); var snapshot = await joint.ReadAsync(ct); var meta = await Metadata(ct);
        if (GoogleAssetCatalogStore.ReferenceHeaders.Keys.Any(t => !meta.Any(m => m.Title == t))) throw new ExternalStorageException("Chạy explicit init trước seed demo.");
        var state = snapshot.Assets; var tables = new Dictionary<string, List<string[]>>(StringComparer.Ordinal) { ["CodeRules"] = [], ["OrgUnits"] = [], ["Locations"] = [] };
        foreach (var id in categoryIds.Distinct(StringComparer.Ordinal))
        {
            var n = snapshot.Categories.ActiveNodes.SingleOrDefault(n => n.CategoryId == id);
            if (n is null || n.FLevel != 1 || n.State != "DRAFT" || n.SourceSheet != "F1-F3 Mã ngang" || !n.Definition.StartsWith("FAKE_TASK014_V1", StringComparison.Ordinal) || !n.Name.StartsWith("GIẢ TASK-014", StringComparison.Ordinal)) throw new ExternalStorageException("Chỉ seed rule cho F1 GIẢ TASK014_FAKE; không phê duyệt rule công ty.");
            var ruleId = "W14-RULE-" + n.CategoryId; var old = state.Rules.SingleOrDefault(r => r.RuleId == ruleId);
            if (old is not null) { if (old.CategoryRootId != id || old.Prefix != "WEBDEMO" || old.Ccc != n.SegmentCode || old.Status != "DEMO" || !old.ApprovedForDemo || old.Version != 1) throw new ExternalStorageException("Fixture rule đã khác; không overwrite."); }
            else if (state.Rules.Any(r => r.CategoryRootId == id)) throw new ExternalStorageException("Không thêm rule trùng root.");
            else tables["CodeRules"].Add([ruleId, id, "WEBDEMO", n.SegmentCode, "DEMO", "True", "1"]);
        }
        foreach (var suffix in new[] { "A", "B" })
        {
            var orgId = "W14-ORG-" + suffix; var locId = "W14-LOC-" + suffix; var name = "GIẢ TASK-014 " + suffix;
            var org = state.OrgUnits.SingleOrDefault(o => o.Id == orgId);
            if (org is null) tables["OrgUnits"].Add([orgId, "", name, "ACTIVE", "1"]); else if (org.Name != name || !org.IsActive || org.ParentId is not null) throw new ExternalStorageException("Fixture Org đã đổi; không overwrite.");
            var loc = state.Locations.SingleOrDefault(l => l.Id == locId);
            if (loc is null) tables["Locations"].Add([locId, "", orgId, name, "ACTIVE", "1"]); else if (loc.Name != name || !loc.IsActive || loc.ParentId is not null || loc.OrgUnitId != orgId) throw new ExternalStorageException("Fixture Location đã đổi; không overwrite.");
        }
        var requests = new List<object>();
        foreach (var (title, rows) in tables.Where(p => p.Value.Count > 0))
        {
            var sheet = meta.Single(m => m.Title == title); var offset = 1 + (title == "CodeRules" ? state.Rules.Count : title == "OrgUnits" ? state.OrgUnits.Count : state.Locations.Count);
            if (offset + rows.Count > sheet.Rows) requests.Add(new { appendDimension = new { sheetId = sheet.Id, dimension = "ROWS", length = offset + rows.Count - sheet.Rows } });
            requests.Add(Cells(sheet.Id, offset, rows));
        }
        if (requests.Count == 0) return new(0, true);
        var body = JsonSerializer.SerializeToUtf8Bytes(new { requests }, Json);
        // Never blindly retry append/seed after a lost response. Rerun verifies exact existing fixture values.
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, SheetsUri(":batchUpdate")) { Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } } }, GoogleRequestSemantics.NonIdempotentWrite, ct);
        _ = await assets.ReadAsync(ct); return new(tables.Count(p => p.Value.Count > 0), false);
    }
    private static object Cells(int id, int offset, IEnumerable<string[]> rows) => new { updateCells = new { start = new { sheetId = id, rowIndex = offset, columnIndex = 0 }, rows = rows.Select(r => new { values = r.Select(v => new { userEnteredValue = new { stringValue = v } }).ToArray() }).ToArray(), fields = "userEnteredValue" } };
    private async Task<Sheet[]> Metadata(CancellationToken ct)
    {
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, SheetsUri("?fields=sheets.properties(sheetId,title,gridProperties)")), GoogleRequestSemantics.SafeRead, ct);
        using var d = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return d.RootElement.GetProperty("sheets").EnumerateArray().Select(s => { var p = s.GetProperty("properties"); return new Sheet(p.GetProperty("sheetId").GetInt32(), p.GetProperty("title").GetString()!, p.GetProperty("gridProperties").GetProperty("rowCount").GetInt32()); }).ToArray();
    }
    private Uri SheetsUri(string suffix) => new(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + suffix);
    private sealed record Sheet(int Id, string Title, int Rows);
}
