using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Features.Barcodes
{
    public class BarcodeTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PageWidthMM { get; set; }
        public decimal PageHeightMM { get; set; }
        public decimal StickerWidthMM { get; set; }
        public decimal StickerHeightMM { get; set; }
        public int StickersPerRow { get; set; }
        public int RowsPerPage { get; set; }
        public decimal MarginTopMM { get; set; }
        public decimal MarginLeftMM { get; set; }
        public decimal HorizontalGapMM { get; set; }
        public decimal VerticalGapMM { get; set; }
        public BarcodeSymbology Symbology { get; set; }
        public bool PrintCompanyName { get; set; }
        public bool PrintProductName { get; set; }
        public bool PrintPrice { get; set; }
        public bool PrintSizeColor { get; set; }
        public bool IsDefault { get; set; }
    }

    public class BarcodeVariantSearchDto
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
