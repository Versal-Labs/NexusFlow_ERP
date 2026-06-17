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
