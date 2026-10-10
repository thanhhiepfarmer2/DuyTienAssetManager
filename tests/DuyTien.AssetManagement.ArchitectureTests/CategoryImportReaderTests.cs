using System.IO.Compression;
using System.Security;
using System.Text;
using DuyTien.AssetManagement.Application.Categories;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class CategoryImportReaderTests
{
    [Fact]
    public void Xlsx_reads_only_structured_nodes_and_joins_F_metadata_without_creating_notes_as_nodes()
    {
        var doc = CategoryImportReader.Read(Workbook(), "demo.xlsx"); Assert.Empty(CategoryCatalogService.Validate(doc.Rows)); Assert.Equal(3, doc.Rows.Count);
        Assert.Equal("F1 definition", doc.Rows[0].Definition); Assert.Equal("F2 definition", doc.Rows[1].Definition);
        Assert.Contains("Liệt kê F4 là ghi chú", doc.Rows[2].Definition, StringComparison.Ordinal); Assert.Equal(64, doc.SourceHash.Length);
    }
    [Theory]
    [InlineData("formula")]
    [InlineData("bad-header")]
    [InlineData("external")]
    [InlineData("metadata-stt")]
    [InlineData("dtd")]
    [InlineData("duplicate-entry")]
    public void Untrusted_workbooks_fail_closed(string fault) => Assert.Throws<CategoryValidationException>(() => CategoryImportReader.Read(Workbook(fault), "demo.xlsx"));
    [Theory]
    [InlineData("[42]")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("[{\"level\":\"bad\"}]")]
    public void Bad_json_produces_validation_result_or_exception_without_unhandled_parse_error(string json)
    {
        try { var doc = CategoryImportReader.Read(Encoding.UTF8.GetBytes(json), "source.json"); Assert.NotEmpty(CategoryCatalogService.Validate(doc.Rows)); }
        catch (CategoryValidationException) { }
    }
    [Fact]
    public void Oversized_or_wrong_extension_is_rejected()
    { Assert.Throws<CategoryValidationException>(() => CategoryImportReader.Read(new byte[CategoryImportReader.MaxFileBytes + 1], "source.json")); Assert.Throws<CategoryValidationException>(() => CategoryImportReader.Read([1, 2], "source.xls")); }
    internal static byte[] Workbook(string? fault = null)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string path, string content) { using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); w.Write(content); }
            var ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            string Cell(string col, int row, string value) => $"<c r=\"{col}{row}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(value)}</t></is></c>";
            string Row(int number, params string[] values) => $"<row r=\"{number}\">" + string.Concat(values.Select((v, i) => Cell(((char)('A' + i)).ToString(), number, v))) + "</row>";
            string Sheet(string rows) => $"<worksheet xmlns=\"{ns}\"><sheetData>{rows}</sheetData></worksheet>";
            Entry("xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
                string.Concat(new[] { "F1-F3 Mã ngang", "F1", "F2", "F3", "Mã nhân viên" }.Select((name, i) => $"<sheet name=\"{name}\" sheetId=\"{i + 1}\" r:id=\"r{i + 1}\"/>")) + "</sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                string.Concat(Enumerable.Range(1, 5).Select(i => $"<Relationship Id=\"r{i}\" Target=\"worksheets/sheet{i}.xml\"{(fault == "external" && i == 1 ? " TargetMode=\"External\"" : "")}/>")) + "</Relationships>");
            var rows = Row(1, "STT", fault == "bad-header" ? "wrong" : "Danh mục", "Mã") + Row(2, "1", "Gốc nháp", "ROT") + Row(3, "1.1", "Nhánh con", "CHD") + Row(4, "1.1.1", "Lá", "LEF");
            if (fault == "formula") rows = rows.Replace("</row>", "<c r=\"D1\"><f>HYPERLINK()</f></c></row>", StringComparison.Ordinal);
            var structure = Sheet(rows); if (fault == "dtd") structure = "<!DOCTYPE worksheet [<!ENTITY x SYSTEM 'file:///secret'>]>" + structure;
            Entry("xl/worksheets/sheet1.xml", structure);
            if (fault == "duplicate-entry") Entry("xl/worksheets/sheet1.xml", structure);
            for (var i = 1; i <= 3; i++)
            {
                var stt = i == 1 ? "1" : i == 2 ? "1.1" : "1.1.1"; if (fault == "metadata-stt" && i == 3) stt = "9.1.1";
                Entry($"xl/worksheets/sheet{i + 1}.xml", Sheet(Row(1, "STT", "Định nghĩa") + Row(2, "(F1)", "header lặp") + Row(3, stt, $"F{i} definition" + (i == 3 ? "; Liệt kê F4 là ghi chú" : ""))));
            }
            // Invalid personnel XML proves this sheet is not read by the category parser.
            Entry("xl/worksheets/sheet5.xml", "not XML; outside TASK006");
        }
        return output.ToArray();
    }
}
