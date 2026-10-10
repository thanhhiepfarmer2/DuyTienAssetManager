using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace DuyTien.AssetManagement.Application.Categories;

public static partial class CategoryImportReader
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxNodes = 1000;
    public const string MappingVersion = "CATEGORY_F1_F3_V1";
    private const string StructureSheet = "F1-F3 Mã ngang";
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static CategoryDocument Read(byte[] bytes, string fileName)
    {
        if (bytes.Length == 0 || bytes.Length > MaxFileBytes)
            throw new CategoryValidationException("File phải có dữ liệu và không vượt 10 MB.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        try
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".json" => ReadJson(bytes, hash), ".xlsx" => ReadXlsx(bytes, hash),
                _ => throw new CategoryValidationException("Chỉ hỗ trợ JSON staging và workbook XLSX.")
            };
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or XmlException or FormatException or OverflowException or ArgumentException or IndexOutOfRangeException or InvalidOperationException)
        { throw new CategoryValidationException("File không đúng cấu trúc JSON/XLSX hoặc có dữ liệu không an toàn."); }
    }
    private static CategoryDocument ReadJson(byte[] bytes, string hash)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 20 });
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new CategoryValidationException("JSON staging phải là mảng các nút.");
        if (json.RootElement.GetArrayLength() > MaxNodes)
            throw new CategoryValidationException("Snapshot demo không vượt 1000 nút.");
        var rows = json.RootElement.EnumerateArray().Select(r => new CategoryInput(
            Text(r, "source_sheet"), Number(r, "source_row"), Text(r, "source_stt"),
            NullableText(r, "source_parent_stt"), Number(r, "level"), Text(r, "name").Trim(),
            Text(r, "segment_code").Trim(), Text(r, "definition"), NullableText(r, "category_id"))).ToArray();
        return new(hash, MappingVersion, rows, rows.All(r => r.Definition.Length == 0)
            ? ["JSON staging không có definition F1/F2/F3; chưa đối chiếu metadata workbook gốc."] : []);
    }
    private static CategoryDocument ReadXlsx(byte[] bytes, string hash)
    {
        using var stream = new MemoryStream(bytes); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 64 * 1024 * 1024 ||
            zip.Entries.Any(e => e.Length > 16 * 1024 * 1024) ||
            zip.Entries.GroupBy(e => e.FullName, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new CategoryValidationException("Workbook quá lớn hoặc ZIP có entry trùng.");
        var workbook = Xml(zip, "xl/workbook.xml");
        var rels = Xml(zip, "xl/_rels/workbook.xml.rels").Root!.Elements().ToDictionary(
            r => (string)r.Attribute("Id")!, r => r, StringComparer.Ordinal);
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] :
            Xml(zip, "xl/sharedStrings.xml").Descendants(Main + "si")
                .Select(x => string.Concat(x.Descendants(Main + "t").Select(t => t.Value))).ToArray();
        var sheets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in workbook.Descendants(Main + "sheet"))
        {
            var name = (string)s.Attribute("name")!;
            if (name != StructureSheet && name is not ("F1" or "F2" or "F3")) continue;
            if (!rels.TryGetValue((string)s.Attribute(relNs + "id")!, out var rel) ||
                (string?)rel.Attribute("TargetMode") == "External")
                throw new CategoryValidationException("Không đọc sheet tham chiếu ngoài workbook.");
            var target = (string)rel.Attribute("Target")!;
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            if (!path.StartsWith("xl/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
                throw new CategoryValidationException("Đường dẫn sheet không hợp lệ.");
            if (!sheets.TryAdd(name, path)) throw new CategoryValidationException("Workbook trùng tên sheet.");
        }
        if (!sheets.TryGetValue(StructureSheet, out var structure))
            throw new CategoryValidationException("Thiếu sheet F1-F3 Mã ngang.");
        var data = Sheet(zip, structure, shared);
        if (data.Count == 0 || !data[0].Cells.GetValueOrDefault("A", "").Contains("STT", StringComparison.OrdinalIgnoreCase)
            || !data[0].Cells.GetValueOrDefault("C", "").Contains("Mã", StringComparison.OrdinalIgnoreCase)
            || !data[0].Cells.GetValueOrDefault("B", "").Contains("Danh mục", StringComparison.OrdinalIgnoreCase))
            throw new CategoryValidationException("Header phải có A=STT, B=Danh mục, C=Mã.");
        var warnings = new List<string>(); var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var title in new[] { "F1", "F2", "F3" })
        {
            if (!sheets.TryGetValue(title, out var path)) { warnings.Add($"Thiếu metadata sheet {title}."); continue; }
            var metadata = Sheet(zip, path, shared);
            var header = metadata.FirstOrDefault(r => r.Cells.Values.Any(v => v.Trim().Equals("STT", StringComparison.OrdinalIgnoreCase)));
            if (header is null) throw new CategoryValidationException($"{title}: không tìm thấy header STT.");
            var sttColumn = header.Cells.First(c => c.Value.Trim().Equals("STT", StringComparison.OrdinalIgnoreCase)).Key;
            var definitionColumns = header.Cells.Where(c => c.Value.Contains("định nghĩa", StringComparison.OrdinalIgnoreCase)
                || c.Value.Contains("mô tả", StringComparison.OrdinalIgnoreCase) || c.Value.Contains("ghi chú", StringComparison.OrdinalIgnoreCase)
                || c.Value.Equals("Definition", StringComparison.OrdinalIgnoreCase)).Select(c => c.Key).ToArray();
            if (definitionColumns.Length == 0) throw new CategoryValidationException($"{title}: header metadata chưa được mapping; cần cột Định nghĩa/Mô tả/Ghi chú.");
            foreach (var row in metadata.Where(r => r.Number > header.Number))
            {
                var stt = row.Cells.GetValueOrDefault(sttColumn, "").Trim();
                if (stt.Length == 0 || stt == "STT" || row.Cells.Values.Any(v => v.Trim() is "(F1)" or "(F2)" or "(F3)")) continue;
                if (!SttPattern().IsMatch(stt) || stt.Split('.').Length != int.Parse(title[1..]))
                    throw new CategoryValidationException($"{title} dòng {row.Number}: STT/cấp metadata không hợp lệ.");
                var definition = string.Join("\n", definitionColumns.Select(c => row.Cells.GetValueOrDefault(c, "")).Where(v => v.Length > 0));
                if (!definitions.TryAdd(stt, definition)) throw new CategoryValidationException($"{title} dòng {row.Number}: STT metadata bị trùng.");
            }
        }
        var rows = data.Skip(1).Where(r => r.Cells.GetValueOrDefault("A", "").Length > 0
            || r.Cells.GetValueOrDefault("B", "").Length > 0 || r.Cells.GetValueOrDefault("C", "").Length > 0).Select(r =>
        {
            var stt = r.Cells.GetValueOrDefault("A", "").Trim(); var index = stt.LastIndexOf('.');
            return new CategoryInput(StructureSheet, r.Number, stt, index < 0 ? null : stt[..index], stt.Split('.').Length,
                r.Cells.GetValueOrDefault("B", "").Trim(), r.Cells.GetValueOrDefault("C", "").Trim(), definitions.GetValueOrDefault(stt, ""));
        }).ToArray();
        if (definitions.Keys.Except(rows.Select(r => r.SourceStt), StringComparer.Ordinal).Any())
            throw new CategoryValidationException("Metadata có STT không tồn tại trong sheet cấu trúc.");
        return new(hash, MappingVersion, rows, warnings);
    }
    private sealed record SheetRow(int Number, Dictionary<string, string> Cells);
    private static List<SheetRow> Sheet(ZipArchive zip, string path, string[] shared)
    {
        var rows = new List<SheetRow>();
        foreach (var row in Xml(zip, path).Descendants(Main + "sheetData").Elements(Main + "row"))
        {
            if (rows.Count >= 5000) throw new CategoryValidationException("Sheet vượt giới hạn 5000 dòng.");
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var cell in row.Elements(Main + "c"))
            {
                if (cell.Element(Main + "f") is not null) throw new CategoryValidationException("Không chấp nhận formula trong sheet danh mục.");
                var reference = (string?)cell.Attribute("r") ?? ""; var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                var raw = cell.Element(Main + "v")?.Value ?? "";
                var value = (string?)cell.Attribute("t") switch { "s" => shared[int.Parse(raw)], "inlineStr" => string.Concat(cell.Descendants(Main + "t").Select(t => t.Value)), _ => raw };
                if (!cells.TryAdd(column, value)) throw new CategoryValidationException("Trùng tham chiếu ô Excel.");
            }
            rows.Add(new(int.Parse((string?)row.Attribute("r") ?? "0"), cells));
        }
        return rows;
    }
    private static XDocument Xml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new CategoryValidationException("Workbook thiếu XML bắt buộc.");
        using var stream = entry.Open();
        using var xml = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        return XDocument.Load(xml);
    }
    private static string Text(JsonElement r, string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    private static string? NullableText(JsonElement r, string name) => string.IsNullOrWhiteSpace(Text(r, name)) ? null : Text(r, name);
    private static int Number(JsonElement r, string name) => r.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;
    [GeneratedRegex(@"^[1-9]\d*(\.[1-9]\d*){0,2}$", RegexOptions.CultureInvariant)] internal static partial Regex SttPattern();
}
