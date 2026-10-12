using System.IO.Compression;
using System.Globalization;
using System.Xml.Linq;
namespace NexusFlow.IntegrationTests;
internal static class CustomerMigrationFixtures
{
    private static readonly Dictionary<string, (byte[], byte[])> Cached = new();
    public static (byte[] Outstanding, byte[] Cheques) Files(string reportDate = "30/09/2026", bool corruptSubtotal = false, bool formula = false)
    {
        var key = reportDate + corruptSubtotal + formula; if (Cached.TryGetValue(key, out var saved)) return saved;
        var ar = new Dictionary<(int Row, int Col), object>();
        string[] headers = ["debcode", "debnam", "ref", "tdate", "xtyp", "creamt", "debamt", "total", "today", "first30", "second30", "third30", "fourth30", "fifth30", "days"];
        for (int c = 0; c < headers.Length; c++) ar[(1, c + 1)] = headers[c];
        (string Code, string Ref, int Type, decimal Amount, decimal Debit, decimal Total)[] data = [("C001", "GT00011762", 1, 18751.5m, 128880, 18851.5m), ("C001", "DE1", 2, 400, 400, 0), ("C001", "RETURN1", 1, -300, 300, 0), ("C002", "RT1", 8, 200, 200, 200)];
        for (int r = 0; r < data.Length; r++)
        {
            var x = data[r]; int row = r + 2; object[] values = [x.Code, "Test " + x.Code, x.Ref, "01/09/2026", x.Type, x.Amount, x.Debit, x.Total, 0, x.Amount, 0, 0, 0, 0, 29];
            for (int c = 0; c < values.Length; c++) ar[(row, c + 1)] = values[c];
        }
        var cheque = new Dictionary<(int, int), object> { [(5, 1)] = "Cheques In Hand " + reportDate, [(8, 3)] = "Ref No", [(10, 1)] = "15/10/2026",
            [(12, 7)] = "Total for", [(12, 9)] = corruptSubtotal ? 749 : 750, [(14, 7)] = "Grand Total", [(14, 9)] = 750 };
        for (int row = 10; row <= 11; row++)
        {
            object[] values = ["20/09/2026", row == 10 ? "CH123" : "CH124", "GT00011762DE1", "C001", "Test C001", "7010-001", row == 10 ? "001946A" : "000012", row == 10 ? 500 : 250];
            for (int c = 0; c < values.Length; c++) cheque[(row, c + 2)] = values[c];
        }
        var result = (Workbook(ar, formula), Workbook(cheque)); Cached[key] = result; return result;
    }
    // Deterministic OpenXML fixtures, containing no real customer data.
    private static byte[] Workbook(Dictionary<(int Row, int Col), object> cells, bool formula = false)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static string Column(int col) { string value = ""; while (col > 0) { col--; value = (char)('A' + col % 26) + value; col /= 26; } return value; }
        var data = new XElement(ns + "sheetData");
        foreach (var group in cells.OrderBy(x => x.Key.Row).ThenBy(x => x.Key.Col).GroupBy(x => x.Key.Row))
        {
            var row = new XElement(ns + "row", new XAttribute("r", group.Key));
            foreach (var cell in group)
            {
                var element = new XElement(ns + "c", new XAttribute("r", Column(cell.Key.Col) + cell.Key.Row));
                if (formula && cell.Key == (2, 6)) element.Add(new XElement(ns + "f", "100+200"), new XElement(ns + "v", 300));
                else if (cell.Value is string text) element.Add(new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", text)));
                else element.Add(new XElement(ns + "v", Convert.ToString(cell.Value, CultureInfo.InvariantCulture)));
                row.Add(element);
            }
            data.Add(row);
        }
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string text) { var entry = zip.CreateEntry(name); entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); using var writer = new StreamWriter(entry.Open()); writer.Write(text); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Entry("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Entry("xl/worksheets/sheet1.xml", new XElement(ns + "worksheet", data).ToString());
        }
        return output.ToArray();
    }
}
