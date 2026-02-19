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
        public string? BrandName { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int UnitOfMeasureId { get; set; }
        public string? UnitName { get; set; }
        public ProductType Type { get; set; }

        // --- Financials ---
        public int SalesAccountId { get; set; }
        public int? InventoryAccountId { get; set; }
        public string? CogsAccountName { get; set; }
        public int CogsAccountId { get; set; }

        // Flattened names for Grid display
        public string SalesAccountName { get; set; } = string.Empty;
        public string InventoryAccountName { get; set; } = string.Empty;

        public List<ProductVariantDto> Variants { get; set; } = new();
    }
}
