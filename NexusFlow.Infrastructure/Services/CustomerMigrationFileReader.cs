using System.Globalization;
using System.Text.RegularExpressions;
using NexusFlow.AppCore.Features.Finance.CustomerMigration;
using System.Text;
using System.IO.Compression;
using System.Xml.Linq;
using ExcelDataReader;

namespace NexusFlow.Infrastructure.Services;

/// <summary>Reads report values only. It never evaluates formulas or follows workbook links.</summary>
public sealed class CustomerMigrationFileReader : ICustomerMigrationFileReader
{
    public CustomerMigrationSource ReadOutstandingOnly(byte[] bytes)
    {
        var source = new CustomerMigrationSource(); ReadOutstanding(Load(bytes), source); return source;
    }
    public CustomerMigrationSource ReadChequesOnly(byte[] bytes)
    {
        var source = new CustomerMigrationSource(); ReadCheques(Load(bytes), source); return source;
    }
    public CustomerMigrationSource Read(byte[] outstanding, byte[] cheques)
    {
        var result = new CustomerMigrationSource();
        try
        {
            ReadOutstanding(Load(outstanding), result);
            ReadCheques(Load(cheques), result);
        }
        catch (InvalidDataException ex)
        {
            result.Errors.Add(ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result.Errors.Add("The files could not be read as the supported legacy reports. Verify their format and headers.");
            System.Diagnostics.Trace.WriteLine(ex);

        }
        return result;
    }

    private sealed record ReportSheet(List<object?[]> Rows, List<string?[]> Formats)
    {
        public int LastRow => Rows.Count;
        public int LastColumn => Rows.Count == 0 ? 0 : Rows[0].Length;
        public object? Value(int row, int col) => row <= Rows.Count && col <= Rows[row - 1].Length ? Rows[row - 1][col - 1] : null;
        public string? Format(int row, int col) => row <= Formats.Count && col <= Formats[row - 1].Length ? Formats[row - 1][col - 1] : null;
    }
    private static ReportSheet Load(byte[] bytes)
    {
        // Formula evaluation and external-link fetching are never performed. XLSX formulas are rejected explicitly.
        if (bytes.Length > 2 && bytes[0] == 'P' && bytes[1] == 'K')
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries.Where(x => x.FullName.StartsWith("xl/worksheets/") && x.FullName.EndsWith(".xml")))
            {
                if (entry.Length > 30 * 1024 * 1024) throw new InvalidDataException("Worksheet is too large.");
                using var data = entry.Open(); var xml = XDocument.Load(data);
                if (xml.Descendants().Any(x => x.Name.LocalName == "f")) throw new InvalidDataException("Formula cells are not supported in migration XLSX input. Export values only.");
            }
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new MemoryStream(bytes); using var reader = ExcelReaderFactory.CreateReader(stream);
        if (reader.FieldCount > 100 || reader.RowCount > 20001) throw new InvalidDataException("Maximum 20,000 detail rows and 100 columns.");
        var rows = new List<object?[]>(); var formats = new List<string?[]>();
        while (reader.Read())
        {
            if (rows.Count >= 20001) throw new InvalidDataException("Maximum 20,000 detail rows.");
            var row = new object?[reader.FieldCount]; var fmt = new string?[reader.FieldCount];
            for (int col = 0; col < row.Length; col++) { row[col] = reader.GetValue(col); fmt[col] = reader.GetNumberFormatString(col); }
            rows.Add(row); formats.Add(fmt);
        }
        return new(rows, formats);
    }
    private static string Text(ReportSheet sheet, int row, int col)
    {
        var value = sheet.Value(row, col);
        if (value is null or DBNull) return "";
        if (value is DateTime date) return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var format = sheet.Format(row, col);
        if (value is double number && format != null && Regex.IsMatch(format, "^0+$") && number == Math.Truncate(number))
            return number.ToString(new string('0', format.Length), CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
    }
    private static decimal Money(ReportSheet sheet, int row, int col)
    {
        var raw = sheet.Value(row, col);
        decimal value;
        if (raw is double or int or decimal) value = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
        else if (!decimal.TryParse(Text(sheet, row, col), NumberStyles.Number | NumberStyles.AllowParentheses, CultureInfo.InvariantCulture, out value))
            throw new InvalidDataException($"Invalid amount at row {row}, column {col}.");
        if (Math.Round(value, 2) != value || Math.Abs(value) >= 10000000000000000m) throw new InvalidDataException($"Amount at row {row} must fit two decimal places.");
        return value;
    }
    private static DateTime Date(ReportSheet sheet, int row, int col)
    {
        var raw = sheet.Value(row, col);
        if (raw is DateTime date) return date.Date;
        if (raw is double number) return DateTime.FromOADate(number).Date;
        if (DateTime.TryParseExact(Text(sheet, row, col), ["dd/MM/yyyy", "yyyy-MM-dd", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return date.Date;
        throw new InvalidDataException($"Invalid date at row {row}.");
    }
    private static void ReadOutstanding(ReportSheet sheet, CustomerMigrationSource result)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= sheet.LastColumn; col++)
            if (Text(sheet, 1, col) != "" && !columns.TryAdd(Text(sheet, 1, col), col)) throw new InvalidDataException("Duplicate outstanding header.");
        foreach (var name in new[] { "debcode", "debnam", "ref", "tdate", "xtyp", "creamt", "debamt", "total", "days", "today", "first30", "second30", "third30", "fourth30", "fifth30" })
            if (!columns.ContainsKey(name)) throw new InvalidDataException($"Missing outstanding header {name}.");
        if (sheet.LastRow > 20001) throw new InvalidDataException("Maximum 20,000 report rows.");
        var totals = new Dictionary<string, decimal>();
        var reportDates = new HashSet<DateTime>();
        for (int row = 2; row <= sheet.LastRow; row++)
        {
            var code = Text(sheet, row, columns["debcode"]).ToUpperInvariant();
            if (code == "" && Text(sheet, row, columns["ref"]) == "") continue;
            var amount = Money(sheet, row, columns["creamt"]);
            var type = Text(sheet, row, columns["xtyp"]);
            var kind = amount < 0 ? "Goods-return credit" : type switch
            {
                "1" => "Legacy invoice", "2" => "Legacy debit adjustment", "8" => "Legacy returned-cheque debt", _ => "Unknown"
            };
            if (kind == "Unknown") result.Errors.Add($"Outstanding row {row}: unsupported type '{type}'.");
            var notes = string.Join("; ", new[] { "rep", "rem", "remark", "chqno", "bankcode" }
                .Where(columns.ContainsKey).Select(k => $"{k}: {Text(sheet, row, columns[k])}"));
            var item = new OutstandingSourceRow(row, code, Text(sheet, row, columns["debnam"]),
                Text(sheet, row, columns["ref"]), Date(sheet, row, columns["tdate"]), kind, amount,
                Money(sheet, row, columns["debamt"]), notes);
            result.Outstanding.Add(item);
            if (type is "1" or "2")
            {
                var days = Money(sheet, row, columns["days"]);
                if (days < 0 || days > 100000 || Math.Truncate(days) != days) result.Errors.Add($"Outstanding row {row}: invalid report ageing days.");
                else reportDates.Add(item.Date.AddDays((int)days));
            }
            totals[code] = totals.GetValueOrDefault(code) + Money(sheet, row, columns["total"]);
            var ageNames = new[] { "today", "first30", "second30", "third30", "fourth30", "fifth30" };
            if (ageNames.All(columns.ContainsKey) && ageNames.Sum(k => Money(sheet, row, columns[k])) != amount)
                result.Errors.Add($"Outstanding row {row}: ageing does not equal the signed outstanding amount.");
        }
        if (reportDates.Count == 1) result.OutstandingReportDate = reportDates.Single();
        else result.Errors.Add("Outstanding report ageing days must identify one consistent report closing date.");
        foreach (var g in result.Outstanding.GroupBy(x => x.Code))
            if (g.Sum(x => x.Amount) != totals.GetValueOrDefault(g.Key)) result.Errors.Add($"Customer {g.Key}: report total does not reconcile.");
    }
    private static void ReadCheques(ReportSheet sheet, CustomerMigrationSource result)
    {
        if (sheet.LastRow > 20000) throw new InvalidDataException("Maximum 20,000 report rows.");
        bool headerFound = false, grandFound = false;
        DateTime? groupDate = null;
        decimal groupTotal = 0, reportTotal = 0;
        for (int row = 1; row <= sheet.LastRow; row++)
        {
            var title = Text(sheet, row, 1);
            var dateMatch = Regex.Match(title, @"^Cheques In Hand\s+(\d{2}/\d{2}/\d{4})$", RegexOptions.IgnoreCase);
            if (dateMatch.Success) result.ChequeReportDate = DateTime.ParseExact(dateMatch.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            var reference = Text(sheet, row, 3);
            var groupLabel = Text(sheet, row, 7);
            if (reference == "Ref No") { headerFound = true; continue; }
            if (groupLabel == "Total for")
            {
                if (groupTotal != Money(sheet, row, 9)) result.Errors.Add($"Cheque row {row}: group subtotal mismatch.");
                groupTotal = 0; groupDate = null; continue;
            }
            if (groupLabel == "Grand Total")
            {
                grandFound = true;
                if (reportTotal != Money(sheet, row, 9)) result.Errors.Add("Cheque grand total mismatch.");
                continue;
            }
            if (!Regex.IsMatch(reference, @"^CH\d+$"))
            {
                if (headerFound && reference != "") result.Errors.Add($"Cheque row {row}: unrecognized receipt reference.");
                continue;
            }
            if (grandFound) result.Errors.Add($"Cheque row {row}: data follows the grand total.");
            if (title != "") groupDate = Date(sheet, row, 1);
            if (groupDate == null) throw new InvalidDataException("Missing cheque group date.");
            var amount = Money(sheet, row, 9);
            result.Cheques.Add(new ChequeSourceRow(row, Text(sheet, row, 5).ToUpperInvariant(), Text(sheet, row, 6),
                reference, Text(sheet, row, 4), Text(sheet, row, 7), Text(sheet, row, 8),
                groupDate.Value, Date(sheet, row, 2), amount));
            groupTotal += amount; reportTotal += amount;
        }
        if (!headerFound || !grandFound || result.ChequeReportDate == null || groupDate != null)
            result.Errors.Add("Cheque report headers, final subtotal, report date or grand total are missing.");
    }
}
