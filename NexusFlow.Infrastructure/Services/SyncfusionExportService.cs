using NexusFlow.AppCore.Interfaces;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace NexusFlow.Infrastructure.Services
{
    public class SyncfusionExportService : IExportService
    {
        public byte[] ExportToExcel<T>(IEnumerable<T> data, ExportMetadata metadata, string sheetName = "Report")
        {
            using ExcelEngine excelEngine = new ExcelEngine();
            IApplication application = excelEngine.Excel;
            application.DefaultVersion = ExcelVersion.Xlsx;

            IWorkbook workbook = application.Workbooks.Create(1);
            IWorksheet sheet = workbook.Worksheets[0];
            sheet.Name = sheetName;

            PropertyInfo[] properties = typeof(T).GetProperties();
            int colCount = properties.Length > 0 ? properties.Length : 5;

            int currentRow = 1;

            // 1. Company Name (Merged, Large, Centered)
            sheet.Range[currentRow, 1, currentRow, colCount].Merge();
            sheet.Range[currentRow, 1].Text = metadata.CompanyName.ToUpper();
            sheet.Range[currentRow, 1].CellStyle.Font.Bold = true;
            sheet.Range[currentRow, 1].CellStyle.Font.Size = 16;
            sheet.Range[currentRow, 1].CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;
            currentRow++;

            // 2. Report Title
            sheet.Range[currentRow, 1, currentRow, colCount].Merge();
            sheet.Range[currentRow, 1].Text = metadata.ReportTitle;
            sheet.Range[currentRow, 1].CellStyle.Font.Bold = true;
            sheet.Range[currentRow, 1].CellStyle.Font.Size = 12;
            sheet.Range[currentRow, 1].CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;
            currentRow++;

            // 3. Generated Timestamp
            sheet.Range[currentRow, 1, currentRow, colCount].Merge();
            sheet.Range[currentRow, 1].Text = $"Generated On: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            sheet.Range[currentRow, 1].CellStyle.Font.Italic = true;
            sheet.Range[currentRow, 1].CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;
            currentRow += 2; // Add a blank row

            // 4. Applied Filters
            if (metadata.AppliedFilters.Any())
            {
                sheet.Range[currentRow, 1].Text = "Applied Filters:";
                sheet.Range[currentRow, 1].CellStyle.Font.Bold = true;
                currentRow++;

                foreach (var filter in metadata.AppliedFilters)
                {
                    sheet.Range[currentRow, 1].Text = filter.Key;
                    sheet.Range[currentRow, 1].CellStyle.Font.Bold = true;
                    sheet.Range[currentRow, 2].Text = filter.Value;
                    currentRow++;
                }
                currentRow++; // Add a blank row before data
            }

            // 5. Data Headers (With Background Color)
            int headerRow = currentRow;
            for (int i = 0; i < properties.Length; i++)
            {
                sheet.Range[headerRow, i + 1].Text = properties[i].Name;
                sheet.Range[headerRow, i + 1].CellStyle.Font.Bold = true;
                sheet.Range[headerRow, i + 1].CellStyle.Font.Color = ExcelKnownColors.White;
                sheet.Range[headerRow, i + 1].CellStyle.Color = Color.FromArgb(13, 110, 253); // Bootstrap Primary Blue
                sheet.Range[headerRow, i + 1].CellStyle.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Medium;
            }
            currentRow++;

            // 6. Populate Data Rows
            var dataList = data.ToList();
            for (int r = 0; r < dataList.Count; r++)
            {
                for (int c = 0; c < properties.Length; c++)
                {
                    var value = properties[c].GetValue(dataList[r], null);
                    sheet.Range[currentRow, c + 1].Value2 = value?.ToString() ?? string.Empty;
                }
                currentRow++;
            }

            // Auto-fit and style
            sheet.UsedRange.AutofitColumns();

            using MemoryStream stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] ExportToPdf<T>(IEnumerable<T> data, ExportMetadata metadata)
        {
            using PdfDocument document = new PdfDocument();
            document.PageSettings.Orientation = PdfPageOrientation.Landscape;
            document.PageSettings.Margins.All = 40;
            PdfPage page = document.Pages.Add();

            float yPos = 0;

            // Fonts
            PdfStandardFont companyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
            PdfStandardFont titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
            PdfStandardFont regularFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
            PdfStandardFont boldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);

            // 1. Company Name & Title
            page.Graphics.DrawString(metadata.CompanyName.ToUpper(), companyFont, PdfBrushes.DarkBlue, new PointF(0, yPos));
            yPos += 25;
            page.Graphics.DrawString(metadata.ReportTitle, titleFont, PdfBrushes.Black, new PointF(0, yPos));
            yPos += 20;
            page.Graphics.DrawString($"Generated On: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", regularFont, PdfBrushes.Gray, new PointF(0, yPos));
            yPos += 25;

            // 2. Applied Filters
            if (metadata.AppliedFilters.Any())
            {
                page.Graphics.DrawString("Applied Filters:", boldFont, PdfBrushes.Black, new PointF(0, yPos));
                yPos += 15;
                foreach (var filter in metadata.AppliedFilters)
                {
                    page.Graphics.DrawString($"{filter.Key}: {filter.Value}", regularFont, PdfBrushes.DarkSlateGray, new PointF(10, yPos));
                    yPos += 15;
                }
                yPos += 10;
            }

            // 3. Generate Grid
            PdfGrid pdfGrid = new PdfGrid();
            pdfGrid.Style.CellPadding = new PdfPaddings(5, 5, 5, 5);

            PropertyInfo[] properties = typeof(T).GetProperties();
            pdfGrid.Columns.Add(properties.Length);

            // Header Styling
            PdfGridRow header = pdfGrid.Headers.Add(1)[0];
            header.Style.BackgroundBrush = new PdfSolidBrush(Color.FromArgb(13, 110, 253));
            header.Style.TextBrush = PdfBrushes.White;
            header.Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);

            for (int i = 0; i < properties.Length; i++)
            {
                // Put spaces before capital letters for cleaner headers (e.g. "InvoiceNo" -> "Invoice No")
                string cleanName = System.Text.RegularExpressions.Regex.Replace(properties[i].Name, "([A-Z])", " $1").Trim();
                header.Cells[i].Value = cleanName;
            }

            // Data Styling
            PdfStandardFont cellFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9);
            bool isAlternate = false;

            foreach (var item in data)
            {
                PdfGridRow row = pdfGrid.Rows.Add();
                row.Style.Font = cellFont;

                if (isAlternate) row.Style.BackgroundBrush = new PdfSolidBrush(Color.FromArgb(248, 249, 250)); // Light Gray
                isAlternate = !isAlternate;

                for (int i = 0; i < properties.Length; i++)
                {
                    var value = properties[i].GetValue(item, null);
                    row.Cells[i].Value = value?.ToString() ?? string.Empty;
                }
            }

            // Draw the grid. If it overflows the page, Syncfusion automatically creates new pages!
            PdfGridLayoutFormat layoutFormat = new PdfGridLayoutFormat { Layout = PdfLayoutType.Paginate };
            pdfGrid.Draw(page, new PointF(0, yPos), layoutFormat);

            using MemoryStream stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }
    }
}
