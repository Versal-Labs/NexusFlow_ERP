using NexusFlow.AppCore.Features.Payroll.Queries;
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

        public byte[] GeneratePayslipPdf(PayslipDto slip)
        {
            using PdfDocument document = new PdfDocument();
            // Payslips are usually Portrait A4
            document.PageSettings.Orientation = PdfPageOrientation.Portrait;
            document.PageSettings.Margins.All = 40;

            PdfPage page = document.Pages.Add();
            PdfGraphics g = page.Graphics;

            // 1. Define Fonts & Brushes
            PdfFont titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
            PdfFont headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
            PdfFont regularFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
            PdfFont boldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
            PdfFont netPayFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);

            PdfBrush blackBrush = PdfBrushes.Black;
            PdfBrush grayBrush = PdfBrushes.DarkGray;
            PdfPen linePen = new PdfPen(Color.LightGray, 1f);
            PdfPen darkPen = new PdfPen(Color.Black, 1.5f);

            float yPos = 0;
            float pageWidth = page.GetClientSize().Width;
            float leftColX = 0;
            float rightColX = pageWidth / 2 + 10; // Split the page in half

            // 2. HEADER SECTION (Company & Month)
            // Center the Company Name
            SizeF titleSize = titleFont.MeasureString(slip.CompanyName);
            g.DrawString(slip.CompanyName, titleFont, blackBrush, new PointF((pageWidth - titleSize.Width) / 2, yPos));
            yPos += 25;

            SizeF slipTitleSize = headerFont.MeasureString($"PAYSLIP - {slip.MonthYear}");
            g.DrawString($"PAYSLIP - {slip.MonthYear}", headerFont, grayBrush, new PointF((pageWidth - slipTitleSize.Width) / 2, yPos));
            yPos += 25;

            g.DrawLine(darkPen, 0, yPos, pageWidth, yPos);
            yPos += 15;

            // 3. EMPLOYEE DETAILS SECTION
            g.DrawString("Employee Name:", boldFont, blackBrush, new PointF(0, yPos));
            g.DrawString(slip.EmployeeName, regularFont, blackBrush, new PointF(100, yPos));

            g.DrawString("Employee Code:", boldFont, blackBrush, new PointF(rightColX, yPos));
            g.DrawString(slip.EmployeeCode, regularFont, blackBrush, new PointF(rightColX + 100, yPos));
            yPos += 20;

            g.DrawString("NIC No:", boldFont, blackBrush, new PointF(0, yPos));
            g.DrawString(slip.NIC, regularFont, blackBrush, new PointF(100, yPos));

            g.DrawString("EPF No:", boldFont, blackBrush, new PointF(rightColX, yPos));
            g.DrawString(slip.EPFNo, regularFont, blackBrush, new PointF(rightColX + 100, yPos));
            yPos += 25;

            g.DrawLine(darkPen, 0, yPos, pageWidth, yPos);
            yPos += 15;

            // 4. FINANCIAL BODY (Two Columns: Earnings vs Deductions)
            float startBodyY = yPos;

            // --- LEFT COLUMN: EARNINGS ---
            g.DrawString("EARNINGS", headerFont, blackBrush, new PointF(leftColX, yPos));
            yPos += 20;

            g.DrawString("Basic Salary", regularFont, blackBrush, new PointF(leftColX, yPos));
            DrawRightAligned(g, slip.BasicSalary.ToString("N2"), regularFont, blackBrush, rightColX - 20, yPos);
            yPos += 15;

            foreach (var allowance in slip.Allowances)
            {
                g.DrawString(allowance.Key, regularFont, blackBrush, new PointF(leftColX, yPos));
                DrawRightAligned(g, allowance.Value.ToString("N2"), regularFont, blackBrush, rightColX - 20, yPos);
                yPos += 15;
            }

            // --- RIGHT COLUMN: DEDUCTIONS ---
            float currentRightY = startBodyY;
            g.DrawString("DEDUCTIONS", headerFont, blackBrush, new PointF(rightColX, currentRightY));
            currentRightY += 20;

            foreach (var deduction in slip.Deductions)
            {
                g.DrawString(deduction.Key, regularFont, blackBrush, new PointF(rightColX, currentRightY));
                DrawRightAligned(g, deduction.Value.ToString("N2"), regularFont, blackBrush, pageWidth, currentRightY);
                currentRightY += 15;
            }

            // Push yPos to whichever column was longer
            yPos = Math.Max(yPos, currentRightY) + 20;

            // 5. TOTALS LINE
            g.DrawLine(linePen, 0, yPos, pageWidth, yPos);
            yPos += 10;

            g.DrawString("Gross Earnings:", boldFont, blackBrush, new PointF(leftColX, yPos));
            DrawRightAligned(g, slip.GrossPay.ToString("N2"), boldFont, blackBrush, rightColX - 20, yPos);

            g.DrawString("Total Deductions:", boldFont, blackBrush, new PointF(rightColX, yPos));
            DrawRightAligned(g, slip.TotalDeductions.ToString("N2"), boldFont, blackBrush, pageWidth, yPos);
            yPos += 20;

            // 6. NET PAY BOX (The most important part)
            yPos += 10;
            RectangleF netPayRect = new RectangleF(0, yPos, pageWidth, 40);
            g.DrawRectangle(new PdfSolidBrush(Color.FromArgb(240, 248, 255)), netPayRect); // Light Blue background
            g.DrawRectangle(darkPen, netPayRect); // Dark Border

            g.DrawString("NET PAY:", netPayFont, blackBrush, new PointF(15, yPos + 12));
            DrawRightAligned(g, "LKR " + slip.NetPay.ToString("N2"), netPayFont, blackBrush, pageWidth - 15, yPos + 12);
            yPos += 70;

            // 7. FOOTER
            g.DrawString("This is a system-generated document and does not require a signature.", new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Italic), grayBrush, new PointF(0, yPos));

            // Save and return
            using MemoryStream stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }

        // Helper function to right-align numbers cleanly like Excel does
        private void DrawRightAligned(PdfGraphics g, string text, PdfFont font, PdfBrush brush, float rightX, float y)
        {
            SizeF size = font.MeasureString(text);
            g.DrawString(text, font, brush, new PointF(rightX - size.Width, y));
        }
    }
}
