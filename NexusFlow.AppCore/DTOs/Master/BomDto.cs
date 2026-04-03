using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class BomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ProductVariantId { get; set; } // The target Finished Good
        public string? ProductVariantName { get; set; }
        public bool IsActive { get; set; } = true;
        public List<BomComponentDto> Components { get; set; } = new();
    }

    public class BomComponentDto
    {
        public int Id { get; set; }
        public int MaterialVariantId { get; set; } // The Raw Material
        public string? MaterialVariantName { get; set; }
        public decimal Quantity { get; set; }
    }

    public class BomListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProductVariantName { get; set; } = string.Empty;
        public int ComponentCount { get; set; }
        public bool IsActive { get; set; }
    }
}
