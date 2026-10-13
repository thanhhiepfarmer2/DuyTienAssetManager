using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DuyTien.AssetManagement.Application.Inventory;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class InventoryReaderTests
{
    [Fact]
    public async Task Json_header_ordinal_and_text_codes_description_separation()
    { var f = await InventoryFixture.Start(); var d = f.Request().Document; var parsed = ItemImportReader.Read(JsonSerializer.SerializeToUtf8Bytes(d, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "fixture.json"); Assert.Empty(ItemImportReader.Validate(parsed)); Assert.Equal("00001", parsed.Rows[0].Cells[0]); Assert.NotEqual(parsed.Rows[0].Cells[4], parsed.Rows[0].Cells[11]); }
    [Theory]
    [InlineData("header")]
    [InlineData("duplicate-row")]
    [InlineData("conversion")]
    [InlineData("negative-factor")]
    [InlineData("operator")]
    [InlineData("code")]
    [InlineData("null-cell")]
    [InlineData("bom")]
    [InlineData("namespace")]
    public async Task Mapping_errors_are_reported_before_job(string fault)
    { var f = await InventoryFixture.Start(); var d = f.Request(2).Document; if (fault == "header") d.Headers[11] = "Mô tả khác"; else if (fault == "duplicate-row") d = d with { Rows = [d.Rows[0], d.Rows[0]] }; else if (fault == "namespace") d = d with { Namespace = "NOT_MISA" }; else if (fault == "code") d.Rows[0].Cells[0] = " 001"; else if (fault == "null-cell") d.Rows[0].Cells[8] = null!; else if (fault == "bom") d.Rows[0].Cells[19] = "not-number"; else { d.Rows[0].Cells[8] = "kg"; d.Rows[0].Cells[9] = fault == "negative-factor" ? "-1" : "2"; d.Rows[0].Cells[10] = fault == "operator" ? "+" : fault == "conversion" ? "" : "*"; } Assert.NotEmpty(ItemImportReader.Validate(d)); Assert.Equal(0, f.Store.Writes); }
    [Theory]
    [InlineData("000001", false, "000001")]
    [InlineData("7", true, "000007")]
    public void Xlsx_preserves_shared_inline_and_custom_zero_mask(string code, bool numeric, string expected)
    { var d = ItemImportReader.Read(Workbook(code, numeric), "GIẢ.xlsx"); Assert.Empty(ItemImportReader.Validate(d)); Assert.Equal(expected, d.Rows[0].Cells[0]); Assert.Equal("Mô tả1 GIẢ", d.Rows[0].Cells[4]); Assert.Equal("Mô tả2 GIẢ", d.Rows[0].Cells[11]); }
    [Theory]
    [InlineData("formula")]
    [InlineData("format")]
    [InlineData("external")]
    [InlineData("header")]
    [InlineData("cell")]
    public void Xlsx_unsafe_or_unmapped_input_fails_without_guessing(string fault) => Assert.Throws<InventoryValidationException>(() => ItemImportReader.Read(Workbook("7", true, fault), "fixture.xlsx"));
    [Theory]
    [InlineData("bad.txt")]
    [InlineData("bad.xls")]
    [InlineData("bad.xlsx")]
    [InlineData("bad.json")]
    public void Empty_bad_or_unsupported_file_is_validation(string name) => Assert.Throws<InventoryValidationException>(() => ItemImportReader.Read(Encoding.UTF8.GetBytes("invalid"), name));
    private static byte[] Workbook(string code, bool numeric, string fault = "")
    {
        XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"; XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XElement Text(string address, string value) => new(n + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"), new XElement(n + "is", new XElement(n + "t", value)));
        var header = new XElement(n + "row", new XAttribute("r", 2), ItemImportReader.Headers.Select((h, i) => Text(((char)('A' + i)).ToString() + "2", fault == "header" && i == 11 ? "OTHER" : h)));
        var row = new XElement(n + "row", new XAttribute("r", 3)); row.Add(numeric ? new XElement(n + "c", new XAttribute("r", "A3"), new XAttribute("s", 1), new XElement(n + "v", code)) : Text("A3", code)); row.Add(Text("B3", "Item GIẢ"), Text("E3", "Mô tả1 GIẢ"), Text("F3", "cái"), Text("L3", "Mô tả2 GIẢ")); if (fault == "formula") row.Element(n + "c")!.Add(new XElement(n + "f", "1+1")); if (fault == "cell") row.Add(Text("A3", "duplicate"));
        using var ms = new MemoryStream(); using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string path, XElement value) { using var w = new StreamWriter(z.CreateEntry(path).Open(), new UTF8Encoding(false)); w.Write(value.ToString(SaveOptions.DisableFormatting)); }
            Add("xl/workbook.xml", new(n + "workbook", new XElement(n + "sheets", new XElement(n + "sheet", new XAttribute("name", "Vat_tu__hang_hoa__dich_vu"), new XAttribute(rel + "id", "r1")))));
            Add("xl/_rels/workbook.xml.rels", new("Relationships", new XElement("Relationship", new XAttribute("Id", "r1"), new XAttribute("Target", "worksheets/items.xml"), new XAttribute("TargetMode", fault == "external" ? "External" : "Internal"))));
            Add("xl/worksheets/items.xml", new(n + "worksheet", new XElement(n + "sheetData", header, row)));
            Add("xl/styles.xml", new(n + "styleSheet", new XElement(n + "numFmts", new XElement(n + "numFmt", new XAttribute("numFmtId", 164), new XAttribute("formatCode", fault == "format" ? "0.00" : "000000"))), new XElement(n + "cellXfs", new XElement(n + "xf", new XAttribute("numFmtId", 0)), new XElement(n + "xf", new XAttribute("numFmtId", 164)))));
        }
        return ms.ToArray();
    }
}
