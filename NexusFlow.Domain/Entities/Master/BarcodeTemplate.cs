using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("BarcodeTemplates", Schema = "Master")]
    public class BarcodeTemplate : AuditableEntity
    {
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

        public BarcodeSymbology Symbology { get; set; } = BarcodeSymbology.Code128;

        public bool PrintCompanyName { get; set; }
        public bool PrintProductName { get; set; } = true;
        public bool PrintPrice { get; set; }
        public bool PrintSizeColor { get; set; }
        public bool IsDefault { get; set; }
    }
}
