using System.Globalization;
using System.Text.Json;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

// Only the two known legacy DEV tables are allowed, after a full used-range read.
// No writes or PoC rerun; nonempty Sheet1 and unknown PoC rows remain fail-closed.
public static class GoogleLegacyDevTables
{
    public static readonly string[] Names = ["Sheet1", GoogleSheetsDataStore.PocSheetTitle];
    public static async Task ValidateAsync(GoogleWorkspaceOptions options, GoogleApiRequestExecutor api, IEnumerable<string> titles, CancellationToken ct)
    {
        var legacy = titles.Where(t => Names.Contains(t, StringComparer.Ordinal)).ToArray();
        if (legacy.Length == 0) return;
        var query = string.Join('&', legacy.Select(t => "ranges=" + Uri.EscapeDataString("'" + t + "'")));
        var uri = new Uri(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + "/values:batchGet?" + query + "&valueRenderOption=FORMULA");
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), GoogleRequestSemantics.SafeRead, ct);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!json.RootElement.TryGetProperty("valueRanges", out var ranges) || ranges.GetArrayLength() != legacy.Length) throw Invalid();
        var header = new[] { "RecordType", "Key", "Value1", "Value2", "Value3", "Value4", "State", "TimestampUtc", "Version" };
        for (var i = 0; i < legacy.Length; i++)
        {
            var range = ranges[i]; var returned = range.GetProperty("range").GetString() ?? "";
            if (!returned.StartsWith("'" + legacy[i] + "'!", StringComparison.Ordinal) && !returned.StartsWith(legacy[i] + "!", StringComparison.Ordinal)) throw Invalid();
            var rows = range.TryGetProperty("values", out var values) ? values.EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText()).ToArray()).ToArray() : [];
            if (legacy[i] == "Sheet1") { if (rows.Any(r => r.Any(v => v.Length > 0))) throw Invalid(); continue; }
            if (rows.Length is < 2 or > 5000 || !rows[0].SequenceEqual(header)) throw Invalid();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows.Skip(1))
            {
                if (r.Length != 9 || string.IsNullOrWhiteSpace(r[1]) || !keys.Add(r[0] + ":" + r[1])) throw Invalid();
                if (r[0] is not ("COUNTER" or "ENTITY" or "OPERATION" or "AUDIT" or "FILE")) throw Invalid();
                if (r[0] == "COUNTER" && (r[6] != "READY" || !long.TryParse(r[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 0)) throw Invalid();
                if (r[0] != "AUDIT" && (!int.TryParse(r[8], out var version) || version < 0)) throw Invalid();
                if (r[0] == "ENTITY" && r[6] != "ACTIVE" || r[0] is "OPERATION" or "AUDIT" && r[6] != "COMMITTED" || r[0] == "FILE" && r[6] is not ("PENDING" or "FAILED" or "AVAILABLE")) throw Invalid();
            }
            if (rows.Count(r => r[0] == "COUNTER" && r[1] == "TASK004") != 1) throw Invalid();
        }
    }
    private static ExternalStorageException Invalid() => new("Legacy DEV table không đúng hợp đồng hoặc Sheet1 có dữ liệu; không ghi cây/schema.");
}
