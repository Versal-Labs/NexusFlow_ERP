using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int BrandId { get; set; }
        public int CategoryId { get; set; }
        public int UnitOfMeasureId { get; set; }

        public ProductType Type { get; set; } // RawMaterial vs FinishedGood

        public List<ProductVariantDto> Variants { get; set; } = new();
    }
}
