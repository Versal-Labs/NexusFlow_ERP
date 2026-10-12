using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ExcelDataReader;
using NexusFlow.AppCore.Features.Migration;

namespace NexusFlow.Infrastructure.Services;

public sealed class MigrationFileReader : IMigrationFileReader
{
    public MigrationReadResult Read(string module, string filename, byte[] bytes)
    {
        try { return ReadCore(module, filename, bytes); }
        catch (Exception ex) when (ex is ExcelDataReader.Exceptions.HeaderException or System.Xml.XmlException or ArgumentException or IndexOutOfRangeException)
        { throw new InvalidDataException("The workbook/UTF-8 file is malformed or unsupported. Export a valid values-only workbook or canonical CSV.", ex); }
    }
    private MigrationReadResult ReadCore(string module, string filename, byte[] bytes)
    {
        if (!MigrationCatalog.Modules.ContainsKey(module)) throw new InvalidDataException("Unknown dataset.");
        if (bytes.Length == 0 || bytes.Length > 20 * 1024 * 1024) throw new InvalidDataException("Files must be between 1 byte and 20 MB.");
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        if (ext is not ".csv" and not ".xls" and not ".xlsx") throw new InvalidDataException("Upload CSV, XLS or values-only XLSX.");
        var table = ext == ".csv" ? Csv(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF')) : Excel(bytes);
        if (table.Count < 2) throw new InvalidDataException("The file contains no detail rows.");
        if (ext != ".csv" && table[0].Any(x => x.Equals("debcode", StringComparison.OrdinalIgnoreCase)) && module == "CustomerOpenings")
        {
            var s = new CustomerMigrationFileReader().ReadOutstandingOnly(bytes);
            var rows = s.Outstanding.Select(x => New(x.Row, ("Code", x.Code), ("Reference", x.Reference), ("Kind", x.Amount < 0 ? "ReturnCredit" : "Invoice"), ("DocumentDate", Day(x.Date)), ("DueDate", Day(x.Date.AddDays(30))), ("Amount", Num(x.Amount)))).ToList();
            return new(rows, s.Errors, s.OutstandingReportDate);
        }
        if (module == "ReceivedCheques" && !table[0].Contains("LegacyRecordId", StringComparer.OrdinalIgnoreCase) && table.Take(12).Any(r => r.Any(v => v.Contains("Cheques In Hand", StringComparison.OrdinalIgnoreCase))))
        {
            var s = new CustomerMigrationFileReader().ReadChequesOnly(bytes);
            return new(s.Cheques.Select(x => New(x.Row, ("Code", x.Code), ("Reference", x.Reference), ("DocumentReferences", x.DocumentReferences), ("ChequeNumber", x.ChequeNumber), ("ChequeDate", Day(x.ChequeDate)), ("ReceivedDate", Day(x.ReceivedDate)), ("BankBranchCode", x.BankCode), ("Amount", Num(x.Amount)), ("Status", "InSafe"), ("PartyReflected", "true"), ("BankReflected", "false"))).ToList(), s.Errors, s.ChequeReportDate);
        }
        var headers = table[0].Select(x => x.Trim()).ToArray();
        if (headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length) throw new InvalidDataException("Every column needs a unique nonblank header.");
        var output = new List<MigrationRow>();
        for (var i = 1; i < table.Count; i++)
        {
            if (table[i].All(string.IsNullOrWhiteSpace)) continue;
            if (table[i].Count > headers.Length) throw new InvalidDataException($"Row {i + 1} has extra columns.");
            var row = new MigrationRow { Row = i + 1 };
            for (var col = 0; col < headers.Length; col++) row.Raw[headers[col]] = col < table[i].Count ? table[i][col].Trim() : "";
            foreach (var field in MigrationCatalog.Modules[module]) if (row.Raw.TryGetValue(field, out var value)) row.Set(field, value);
            if (row.Raw.TryGetValue("ClosingDate", out var closing)) row.Set("ClosingDate", closing);
            if (module == "CustomerMaster" && row.Raw.ContainsKey("debcode")) Map(row, ("Code", "debcode"), ("Name", "debname"), ("Contact", "debcper"), ("Email", "debmail"), ("Phone", "debtel1"), ("Mobile", "debtel2"), ("Address1", "debadd1"), ("Address2", "debadd2"));
            if (module == "CustomerOpenings" && row.Raw.ContainsKey("debcode")) { Map(row, ("Code", "debcode"), ("Reference", "ref"), ("DocumentDate", "tdate"), ("Amount", "creamt")); row.Set("Kind", row.Decimal("Amount") < 0 ? "ReturnCredit" : "Invoice"); }
            if (module == "SupplierMaster" && row.Raw.ContainsKey("supcode")) Map(row, ("Code", "supcode"), ("Name", "supname"), ("Contact", "supcontact"), ("Email", "supemail"), ("Phone", "suptele"), ("Address1", "supadd1"), ("Address2", "supadd2"), ("GroupCode", "supgroup"), ("PayableAccountCode", "supacc"), ("TaxNumber", "tinno"));
            if (module == "Stock" && row.Raw.ContainsKey("SKU"))
            {
                Map(row, ("Code", "SKU"), ("Name", "ProductName"), ("BrandCode", "Brand"), ("CategoryCode", "Category"), ("UnitCode", "Unit")); row.Set("ProductType", "RawMaterial");
                if (row.Raw.TryGetValue("OpeningDate", out var d)) row.Set("SourceSnapshotDate", NormalizeDate(d));
            }
            if (module == "Stock" && row.Raw.ContainsKey("itemcode"))
            {
                Map(row, ("Code", "itemcode"), ("Name", "itemname"), ("BrandCode", "brand"), ("CategoryCode", "type"), ("CandidateUnitCost", "avar"), ("SourceValue", "costval"), ("SellingPrice", "selprice"), ("Size", "size"), ("Color", "color"));
                row.Set("ProductType", "FinishedGood");
                decimal total = 0;
                for (var loc = 1; loc <= 8; loc++)
                {
                    var qty = decimal.Parse(row.Raw["loc" + loc], CultureInfo.InvariantCulture); total += qty;
                    if (qty == 0) continue;
                    var part = MigrationJson.Read<MigrationRow>(MigrationJson.Write(row));
                    part.Set("WarehouseCode", "loc" + loc); part.Set("Quantity", Num(qty)); output.Add(part);
                }
                if (total != decimal.Parse(row.Raw["totqty"], CultureInfo.InvariantCulture)) throw new InvalidDataException($"Row {row.Row}: warehouse quantities differ from totqty.");
                if (total == 0) { row.Set("Quantity", "0"); output.Add(row); }
                continue;
            }
            output.Add(row);
        }
        foreach (var r in output) foreach (var field in new[] { "DocumentDate", "DueDate", "ChequeDate", "ReceivedDate", "LayerDate", "ClosingDate" }) if (r.Get(field) != "") r.Set(field, NormalizeDate(r.Get(field)));
        if (output.Count > 20000) throw new InvalidDataException("Maximum 20,000 normalized rows per dataset.");
        return new(output, []);
    }
    private static void Map(MigrationRow r, params (string Target, string Source)[] map) { foreach (var (target, source) in map) r.Set(target, r.Raw.TryGetValue(source, out var value) ? value : ""); }
    private static MigrationRow New(int row, params (string Key, string Value)[] fields) { var r = new MigrationRow { Row = row }; foreach (var (k, v) in fields) r.Set(k, v); return r; }
    private static string Day(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Num(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string NormalizeDate(string s) => DateTime.TryParseExact(s, ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? Day(d) : throw new InvalidDataException($"Invalid date '{s}'. Use yyyy-MM-dd.");
    private static List<List<string>> Excel(byte[] bytes)
    {
        if (bytes[0] == 'P' && bytes[1] == 'K')
        {
            using var zip = new ZipArchive(new MemoryStream(bytes)); long length = 0;
            foreach (var e in zip.Entries)
            {
                length += e.Length; if (length > 100 * 1024 * 1024) throw new InvalidDataException("Expanded workbook exceeds 100 MB.");
                if (e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Macro workbooks are not supported.");
                if (!e.FullName.StartsWith("xl/worksheets/") || !e.FullName.EndsWith(".xml")) continue;
                using var stream = e.Open(); if (XDocument.Load(stream).Descendants().Any(x => x.Name.LocalName == "f")) throw new InvalidDataException("Export formula cells as values before uploading.");
            }
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var file = new MemoryStream(bytes); using var reader = ExcelReaderFactory.CreateReader(file);
        if (reader.FieldCount > 100 || reader.RowCount > 20001) throw new InvalidDataException("Maximum 20,000 rows and 100 columns.");
        var table = new List<List<string>>();
        while (reader.Read())
        {
            var values = new List<string>();
            for (var col = 0; col < reader.FieldCount; col++)
            {
                var value = reader.GetValue(col); var format = reader.GetNumberFormatString(col);
                values.Add(value is DateTime date ? Day(date) : value is double n && format != null && System.Text.RegularExpressions.Regex.IsMatch(format, "^0+$") ? n.ToString(format, CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
            }
            table.Add(values);
        }
        while (table.Count > 0 && table[0].All(string.IsNullOrWhiteSpace)) table.RemoveAt(0);
        return table;
    }
    internal static List<List<string>> Csv(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else { if (!quoted && field.Length != 0) throw new InvalidDataException("Invalid CSV quoting."); quoted = !quoted; } }
            else if (!quoted && (c == ',' || c == '\n' || c == '\r'))
            {
                row.Add(field.ToString()); field.Clear();
                if (c != ',') { rows.Add(row); row = []; if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
            }
            else field.Append(c);
            if (rows.Count > 20001 || row.Count > 100 || field.Length > 8000) throw new InvalidDataException("CSV limit exceeded.");
        }
        if (quoted) throw new InvalidDataException("CSV has an unclosed quoted field.");
        if (field.Length != 0 || row.Count != 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
