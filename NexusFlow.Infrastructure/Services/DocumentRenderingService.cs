using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
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
                .FirstOrDefaultAsync(t => t.DocumentType == documentType && t.IsActive && t.IsDefault, cancellationToken);

            if (template != null && !string.IsNullOrWhiteSpace(template.BlobUrl))
            {
                return await RenderUsingWordTemplateAsync(template.BlobUrl, data, cancellationToken);
            }

            return await RenderFallbackPdfAsync(documentType, data, cancellationToken);
        }

        private async Task<byte[]> RenderUsingWordTemplateAsync(string blobUrl, PrintDocumentDto data, CancellationToken cancellationToken)
        {
            try
            {
                var (templateStream, _) = await _storageCoordinator.RetrieveFileAsync(blobUrl, cancellationToken);

                using var memoryStream = new MemoryStream();
                await templateStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                using var wordDocument = new WordDocument(memoryStream, FormatType.Docx);

                // Prepare MailMerge Data
                string[] fieldNames = new[] {
                    "DocumentNumber", "DocumentDate", "CustomerOrSupplierName",
                    "BillingAddress", "ShippingAddress", "Notes",
                    "SubTotal", "TaxTotal", "DiscountTotal", "GrandTotal", "CurrencyCode"
                };

                string[] fieldValues = new[] {
                    data.DocumentNumber, data.DocumentDate.ToString("yyyy-MM-dd"), data.CustomerOrSupplierName,
                    data.BillingAddress, data.ShippingAddress, data.Notes,
                    data.SubTotal.ToString("N2"), data.TaxTotal.ToString("N2"), data.DiscountTotal.ToString("N2"), data.GrandTotal.ToString("N2"), data.CurrencyCode
                };

                wordDocument.MailMerge.Execute(fieldNames, fieldValues);

                // Execute MailMerge for Line Items
                if (data.LineItems != null && data.LineItems.Count > 0)
                {
                    wordDocument.MailMerge.ExecuteGroup(new MailMergeDataTable("LineItems", data.LineItems));
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
                return await RenderFallbackPdfAsync(DocumentType.SalesInvoice, data, cancellationToken);
            }
        }

        private async Task<byte[]> RenderFallbackPdfAsync(DocumentType documentType, PrintDocumentDto data, CancellationToken cancellationToken)
        {
            var profile = await _companyProfileService.GetProfileAsync(cancellationToken);

            using var pdfDocument = new PdfDocument();
            var page = pdfDocument.Pages.Add();
            var graphics = page.Graphics;

            var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
            var regularFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

            float yPos = 20;

            if (!string.IsNullOrWhiteSpace(profile.LogoBlobUrl))
            {
                try
                {
                    var (logoStream, _) = await _storageCoordinator.RetrieveFileAsync(profile.LogoBlobUrl, cancellationToken);
                    using var ms = new MemoryStream();
                    await logoStream.CopyToAsync(ms, cancellationToken);
                    ms.Position = 0;
                    var pdfImage = new PdfBitmap(ms);
                    graphics.DrawImage(pdfImage, 10, yPos, 150, 50);
                    yPos += 70;
                }
                catch
                {
                    // Ignore logo errors in fallback
                }
            }

            graphics.DrawString(profile.CompanyName ?? "Company Name", titleFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPos));
            yPos += 30;

            graphics.DrawString($"{documentType} - {data.DocumentNumber}", titleFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPos));
            yPos += 40;

            graphics.DrawString($"Customer/Supplier: {data.CustomerOrSupplierName}", regularFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPos));
            yPos += 20;
            graphics.DrawString($"Date: {data.DocumentDate:yyyy-MM-dd}", regularFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPos));
            yPos += 40;

            graphics.DrawString("Line Items:", new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold), PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPos));
            yPos += 25;

            foreach (var item in data.LineItems)
            {
                graphics.DrawString($"{item.ItemCode} - {item.Description} (Qty: {item.Quantity} {item.Unit}) - {item.LineTotal:N2} {data.CurrencyCode}", regularFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPos));
                yPos += 20;
            }

            yPos += 20;
            graphics.DrawString($"Grand Total: {data.GrandTotal:N2} {data.CurrencyCode}", titleFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPos));

            using var msOut = new MemoryStream();
            pdfDocument.Save(msOut);
            return msOut.ToArray();
        }
    }
}
