using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Barcode;
using Syncfusion.Pdf.Graphics;

namespace NexusFlow.AppCore.Features.Barcodes
{
    public class PrintItem
    {
        public int ProductVariantId { get; set; }
        public int Quantity { get; set; }
    }

    public class GenerateBarcodePdfQuery : IRequest<Result<byte[]>>
    {
        public int TemplateId { get; set; }
        public List<PrintItem> Items { get; set; } = [];
    }

    public class GenerateBarcodePdfHandler : IRequestHandler<GenerateBarcodePdfQuery, Result<byte[]>>
    {
        public const int MaximumStickerCount = 5000;

        private readonly IErpDbContext _context;

        public GenerateBarcodePdfHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<byte[]>> Handle(GenerateBarcodePdfQuery request, CancellationToken cancellationToken)
        {
            if (request.Items == null || request.Items.Count == 0)
                return Result<byte[]>.Failure("Add at least one product variant to print.");

            if (request.Items.Any(x => x.ProductVariantId <= 0 || x.Quantity <= 0))
                return Result<byte[]>.Failure("Every print item must have a valid product variant and a quantity greater than zero.");

            if (request.Items.Any(x => x.Quantity > MaximumStickerCount))
                return Result<byte[]>.Failure($"A maximum of {MaximumStickerCount:N0} stickers can be generated at once.");

            var requestedItems = request.Items
                .GroupBy(x => x.ProductVariantId)
                .Select(x => new PrintItem { ProductVariantId = x.Key, Quantity = x.Sum(y => y.Quantity) })
                .ToList();

            int totalStickers;
            try
            {
                totalStickers = checked(requestedItems.Sum(x => x.Quantity));
            }
            catch (OverflowException)
            {
                return Result<byte[]>.Failure($"A maximum of {MaximumStickerCount:N0} stickers can be generated at once.");
            }

            if (totalStickers > MaximumStickerCount)
                return Result<byte[]>.Failure($"A maximum of {MaximumStickerCount:N0} stickers can be generated at once.");

            var template = await _context.BarcodeTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);
            if (template == null)
                return Result<byte[]>.Failure("Barcode template not found.");

            var templateErrors = BarcodeTemplateValidator.Validate(ToDto(template));
            if (templateErrors.Count > 0)
                return Result<byte[]>.Failure(templateErrors.ToArray());

            var variantIds = requestedItems.Select(x => x.ProductVariantId).ToList();
            var variants = await _context.ProductVariants
                .AsNoTracking()
                .Where(x => variantIds.Contains(x.Id) && x.IsActive)
                .ToListAsync(cancellationToken);

            var foundIds = variants.Select(x => x.Id).ToHashSet();
            var missingIds = variantIds.Where(x => !foundIds.Contains(x)).ToArray();
            if (missingIds.Length > 0)
                return Result<byte[]>.Failure($"The following product variants are missing or inactive: {string.Join(", ", missingIds)}.");

            var payloads = variants.ToDictionary(x => x.Id, ResolvePayload);
            var invalidSkus = variants
                .Where(x => !IsValidPayload(payloads[x.Id], template.Symbology))
                .Select(x => x.SKU)
                .OrderBy(x => x)
                .ToArray();
            if (invalidSkus.Length > 0)
                return Result<byte[]>.Failure($"The following SKUs do not contain valid {template.Symbology} barcode payloads: {string.Join(", ", invalidSkus)}.");

            var configValues = await _context.SystemConfigs
                .AsNoTracking()
                .Where(x => x.Key == ConfigurationKeys.CompanyName || x.Key == ConfigurationKeys.FinanceBaseCurrency)
                .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);
            string companyName = configValues.GetValueOrDefault(ConfigurationKeys.CompanyName, "NexusFlow Enterprise");
            string currency = configValues.GetValueOrDefault(ConfigurationKeys.FinanceBaseCurrency, "LKR");

            var variantsById = variants.ToDictionary(x => x.Id);
            var stickers = requestedItems
                .SelectMany(item => Enumerable.Repeat(variantsById[item.ProductVariantId], item.Quantity))
                .ToList();

