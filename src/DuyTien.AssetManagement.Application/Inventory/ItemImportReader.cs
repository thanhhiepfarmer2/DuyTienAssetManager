using System.Globalization;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace DuyTien.AssetManagement.Application.Inventory;

public static class ItemImportReader
{
    public const int MaxRows = 30000, MaxBytes = 32 * 1024 * 1024;
    public static readonly string[] Headers = ["Mã", "Tên", "Tính chất", "Nhóm VTHH", "Mô tả", "ĐVT chính", "Mã nhà SX", "Kho ngầm định", "Đơn vị chuyển đổi", "Tỷ lệ", "Phép tính", "Mô tả", "Giá bán 1", "Giá bán 2", "Giá bán 3", "Giá cố định", "Mã NVL", "Tên NVL", "ĐVT", "Số lượng"];
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static ItemDocument Read(byte[] bytes, string fileName)
    {
        if (bytes.Length is 0 or > MaxBytes) throw new InventoryValidationException("File phải có dữ liệu, tối đa32MiB.");
        try
        {
            var document = Path.GetExtension(fileName).ToLowerInvariant() switch
            { ".json" => JsonSerializer.Deserialize<ItemDocument>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 20 }) ?? throw new InventoryValidationException("JSON null."), ".xlsx" => Xlsx(bytes), _ => throw new InventoryValidationException("Chỉ XLSX hoặc JSON mapping20cột; không đọc XLS/formula.") }; return document with { SourceHash = Convert.ToHexString(SHA256.HashData(bytes)) };
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or XmlException or FormatException or OverflowException or ArgumentException or InvalidOperationException or IndexOutOfRangeException) { throw new InventoryValidationException("File JSON/XLSX không đúng cấu trúc hoặc không an toàn."); }
    }
    public static List<InventoryIssue> Validate(ItemDocument d)
    {
        var errors = new List<InventoryIssue>();
        if (d is null || d.Headers is null || d.Rows is null || d.Namespace != "MISA_VTHH" || !d.Headers.SequenceEqual(Headers, StringComparer.Ordinal)) { errors.Add(new(null, "Cần namespace MISA_VTHH và fingerprint header20cột đúng ordinal (Mô tả5/12).")); return errors; }
        if (d.SourceHash is null || d.SourceHash.Length > 0 && (d.SourceHash.Length != 64 || d.SourceHash.Any(ch => !Uri.IsHexDigit(ch)))) errors.Add(new(null, "SourceHash phải SHA256 hex hoặc rỗng khi chưa có file gốc; không là chứng nhận nguồn."));
        if (d.Rows.Length is 0 or > MaxRows) errors.Add(new(null, "Import phải có1–30000 dòng."));
        var seen = new HashSet<int>();
        foreach (var r in d.Rows)
        {
            if (r is null || r.SourceRow < 3 || !seen.Add(r.SourceRow) || r.Cells is null || r.Cells.Length != 20 || r.Cells.Any(v => v is null || v.Length > 2000 || v.IndexOf('\0') >= 0)) { errors.Add(new(r?.SourceRow, "Dòng/ô nguồn không hợp lệ, trùng row, vượt2000ký tự hoặc không đủ20cột.")); continue; }
            var c = r.Cells;
            if (string.IsNullOrWhiteSpace(c[0]) || c[0] != c[0].Trim() || string.IsNullOrWhiteSpace(c[1])) errors.Add(new(r.SourceRow, "Mã text và tên bắt buộc; không tự trim/đổi format mã."));
            if (c[8].Length > 0 || c[9].Length > 0 || c[10].Length > 0)
                if (c[8].Length == 0 || !decimal.TryParse(c[9], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var factor) || factor <= 0 || c[10] is not ("*" or "/")) errors.Add(new(r.SourceRow, "Conversion cần ĐVT, factor dương dạng invariant và toán tử * hoặc /; không tự quy đổi tồn."));
            if (c[19].Length > 0 && !decimal.TryParse(c[19], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _)) errors.Add(new(r.SourceRow, "Số lượng BOM tham chiếu không đúng số invariant; không phải tồn."));
        }
        return errors;
    }
    private static ItemDocument Xlsx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes); using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 192L * 1024 * 1024 || zip.Entries.Any(e => e.Length > 128L * 1024 * 1024) || zip.Entries.GroupBy(e => e.FullName, StringComparer.Ordinal).Any(g => g.Count() > 1)) throw new InventoryValidationException("Workbook ZIP vượt giới hạn/trùng entry.");
        XDocument Xml(string path) { using var stream = (zip.GetEntry(path) ?? throw new InventoryValidationException("Thiếu XML workbook.")).Open(); using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 128L * 1024 * 1024 }); return XDocument.Load(reader); }
        var shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(Main + "si").Select(x => string.Concat(x.Descendants(Main + "t").Select(t => t.Value))).ToArray();
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var sheet = Xml("xl/workbook.xml").Descendants(Main + "sheet").SingleOrDefault(s => (string?)s.Attribute("name") == "Vat_tu__hang_hoa__dich_vu") ?? throw new InventoryValidationException("Thiếu sheet Vat_tu__hang_hoa__dich_vu.");
        var link = Xml("xl/_rels/workbook.xml.rels").Root!.Elements().Single(r => (string?)r.Attribute("Id") == (string?)sheet.Attribute(rel + "id")); var target = (string?)link.Attribute("Target") ?? "";
        if ((string?)link.Attribute("TargetMode") == "External" || target.Contains("..", StringComparison.Ordinal) || target.Contains('\\')) throw new InventoryValidationException("Không đọc external/đường dẫn workbook lạ.");
        var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target; if (!path.StartsWith("xl/", StringComparison.Ordinal)) throw new InventoryValidationException("Đường dẫn sheet không hợp lệ.");
        var formats = zip.GetEntry("xl/styles.xml") is null ? [] : Xml("xl/styles.xml").Descendants(Main + "numFmt").ToDictionary(e => int.Parse((string)e.Attribute("numFmtId")!, CultureInfo.InvariantCulture), e => (string)e.Attribute("formatCode")!);
        var styles = zip.GetEntry("xl/styles.xml") is null ? [] : Xml("xl/styles.xml").Descendants(Main + "cellXfs").Elements(Main + "xf").Select(e => int.Parse((string?)e.Attribute("numFmtId") ?? "0", CultureInfo.InvariantCulture)).ToArray();
        string Value(XElement cell, int column)
        {
            if (cell.Element(Main + "f") is not null) throw new InventoryValidationException("Formula không được phép trong nguồn Items.");
            var raw = cell.Element(Main + "v")?.Value ?? ""; var type = (string?)cell.Attribute("t");
            if (type == "s") return shared[int.Parse(raw, CultureInfo.InvariantCulture)]; if (type == "inlineStr") return string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
            if (type is not (null or "n" or "str")) throw new InventoryValidationException("Kiểu ô không được mapping.");
            if (column is 0 or 6 or 7 or 16 && raw.Length > 0 && type != "str")
            {
                var style = int.Parse((string?)cell.Attribute("s") ?? "0", CultureInfo.InvariantCulture); var fmt = styles.Length == 0 ? 0 : styles[style];
                if (fmt is 0 or 49) return raw;
                if (formats.TryGetValue(fmt, out var code) && Regex.IsMatch(code, "^0{1,30}$", RegexOptions.CultureInvariant) && long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return number.ToString(new string('0', code.Length), CultureInfo.InvariantCulture);
                throw new InventoryValidationException("Format mã số chưa hỗ trợ; xuất ô code dạng text, không mất format/số0.");
            }
            return raw;
        }
        string[]? header = null; var rows = new List<ItemSourceRow>(); var seen = new HashSet<int>();
        foreach (var row in Xml(path).Descendants(Main + "sheetData").Elements(Main + "row"))
        {
            var n = int.Parse((string?)row.Attribute("r") ?? "0", CultureInfo.InvariantCulture); if (!seen.Add(n)) throw new InventoryValidationException("Trùng row workbook."); if (n < 2) continue; var cells = new string[20]; Array.Fill(cells, ""); var columns = new HashSet<int>();
            foreach (var cell in row.Elements(Main + "c")) { var reference = (string?)cell.Attribute("r") ?? ""; var letters = new string(reference.TakeWhile(char.IsLetter).ToArray()); var col = 0; foreach (var ch in letters) col = col * 26 + ch - 'A' + 1; col--; if (col < 0 || col >= 20 || !columns.Add(col) || reference[letters.Length..] != n.ToString(CultureInfo.InvariantCulture)) throw new InventoryValidationException("Cell reference/cột không đúng20cột."); cells[col] = Value(cell, col); }
            if (n == 2) header = cells; else if (cells.Any(v => v.Length > 0)) { rows.Add(new(n, cells)); if (rows.Count > MaxRows) throw new InventoryValidationException("Quá30000dòng."); }
        }
        if (header is null || !header.SequenceEqual(Headers, StringComparer.Ordinal)) throw new InventoryValidationException("Header XLSX không đúng fingerprint20cột, không đoán mapping."); return new("MISA_VTHH", header, rows.ToArray());
    }
}
