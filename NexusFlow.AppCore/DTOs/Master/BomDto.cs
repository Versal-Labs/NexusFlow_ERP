using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class BomDto
    {
        public int Id { get; set; }
        public int ProductVariantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<BomComponentDto> Components { get; set; } = new();
    }

    public class BomComponentDto
    {
        public int Id { get; set; }
        public int MaterialVariantId { get; set; }
        public decimal Quantity { get; set; }
    }
}
