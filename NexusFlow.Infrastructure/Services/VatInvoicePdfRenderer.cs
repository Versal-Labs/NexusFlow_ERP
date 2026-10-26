using System.Globalization;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.Domain.Entities.System;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

namespace NexusFlow.Infrastructure.Services;

/// <summary>The VAT-only A4 structure supplied by the business. All amounts remain invoice values.</summary>
internal sealed class VatInvoicePdfRenderer
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-LK");
    private readonly PdfFont regular = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f);
    private readonly PdfFont bold = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f, PdfFontStyle.Bold);
    private readonly PdfFont small = new PdfStandardFont(PdfFontFamily.Helvetica, 7);
    private readonly PdfPen pen = new(Color.Black, 0.65f);
    private readonly PdfStringFormat right = new() { Alignment = PdfTextAlignment.Right };
    private readonly PdfStringFormat center = new() { Alignment = PdfTextAlignment.Center };
    private readonly PrintDocumentDto data;
    private readonly CompanyProfile company;
    private readonly VatInvoicePrintDto vat;
    private readonly PdfDocument document = new();
    private readonly Stream? logo;
    private PdfPage page = null!;
    private float width;
    private float height;
    private float[] columns = [];

    private VatInvoicePdfRenderer(PrintDocumentDto data, CompanyProfile company, Stream? logo)
    {
        this.data = data;
        this.company = company;
        this.logo = logo;
        vat = data.VatInvoice!;
    }

    public static byte[] Render(PrintDocumentDto data, CompanyProfile company, Stream? logo)
    {
        var renderer = new VatInvoicePdfRenderer(data, company, logo);
        using var owned = renderer.document;
        return renderer.Build();
    }

    private byte[] Build()
    {
        document.PageSettings.Size = PdfPageSize.A4;
        document.PageSettings.Margins.All = 16;
        page = document.Pages.Add();
        width = page.GetClientSize().Width;
        height = page.GetClientSize().Height;
        columns = [0, width * .58f, width * .67f, width * .75f, width * .87f, width];

        var words = AmountInWords(data.GrandTotal);
        var wordsHeight = Math.Max(29, Lines(words, width - 102, regular).Count * 11 + 8);
        var cheque = $"Cheque should be written in favour of {company.CompanyName}";
        var chequeHeight = Lines(cheque, width, bold).Count * 11;
        var noteLines = Lines(data.Notes, width - 8, regular);
        var shortNotesHeight = !string.IsNullOrWhiteSpace(data.Notes) && noteLines.Count <= 4 ? 14 + noteLines.Count * 11 : 0;
        var footerHeight = 60 + (data.DiscountTotal != 0 ? 18 : 0) + 4 + wordsHeight + 8 + chequeHeight
            + 5 + 10 + 39 + 28 + 48 + 20 + shortNotesHeight;
        var bodyBottom = height - 14 - footerHeight;
        var top = DrawHeader();
        var y = DrawTableHeader(top);
        if (bodyBottom < y + 28)
            throw new InvalidOperationException("VAT invoice party details are too long to fit on A4. Shorten the company or billing address.");

        foreach (var item in data.LineItems)
        {
            var descriptionWidth = columns[1] - 8;
            var description = string.Join("   ", new[] { item.ItemCode, item.Description }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var lines = Lines(description, descriptionWidth, regular);
            var offset = 0;
            while (offset < lines.Count)
            {
                var available = (int)((bodyBottom - y - 6) / 11);
                if (available < 1)
                {
                    FinishTable(top, bodyBottom);
                    Text("Continued on next page", 0, bodyBottom + 8, width, 12, small, right);
                    page = document.Pages.Add();
                    top = DrawHeader();
                    y = DrawTableHeader(top);
                    continue;
                }
                var count = Math.Min(available, lines.Count - offset);
                var rowHeight = Math.Max(19, count * 11 + 6);
                for (var line = 0; line < count; line++)
                    Text(lines[offset + line], 4, y + 3 + line * 11, descriptionWidth, 11);
                if (offset == 0)
                {
                    var gross = item.Quantity * item.UnitPrice;
                    Cell(1, y + 3, item.Quantity.ToString("0.##", Culture));
                    Cell(2, y + 3, (gross == 0 ? 0 : item.Discount / gross * 100).ToString("N2", Culture));
                    Cell(3, y + 3, Money(item.UnitPrice));
                    Cell(4, y + 3, Money(item.LineTotal));
                }
                y += rowHeight;
                offset += count;
            }
        }
        FinishTable(top, bodyBottom);
        DrawFooter(bodyBottom, words, wordsHeight, cheque, chequeHeight, shortNotesHeight);
        if (shortNotesHeight == 0) DrawNotes();

        for (var i = 0; i < document.Pages.Count; i++)
        {
            page = document.Pages[i];
            Text($"Page {i + 1} of {document.Pages.Count}", 0, height - 9, width, 9, small, right);
        }
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private float DrawHeader()
    {
        float companyX = 0;
        if (logo != null)
        {
            try
            {
                logo.Position = 0;
                using var bitmap = new PdfBitmap(logo);
                var scale = Math.Min(82f / bitmap.Width, 56f / bitmap.Height);
                page.Graphics.DrawImage(bitmap, 0, 0, bitmap.Width * scale, bitmap.Height * scale);
                companyX = 94;
            }
            catch
            {
                // A missing or unreadable logo must not suppress the supplier's legal details.
            }
        }
        var name = company.CompanyName ?? string.Empty;
        float nameSize = 25;
        while (nameSize > 10 && new PdfStandardFont(PdfFontFamily.Helvetica, nameSize).MeasureString(name).Width > width - companyX)
            nameSize -= .5f;
        var nameFont = new PdfStandardFont(PdfFontFamily.Helvetica, nameSize);
        var nameLines = Lines(name, width - companyX, nameFont);
        var nameLineHeight = nameFont.MeasureString("Ag").Height + 2;
        var nameHeight = nameLines.Count * nameLineHeight;
        TextBlock(name, companyX, 0, width - companyX, nameFont, nameLineHeight);
        var contact = string.Join("  ", new[] { company.PrimaryAddress?.Replace("\r", "").Replace("\n", ", "), company.ContactPhone, company.ContactEmail }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var contactHeight = TextBlock(contact, companyX, nameHeight + 3, width - companyX, regular);
        var headerBottom = Math.Max(60, nameHeight + contactHeight + 10);
        Line(0, headerBottom, width, headerBottom);
        var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
        Box(width / 2 - 61, headerBottom + 7, 122, 22);
        Text("TAX INVOICE", width / 2 - 61, headerBottom + 9, 122, 20, titleFont, center);

        var panelY = headerBottom + 36;
        var gap = 24f;
        var leftWidth = width * .44f;
        var rightX = leftWidth + gap;
        var rightWidth = width - rightX;
        var leftLabel = 72f;
        var rightLabel = 85f;
        var supplier = new[] { ("Supplier's Name", name), ("Address", company.PrimaryAddress ?? ""),
            ("Telephone No", company.ContactPhone ?? ""), ("Email", company.ContactEmail ?? "") };
        var purchaser = new[] { ("Purchaser's Name", data.CustomerOrSupplierName ?? ""), ("Address", data.BillingAddress), ("Telephone", vat.PurchaserPhone) };
        float DetailHeight((string, string)[] fields, float w) => fields.Sum(x => Math.Max(14, Lines(x.Item2, w - 8, regular).Count * 11 + 3));
        var lastHeight = Math.Max(18, Math.Max(Lines(vat.PaymentTerms, (rightWidth - rightLabel) * .54f - 8, regular).Count,
            Lines("Rep : " + vat.SalesRepresentative, (rightWidth - rightLabel) * .46f - 8, regular).Count) * 11 + 6);
        var rowHeight = Math.Max(18, Math.Max(Lines(data.DocumentNumber, rightWidth - rightLabel - 8, regular).Count,
            Lines(vat.CustomerPoNumber, rightWidth - rightLabel - 8, regular).Count) * 11 + 6);
        var detailsBottom = Math.Max(102, Math.Max(rowHeight * 2 + 3 + DetailHeight(supplier, leftWidth - leftLabel),
            rowHeight * 3 + 3 + DetailHeight(purchaser, rightWidth - rightLabel)));
        var panelHeight = detailsBottom + lastHeight;
        Box(0, panelY, leftWidth, panelHeight);
        Box(rightX, panelY, rightWidth, panelHeight);
        Line(leftLabel, panelY, leftLabel, panelY + panelHeight);
        Line(rightX + rightLabel, panelY, rightX + rightLabel, panelY + panelHeight);
        PanelRow(0, leftWidth, leftLabel, panelY, rowHeight, "Date of Invoice", Date(data.DocumentDate));
        PanelRow(rightX, rightWidth, rightLabel, panelY, rowHeight, "Tax Invoice No", data.DocumentNumber);
        PanelRow(0, leftWidth, leftLabel, panelY + rowHeight, rowHeight, "Supplier's Tin", company.TaxRegistrationNumber ?? "");
        PanelRow(rightX, rightWidth, rightLabel, panelY + rowHeight, rowHeight, "P.O. Number", vat.CustomerPoNumber);
        // Keep both panels aligned, allowing the purchaser TIN its own row as in the reference.
        PanelRow(rightX, rightWidth, rightLabel, panelY + rowHeight * 2, rowHeight, "Purchaser's Tin", vat.PurchaserTin);
        DrawDetails(supplier, 0, leftLabel, leftWidth, panelY + rowHeight * 2 + 3);
        DrawDetails(purchaser, rightX, rightLabel, rightWidth, panelY + rowHeight * 3 + 3);
        var lastY = panelY + panelHeight - lastHeight;
        PanelRow(0, leftWidth, leftLabel, lastY, lastHeight, "Date of Supply", Date(vat.SupplyDate));
        Line(rightX, lastY, width, lastY);
        TextBlock("Mode of Payment", rightX + 4, lastY + 3, rightLabel - 8, regular);
        var valueWidth = rightWidth - rightLabel;
        TextBlock(vat.PaymentTerms, rightX + rightLabel + 4, lastY + 3, valueWidth * .54f - 8, regular);
        Line(rightX + rightLabel + valueWidth * .54f, lastY, rightX + rightLabel + valueWidth * .54f, lastY + lastHeight);
        TextBlock("Rep : " + vat.SalesRepresentative, rightX + rightLabel + valueWidth * .54f + 4, lastY + 3, valueWidth * .46f - 8, regular);
        return panelY + panelHeight + 9;
    }

    private void DrawDetails((string Label, string Value)[] fields, float x, float labelWidth, float panelWidth, float y)
    {
        foreach (var field in fields)
        {
            Text(field.Label, x + 4, y, labelWidth - 7, 12);
            y += Math.Max(14, TextBlock(field.Value, x + labelWidth + 4, y, panelWidth - labelWidth - 8, regular) + 3);
        }
    }

    private void PanelRow(float x, float w, float labelWidth, float y, float h, string label, string value)
    {
        TextBlock(label, x + 4, y + 3, labelWidth - 8, regular);
        TextBlock(value, x + labelWidth + 4, y + 3, w - labelWidth - 8, regular);
        Line(x, y + h, x + w, y + h);
    }

    private float DrawTableHeader(float y)
    {
        page.Graphics.DrawRectangle(new PdfSolidBrush(Color.FromArgb(222, 222, 222)), new RectangleF(0, y, width, 20));
        var labels = new[] { "Description of Goods or Services", "Qty", "Disc %", "Unit Price", "Total Ex. VAT" };
        for (var i = 0; i < labels.Length; i++)
            Text(labels[i], columns[i] + 4, y + 5, columns[i + 1] - columns[i] - 8, 12, i == 0 ? bold : small, i == 0 ? null : right);
        Line(0, y + 20, width, y + 20);
        return y + 20;
    }

    private void FinishTable(float top, float bottom)
    {
        Box(0, top, width, bottom - top);
        foreach (var x in columns.Skip(1).SkipLast(1)) Line(x, top, x, bottom);
    }

    private void DrawFooter(float y, string words, float wordsHeight, string cheque, float chequeHeight, float shortNotesHeight)
    {
        if (data.DiscountTotal != 0)
        {
            Summary("Invoice Discount", -data.DiscountTotal, y, 18);
            y += 18;
        }
        var supplyValue = Math.Max(0, data.SubTotal - data.DiscountTotal);
        Summary("Total Value of Supply", supplyValue, y, 20);
        Cell(1, y + 4, data.LineItems.Sum(x => x.Quantity).ToString("0.##", Culture));
        Line(columns[1], y, columns[1], y + 20);
        Line(columns[2], y, columns[2], y + 20);
        y += 20;
        var rate = supplyValue == 0 ? "" : $" {(data.TaxTotal / supplyValue * 100).ToString("0.##", Culture)}%";
        Summary("VAT Amount" + rate, data.TaxTotal, y, 20);
        y += 20;
        page.Graphics.DrawRectangle(new PdfSolidBrush(Color.FromArgb(193, 193, 193)), new RectangleF(0, y, width, 20));
        Summary("Total Amount including VAT", data.GrandTotal, y, 20, true);
        y += 24;
        Box(0, y, width, wordsHeight);
        Text("Amount in Words", 4, y + 5, 92, 12);
        TextBlock(words, 101, y + 5, width - 106, regular);
        y += wordsHeight + 8;
        if (shortNotesHeight > 0)
        {
            Text("Additional Information", 0, y, width, 12, bold);
            TextBlock(data.Notes, 0, y + 13, width - 8, regular);
            y += shortNotesHeight;
        }
        TextBlock(cheque, 0, y, width, bold);
        y += chequeHeight + 5;
        var aging = vat.AccountAging;
        var agingWidth = width * .70f;
        Text($"Account aging as at {Date(aging.AsOfDate)} ({data.CurrencyCode})", 0, y, agingWidth, 10, small);
        y += 10;
        var labels = new[] { "0 - 30", "31 - 60", "61 - 90", "Over 90", "Total" };
        var values = new[] { aging.Days0To30, aging.Days31To60, aging.Days61To90, aging.Over90Days, aging.TotalOutstanding };
        var cellWidth = agingWidth / 5;
        page.Graphics.DrawRectangle(PdfBrushes.Black, new RectangleF(0, y, agingWidth, 17));
        for (var i = 0; i < 5; i++)
        {
            page.Graphics.DrawString(labels[i], regular, PdfBrushes.White, new RectangleF(i * cellWidth, y + 3, cellWidth, 13), center);
            Box(i * cellWidth, y + 17, cellWidth, 19);
            Text(Money(values[i]), i * cellWidth + 3, y + 21, cellWidth - 6, 12, small, right);
        }
        var bankX = agingWidth + 14;
        Text("Account Detail :-", bankX, y, width - bankX, 12);
        Text("Bank          :", bankX, y + 14, width - bankX, 12);
        Text("Account No :", bankX, y + 27, width - bankX, 12);
        y += 39;
        Text("Days past due; 0-30 includes current balances.", 0, y, width, 10, small);
        Text($"Unapplied credit: {Money(aging.UnappliedCredit)} (deducted in Total)", 0, y + 10, width, 10, small);
        y += 28;
        Text("Return Clause :", 0, y, width, 12);
        y += 48;
        var signatureWidth = width * .22f;
        var positions = new[] { 20f, (width - signatureWidth) / 2, width - signatureWidth - 12 };
        var signatures = new[] { "Customer Signature", "Sales Rep Signature", "Checked By" };
        for (var i = 0; i < positions.Length; i++)
        {
            Line(positions[i], y, positions[i] + signatureWidth, y);
            Text(signatures[i], positions[i] - 4, y + 3, signatureWidth + 8, 12, regular, center);
        }
    }

    private void DrawNotes()
    {
        if (string.IsNullOrWhiteSpace(data.Notes)) return;
        // Keep free-form notes without squeezing the reference's signature and account areas.
        var lines = Lines(data.Notes, width - 8, regular);
        var index = 0;
        while (index < lines.Count)
        {
            page = document.Pages.Add();
            Text($"TAX INVOICE - {data.DocumentNumber}", 0, 0, width, 15, bold);
            Text("Additional Information", 0, 24, width, 15, bold);
            float y = 44;
            while (index < lines.Count && y < height - 24)
            {
                Text(lines[index++], 0, y, width, 11);
                y += 11;
            }
        }
    }

    private void Summary(string label, decimal amount, float y, float h, bool prominent = false)
    {
        Box(0, y, width, h);
        Line(columns[4], y, columns[4], y + h);
        Text(label, 5, y + 4, columns[4] - 10, 13, prominent ? bold : regular);
        Cell(4, y + 4, Money(amount), prominent ? bold : regular);
    }

    private void Cell(int index, float y, string value, PdfFont? font = null)
        => Text(value, columns[index] + 3, y, columns[index + 1] - columns[index] - 7, 13, font, right);
    private void Text(string value, float x, float y, float w, float h, PdfFont? font = null, PdfStringFormat? format = null)
        => page.Graphics.DrawString(value, font ?? regular, PdfBrushes.Black, new RectangleF(x, y, w, h), format ?? new PdfStringFormat());
    private void Line(float x1, float y1, float x2, float y2) => page.Graphics.DrawLine(pen, x1, y1, x2, y2);
    private void Box(float x, float y, float w, float h) => page.Graphics.DrawRectangle(pen, new RectangleF(x, y, w, h));
    private float TextBlock(string value, float x, float y, float w, PdfFont font, float lineHeight = 11)
    {
        var lines = Lines(value, w, font);
        for (var i = 0; i < lines.Count; i++) Text(lines[i], x, y + i * lineHeight, w, lineHeight + 1, font);
        return lines.Count * lineHeight;
    }

    private static List<string> Lines(string? value, float width, PdfFont font)
    {
        var result = new List<string>();
        foreach (var paragraph in (value ?? "").Replace("\r", "").Split('\n'))
        {
            var current = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var next = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureString(next).Width <= width) { current = next; continue; }
                if (current.Length > 0) result.Add(current);
                current = "";
                foreach (var character in word)
                {
                    if (font.MeasureString(current + character).Width > width && current.Length > 0)
                    { result.Add(current); current = ""; }
                    current += character;
                }
            }
            result.Add(current);
        }
        return result;
    }

    private static string Money(decimal value) => value.ToString("N2", Culture);
    private static string Date(DateTime value) => value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    internal static string AmountInWords(decimal amount)
    {
        var rounded = decimal.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var whole = decimal.Truncate(rounded);
        var cents = (int)((rounded - whole) * 100);
        var words = IntegerWords(whole);
        if (cents != 0) words += " AND " + IntegerWords(cents) + " CENTS";
        return (amount < 0 ? "MINUS " : "") + words + " ONLY";
    }

    private static string IntegerWords(decimal value)
    {
        string[] units = ["ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN"];
        string[] tens = ["", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY"];
        if (value < 20) return units[(int)value];
        if (value < 100) return tens[(int)(value / 10)] + (value % 10 > 0 ? " " + IntegerWords(value % 10) : "");
        (decimal Size, string Name)[] scales = [(1_000_000_000_000_000_000_000_000m, "SEPTILLION"), (1_000_000_000_000_000_000_000m, "SEXTILLION"),
            (1_000_000_000_000_000_000m, "QUINTILLION"), (1_000_000_000_000_000m, "QUADRILLION"), (1_000_000_000_000m, "TRILLION"),
            (1_000_000_000m, "BILLION"), (1_000_000m, "MILLION"), (1_000m, "THOUSAND"), (100m, "HUNDRED")];
        var scale = scales.First(x => value >= x.Size);
        return IntegerWords(decimal.Truncate(value / scale.Size)) + " " + scale.Name
            + (value % scale.Size > 0 ? (scale.Size == 100 ? " AND " : " ") + IntegerWords(value % scale.Size) : "");
    }
}
