using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusFlow.Infrastructure.Services
{
    public class DocumentRenderingService : IDocumentRenderingService
    {
        private readonly IErpDbContext _dbContext;
        private readonly ICompanyProfileService _companyProfileService;
        private readonly IGlobalStorageCoordinator _storageCoordinator;

        public DocumentRenderingService(
            IErpDbContext dbContext,
            ICompanyProfileService companyProfileService,
            IGlobalStorageCoordinator storageCoordinator)
        {
            _dbContext = dbContext;
            _companyProfileService = companyProfileService;
            _storageCoordinator = storageCoordinator;
        }

        public async Task<byte[]> RenderDocumentToPdfAsync(DocumentType documentType, PrintDocumentDto data, CancellationToken cancellationToken = default)
        {
            var template = await _dbContext.DocumentTemplates
                .Where(t => t.DocumentType == documentType && t.IsActive && t.IsDefault)
                .OrderBy(t => t.TaxProfile == TaxProfile.All ? 0 : 1)
                .FirstOrDefaultAsync(cancellationToken);

            if (template != null && !string.IsNullOrWhiteSpace(template.BlobUrl))
            {
                return await RenderUsingWordTemplateAsync(documentType, template.BlobUrl, data, cancellationToken);
            }

            return await RenderFallbackPdfAsync(documentType, data, cancellationToken);
        }

        private async Task<byte[]> RenderUsingWordTemplateAsync(DocumentType documentType, string blobUrl, PrintDocumentDto data, CancellationToken cancellationToken)
        {
            try
            {
                var profile = await _companyProfileService.GetProfileAsync(cancellationToken);
                var (templateStream, _) = await _storageCoordinator.RetrieveFileAsync(blobUrl, cancellationToken);

                using var memoryStream = new MemoryStream();
                await templateStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                using var wordDocument = new WordDocument(memoryStream, FormatType.Docx);
                using var companyLogoStream = await TryLoadCompanyLogoAsync(profile.LogoBlobUrl, cancellationToken);

                var mergeFields = BuildMergeFields(data, profile, companyLogoStream != null);

                void MergeCompanyLogo(object sender, MergeImageFieldEventArgs args)
                {
                    if (companyLogoStream == null)
                    {
                        args.Skip = true;
                        return;
                    }

                    companyLogoStream.Position = 0;
                    args.ImageStream = companyLogoStream;

                    if (args.Picture != null)
                    {
                        args.Picture.Width = 120;
                        args.Picture.Height = 45;
                    }
                }

                wordDocument.MailMerge.MergeImageField += MergeCompanyLogo;

                try
                {
                    var fields = mergeFields.ToArray();
                    wordDocument.MailMerge.Execute(
                        fields.Select(x => x.Key).ToArray(),
                        fields.Select(x => x.Value).ToArray());

                    foreach (var table in BuildMergeTables(documentType, data))
                    {
                        wordDocument.MailMerge.ExecuteGroup(new MailMergeDataTable(table.Key, table.Value));
                    }
                }
                finally
                {
                    wordDocument.MailMerge.MergeImageField -= MergeCompanyLogo;
                }

                using var render = new DocIORenderer();
                using var pdfDocument = render.ConvertToPDF(wordDocument);

                using var outStream = new MemoryStream();
                pdfDocument.Save(outStream);
                return outStream.ToArray();
            }
            catch (Exception)
            {
                // Fallback on error
                return await RenderFallbackPdfAsync(documentType, data, cancellationToken);
            }
        }

        private static Dictionary<string, string> BuildMergeFields(PrintDocumentDto data, Domain.Entities.System.CompanyProfile profile, bool hasCompanyLogo)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DocumentId"] = data.DocumentId ?? string.Empty,
                ["DocumentType"] = data.DocumentType ?? string.Empty,
                ["DocumentNumber"] = data.DocumentNumber ?? string.Empty,
                ["DocumentDate"] = data.DocumentDate.ToString("yyyy-MM-dd"),
                ["CustomerOrSupplierName"] = data.CustomerOrSupplierName ?? string.Empty,
                ["BillingAddress"] = data.BillingAddress ?? string.Empty,
                ["ShippingAddress"] = data.ShippingAddress ?? string.Empty,
                ["Notes"] = data.Notes ?? string.Empty,
                ["SubTotal"] = data.SubTotal.ToString("N2"),
                ["TaxTotal"] = data.TaxTotal.ToString("N2"),
                ["DiscountTotal"] = data.DiscountTotal.ToString("N2"),
                ["GrandTotal"] = data.GrandTotal.ToString("N2"),
                ["CurrencyCode"] = data.CurrencyCode ?? string.Empty,
                ["CompanyName"] = profile.CompanyName ?? string.Empty,
                ["CompanyTaxRegistrationNumber"] = profile.TaxRegistrationNumber ?? string.Empty,
                ["CompanyBusinessRegistrationNumber"] = profile.BusinessRegistrationNumber ?? string.Empty,
                ["CompanyAddress"] = profile.PrimaryAddress ?? string.Empty,
                ["CompanyEmail"] = profile.ContactEmail ?? string.Empty,
                ["CompanyPhone"] = profile.ContactPhone ?? string.Empty,
                ["CompanyLogo"] = hasCompanyLogo ? "CompanyLogo" : string.Empty
            };

            foreach (var field in data.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Key))
                {
                    fields[field.Key.Trim()] = field.Value ?? string.Empty;
                }
            }

            return fields;
        }

        private static Dictionary<string, List<PrintTableRowDto>> BuildMergeTables(DocumentType documentType, PrintDocumentDto data)
        {
            var tables = new Dictionary<string, List<PrintTableRowDto>>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in data.Tables)
            {
                if (!string.IsNullOrWhiteSpace(table.Key) && table.Value.Count > 0)
                {
                    tables[table.Key.Trim()] = table.Value;
                }
            }

            if (data.LineItems.Count > 0)
            {
                var rows = data.LineItems.Select(PrintTableRowDto.FromLineItem).ToList();
                tables.TryAdd("LineItems", rows);

                var defaultTableName = DefaultTableName(documentType);
                if (!string.IsNullOrWhiteSpace(defaultTableName))
                {
                    tables.TryAdd(defaultTableName, rows);
                }
            }

            return tables;
        }

        private static string? DefaultTableName(DocumentType documentType)
        {
            return documentType switch
            {
                DocumentType.SalesOrder or DocumentType.SalesQuotation or DocumentType.SalesInvoice => "SalesLines",
                DocumentType.CreditNote => "CreditNoteLines",
                DocumentType.PurchaseOrder => "PurchaseLines",
                DocumentType.GRN => "ReceivedLines",
                DocumentType.SupplierBill => "SupplierBillLines",
                DocumentType.DebitNote => "DebitNoteLines",
                DocumentType.CustomerReceipt or DocumentType.SupplierPaymentRemittance => "PaymentAllocations",
                DocumentType.StockTransferDeliveryNote => "TransferLines",
                DocumentType.ProductionOrder or DocumentType.MaterialRequirementIssueSheet => "ProductionMaterials",
                DocumentType.MaterialReturn => "MaterialReturnLines",
                DocumentType.ProductionReceipt => "ProductionConsumptionLines",
                DocumentType.ProductionClosureReconciliation => "ProductionReconciliationLines",
                _ => null
            };
        }

        private async Task<MemoryStream?> TryLoadCompanyLogoAsync(string? logoBlobUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(logoBlobUrl))
            {
                return null;
            }

            try
            {
                var (logoStream, _) = await _storageCoordinator.RetrieveFileAsync(logoBlobUrl, cancellationToken);
                using (logoStream)
                {
                    var memoryStream = new MemoryStream();
                    await logoStream.CopyToAsync(memoryStream, cancellationToken);

                    if (memoryStream.Length == 0)
                    {
                        memoryStream.Dispose();
                        return null;
                    }

                    memoryStream.Position = 0;
                    return memoryStream;
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<byte[]> RenderFallbackPdfAsync(DocumentType documentType, PrintDocumentDto data, CancellationToken cancellationToken)
        {
            var profile = await _companyProfileService.GetProfileAsync(cancellationToken);

            using var pdfDocument = new PdfDocument();
            pdfDocument.PageSettings.Size = PdfPageSize.A4;
            pdfDocument.PageSettings.Margins.All = 36;
            var page = pdfDocument.Pages.Add();
            var graphics = page.Graphics;

            var companyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 15, PdfFontStyle.Bold);
            var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
            var headingFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9, PdfFontStyle.Bold);
            var regularFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f);
            var totalFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11, PdfFontStyle.Bold);
            var accent = new PdfSolidBrush(Color.FromArgb(31, 78, 121));
            var muted = new PdfSolidBrush(Color.FromArgb(90, 98, 108));
            var linePen = new PdfPen(Color.FromArgb(210, 214, 220), 0.75f);
            var pageWidth = page.GetClientSize().Width;
            float yPos = 0;

            if (!string.IsNullOrWhiteSpace(profile.LogoBlobUrl))
            {
                try
                {
                    var (logoStream, _) = await _storageCoordinator.RetrieveFileAsync(profile.LogoBlobUrl, cancellationToken);
                    using var ms = new MemoryStream();
                    await logoStream.CopyToAsync(ms, cancellationToken);
                    ms.Position = 0;
                    var pdfImage = new PdfBitmap(ms);
                    graphics.DrawImage(pdfImage, 0, yPos, 110, 38);
                }
                catch
                {
                    // A missing logo must not prevent legally important documents from being produced.
                }
            }

            var right = new PdfStringFormat { Alignment = PdfTextAlignment.Right };
            graphics.DrawString(profile.CompanyName ?? "Company Name", companyFont, accent, new PointF(120, 2));
            graphics.DrawString(profile.PrimaryAddress ?? string.Empty, regularFont, muted, new RectangleF(120, 22, pageWidth - 120, 28));
            graphics.DrawString(documentType.ToString().Replace("DeliveryNote", " Delivery Note").ToUpperInvariant(), titleFont, accent, new RectangleF(0, 52, pageWidth, 24), right);
            graphics.DrawString($"No: {data.DocumentNumber}\nDate: {data.DocumentDate:dd MMM yyyy}", regularFont, PdfBrushes.Black, new RectangleF(pageWidth - 200, 78, 200, 34), right);
            graphics.DrawLine(linePen, new PointF(0, 116), new PointF(pageWidth, 116));

            graphics.DrawString("BILL / DELIVER TO", headingFont, accent, new PointF(0, 128));
            graphics.DrawString(data.CustomerOrSupplierName ?? string.Empty, headingFont, PdfBrushes.Black, new PointF(0, 145));
            graphics.DrawString(data.BillingAddress ?? string.Empty, regularFont, muted, new RectangleF(0, 162, pageWidth * 0.48f, 46));
            if (!string.IsNullOrWhiteSpace(data.ShippingAddress))
            {
                graphics.DrawString("SHIPPING / LOCATION", headingFont, accent, new PointF(pageWidth * 0.55f, 128));
                graphics.DrawString(data.ShippingAddress, regularFont, muted, new RectangleF(pageWidth * 0.55f, 145, pageWidth * 0.45f, 58));
            }

            yPos = 218;
            var grid = new PdfGrid();
            grid.Columns.Add(7);
            grid.RepeatHeader = true;
            grid.Style.CellPadding = new PdfPaddings(4, 4, 4, 4);
            grid.Columns[0].Width = 65;
            grid.Columns[1].Width = pageWidth - 315;
            grid.Columns[2].Width = 48;
            grid.Columns[3].Width = 38;
            grid.Columns[4].Width = 64;
            grid.Columns[5].Width = 45;
            grid.Columns[6].Width = 55;

            var header = grid.Headers.Add(1)[0];
            var headings = new[] { "Code", "Description", "Qty", "Unit", "Unit Price", "Tax", "Total" };
            for (var i = 0; i < headings.Length; i++) header.Cells[i].Value = headings[i];
            header.ApplyStyle(new PdfGridCellStyle { BackgroundBrush = accent, TextBrush = PdfBrushes.White, Font = headingFont });
            for (var i = 2; i < grid.Columns.Count; i++)
                grid.Columns[i].Format = new PdfStringFormat { Alignment = PdfTextAlignment.Right, LineAlignment = PdfVerticalAlignment.Middle };

            var alternate = false;
            foreach (var item in data.LineItems)
            {
                var row = grid.Rows.Add();
                row.Style.Font = regularFont;
                if (alternate) row.Style.BackgroundBrush = new PdfSolidBrush(Color.FromArgb(246, 248, 250));
                alternate = !alternate;
                row.Cells[0].Value = item.ItemCode;
                row.Cells[1].Value = item.Description;
                row.Cells[2].Value = item.Quantity.ToString("N2");
                row.Cells[3].Value = item.Unit;
                row.Cells[4].Value = item.UnitPrice.ToString("N2");
                row.Cells[5].Value = item.TaxAmount.ToString("N2");
                row.Cells[6].Value = item.LineTotal.ToString("N2");
            }

            var gridResult = grid.Draw(page, new PointF(0, yPos), new PdfGridLayoutFormat { Layout = PdfLayoutType.Paginate });
            page = gridResult.Page;
            graphics = page.Graphics;
            yPos = gridResult.Bounds.Bottom + 16;
            if (yPos > page.GetClientSize().Height - 145)
            {
                page = pdfDocument.Pages.Add();
                graphics = page.Graphics;
                yPos = 20;
            }

            var labelX = page.GetClientSize().Width - 225;
            var valueX = page.GetClientSize().Width - 105;
            DrawTotal("Subtotal", data.SubTotal, false);
            if (data.DiscountTotal != 0) DrawTotal("Discount", -data.DiscountTotal, false);
            if (data.TaxTotal != 0) DrawTotal("Tax", data.TaxTotal, false);
            graphics.DrawLine(linePen, new PointF(labelX, yPos), new PointF(page.GetClientSize().Width, yPos));
            yPos += 7;
            DrawTotal("Grand Total", data.GrandTotal, true);

            if (!string.IsNullOrWhiteSpace(data.Notes))
            {
                yPos += 8;
                graphics.DrawString("NOTES", headingFont, accent, new PointF(0, yPos));
                yPos += 15;
                graphics.DrawString(data.Notes, regularFont, muted, new RectangleF(0, yPos, page.GetClientSize().Width * 0.62f, 55));
            }

            var generated = DateTime.UtcNow;
            for (var i = 0; i < pdfDocument.Pages.Count; i++)
            {
                var footerPage = pdfDocument.Pages[i];
                var footerY = footerPage.GetClientSize().Height - 12;
                footerPage.Graphics.DrawLine(linePen, new PointF(0, footerY - 6), new PointF(footerPage.GetClientSize().Width, footerY - 6));
                footerPage.Graphics.DrawString($"Generated {generated:yyyy-MM-dd HH:mm} UTC", regularFont, muted, new PointF(0, footerY));
                footerPage.Graphics.DrawString($"Page {i + 1} of {pdfDocument.Pages.Count}", regularFont, muted, new RectangleF(0, footerY, footerPage.GetClientSize().Width, 12), right);
            }

            void DrawTotal(string label, decimal amount, bool prominent)
            {
                var font = prominent ? totalFont : regularFont;
                graphics.DrawString(label, font, PdfBrushes.Black, new RectangleF(labelX, yPos, 115, 16), right);
                graphics.DrawString($"{data.CurrencyCode} {amount:N2}", font, prominent ? accent : PdfBrushes.Black, new RectangleF(valueX, yPos, 105, 16), right);
                yPos += prominent ? 22 : 17;
            }

            using var msOut = new MemoryStream();
            pdfDocument.Save(msOut);
            return msOut.ToArray();
        }
    }
}