            try
            {
                return Result<byte[]>.Success(CreatePdf(template, stickers, payloads, companyName, currency));
            }
            catch (Exception ex)
            {
                return Result<byte[]>.Failure($"Barcode PDF generation failed: {ex.Message}");
            }
        }

        public static string ResolvePayload(ProductVariant variant)
            => string.IsNullOrWhiteSpace(variant.Barcode) ? variant.SKU.Trim() : variant.Barcode.Trim();

        public static bool IsValidPayload(string payload, BarcodeSymbology symbology)
        {
            return symbology switch
            {
                BarcodeSymbology.Code128 => payload.Length > 0 && payload.All(x => x is >= ' ' and <= '~'),
                BarcodeSymbology.EAN13 => IsValidNumericChecksum(payload, 13),
                BarcodeSymbology.UPC => IsValidNumericChecksum(payload, 12),
                _ => false
            };
        }

        private static bool IsValidNumericChecksum(string payload, int requiredLength)
        {
            if (payload.Length != requiredLength || payload.Any(x => !char.IsAsciiDigit(x)))
                return false;

            int sum = 0;
            for (int i = 0; i < payload.Length - 1; i++)
            {
                int digit = payload[i] - '0';
                bool multiplyByThree = requiredLength == 12 ? i % 2 == 0 : i % 2 != 0;
                sum += multiplyByThree ? digit * 3 : digit;
            }

            int expectedCheckDigit = (10 - (sum % 10)) % 10;
            return payload[^1] - '0' == expectedCheckDigit;
        }

        private static byte[] CreatePdf(
            BarcodeTemplate template,
            IReadOnlyList<ProductVariant> stickers,
            IReadOnlyDictionary<int, string> payloads,
            string companyName,
            string currency)
        {
            var convertor = new PdfUnitConvertor();
            float Points(decimal millimeters) => convertor.ConvertUnits((float)millimeters, PdfGraphicsUnit.Millimeter, PdfGraphicsUnit.Point);

            float pageWidth = Points(template.PageWidthMM);
            float pageHeight = Points(template.PageHeightMM);
            float stickerWidth = Points(template.StickerWidthMM);
            float stickerHeight = Points(template.StickerHeightMM);
            float marginLeft = Points(template.MarginLeftMM);
            float marginTop = Points(template.MarginTopMM);
            float horizontalGap = Points(template.HorizontalGapMM);
            float verticalGap = Points(template.VerticalGapMM);

            using PdfDocument document = new();
            document.PageSettings.Size = new SizeF(pageWidth, pageHeight);
            document.PageSettings.Margins.All = 0;

            int stickerIndex = 0;
            while (stickerIndex < stickers.Count)
            {
                PdfPage page = document.Pages.Add();
                for (int row = 0; row < template.RowsPerPage && stickerIndex < stickers.Count; row++)
                {
                    for (int column = 0; column < template.StickersPerRow && stickerIndex < stickers.Count; column++)
                    {
                        float x = marginLeft + column * (stickerWidth + horizontalGap);
                        float y = marginTop + row * (stickerHeight + verticalGap);
                        ProductVariant variant = stickers[stickerIndex++];

                        DrawSticker(page.Graphics, new RectangleF(x, y, stickerWidth, stickerHeight), template, variant, payloads[variant.Id], companyName, currency);
                    }
                }
            }

            using MemoryStream stream = new();
            document.Save(stream);
            document.Close(true);
            return stream.ToArray();
        }

        private static void DrawSticker(
            PdfGraphics graphics,
            RectangleF bounds,
            BarcodeTemplate template,
            ProductVariant variant,
            string payload,
            string companyName,
            string currency)
        {
            const float padding = 2f;
            PdfFont smallFont = new PdfStandardFont(PdfFontFamily.Helvetica, 6);
            PdfFont smallBoldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 6, PdfFontStyle.Bold);
            PdfFont regularBoldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Bold);
            PdfStringFormat centered = new() { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle };

            float left = bounds.X + padding;
            float width = bounds.Width - (padding * 2);
            float cursorTop = bounds.Y + padding;
            float cursorBottom = bounds.Bottom - padding;

            void DrawTopLine(string text, PdfFont font, float lineHeight)
            {
                string fitted = FitText(text, font, width);
                graphics.DrawString(fitted, font, PdfBrushes.Black, new RectangleF(left, cursorTop, width, lineHeight), centered);
                cursorTop += lineHeight;
            }

            if (template.PrintCompanyName)
                DrawTopLine(companyName, smallBoldFont, 8f);
            if (template.PrintProductName)
                DrawTopLine(variant.Name, regularBoldFont, 10f);
            if (template.PrintSizeColor)
            {
                string sizeColor = string.Join(" / ", new[] { variant.Size, variant.Color }
                    .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("N/A", StringComparison.OrdinalIgnoreCase)));
                if (!string.IsNullOrWhiteSpace(sizeColor))
                    DrawTopLine(sizeColor, smallFont, 8f);
            }
            if (template.PrintPrice)
            {
                const float priceHeight = 10f;
                cursorBottom -= priceHeight;
                string price = FitText($"{currency} {variant.SellingPrice:N2}", regularBoldFont, width);
                graphics.DrawString(price, regularBoldFont, PdfBrushes.Black, new RectangleF(left, cursorBottom, width, priceHeight), centered);
            }

            RectangleF barcodeBounds = new(left, cursorTop, width, Math.Max(1f, cursorBottom - cursorTop));
            PdfUnidimensionalBarcode barcode = template.Symbology switch
            {
                BarcodeSymbology.EAN13 => new PdfEan13Barcode(payload),
                BarcodeSymbology.UPC => new PdfCodeUpcBarcode(payload),
                _ => new PdfCode128BBarcode(payload)
            };
            barcode.Font = smallFont;
            barcode.TextDisplayLocation = TextLocation.Bottom;
            barcode.TextAlignment = PdfBarcodeTextAlignment.Center;
            barcode.Draw(graphics, barcodeBounds);
        }

        private static string FitText(string value, PdfFont font, float maximumWidth)
        {
            string text = value?.Trim() ?? string.Empty;
            if (font.MeasureString(text).Width <= maximumWidth)
                return text;

            const string ellipsis = "...";
            while (text.Length > 0 && font.MeasureString(text + ellipsis).Width > maximumWidth)
                text = text[..^1];

            return text.Length == 0 ? ellipsis : text + ellipsis;
        }

        private static BarcodeTemplateDto ToDto(BarcodeTemplate template)
        {
            return new BarcodeTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                PageWidthMM = template.PageWidthMM,
                PageHeightMM = template.PageHeightMM,
                StickerWidthMM = template.StickerWidthMM,
                StickerHeightMM = template.StickerHeightMM,
                StickersPerRow = template.StickersPerRow,
                RowsPerPage = template.RowsPerPage,
                MarginTopMM = template.MarginTopMM,
                MarginLeftMM = template.MarginLeftMM,
                HorizontalGapMM = template.HorizontalGapMM,
                VerticalGapMM = template.VerticalGapMM,
                Symbology = template.Symbology,
                PrintCompanyName = template.PrintCompanyName,
                PrintProductName = template.PrintProductName,
                PrintPrice = template.PrintPrice,
                PrintSizeColor = template.PrintSizeColor,
                IsDefault = template.IsDefault
            };
        }
    }
}
