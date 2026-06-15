namespace NexusFlow.AppCore.Features.Barcodes
{
    public static class BarcodeTemplateValidator
    {
        private const decimal MinimumBarcodeAreaHeightMM = 10m;
        private const decimal MinimumBarcodeAreaWidthMM = 10m;
        private const decimal MaximumMeasurementMM = 999999.999m;
        private const decimal VerticalPaddingMM = 2m;
        private const decimal OptionalTextLineHeightMM = 3m;

        public static IReadOnlyList<string> Validate(BarcodeTemplateDto template)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(template.Name))
                errors.Add("Template name is required.");
            else if (template.Name.Trim().Length > 150)
                errors.Add("Template name cannot exceed 150 characters.");

            if (template.PageWidthMM <= 0 || template.PageHeightMM <= 0)
                errors.Add("Page width and height must be greater than zero.");

            if (template.StickerWidthMM <= 0 || template.StickerHeightMM <= 0)
                errors.Add("Sticker width and height must be greater than zero.");
            else if (template.StickerWidthMM < MinimumBarcodeAreaWidthMM)
                errors.Add($"Sticker width must be at least {MinimumBarcodeAreaWidthMM:0.###} mm.");

            if (template.StickersPerRow <= 0 || template.RowsPerPage <= 0)
                errors.Add("Stickers per row and rows per page must be greater than zero.");

            if (template.MarginTopMM < 0 || template.MarginLeftMM < 0 ||
                template.HorizontalGapMM < 0 || template.VerticalGapMM < 0)
                errors.Add("Margins and gaps cannot be negative.");

            decimal[] measurements =
            [
                template.PageWidthMM, template.PageHeightMM,
                template.StickerWidthMM, template.StickerHeightMM,
                template.MarginTopMM, template.MarginLeftMM,
                template.HorizontalGapMM, template.VerticalGapMM
            ];
            if (measurements.Any(x => x > MaximumMeasurementMM))
                errors.Add($"Measurements cannot exceed {MaximumMeasurementMM:0.###} mm.");

            decimal requiredWidth = template.MarginLeftMM
                + (template.StickersPerRow * template.StickerWidthMM)
                + ((template.StickersPerRow - 1) * template.HorizontalGapMM);
            decimal requiredHeight = template.MarginTopMM
                + (template.RowsPerPage * template.StickerHeightMM)
                + ((template.RowsPerPage - 1) * template.VerticalGapMM);

            if (requiredWidth > template.PageWidthMM)
                errors.Add($"The configured sticker columns require {requiredWidth:0.###} mm, exceeding the page width of {template.PageWidthMM:0.###} mm.");

            if (requiredHeight > template.PageHeightMM)
                errors.Add($"The configured sticker rows require {requiredHeight:0.###} mm, exceeding the page height of {template.PageHeightMM:0.###} mm.");

            int optionalLines = Convert.ToInt32(template.PrintCompanyName)
                + Convert.ToInt32(template.PrintProductName)
                + Convert.ToInt32(template.PrintPrice)
                + Convert.ToInt32(template.PrintSizeColor);
            decimal minimumHeight = VerticalPaddingMM + MinimumBarcodeAreaHeightMM + (optionalLines * OptionalTextLineHeightMM);

            if (template.StickerHeightMM < minimumHeight)
                errors.Add($"Sticker height must be at least {minimumHeight:0.###} mm for the selected print fields.");

            if (!Enum.IsDefined(template.Symbology))
                errors.Add("A valid barcode symbology is required.");

            return errors;
        }
    }
}
