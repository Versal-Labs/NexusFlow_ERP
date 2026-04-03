using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Inventory
{
    public class ProductImportDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Temporary ID for UI tracking
        public string LotNo { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public decimal AverageCost { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal MinSellingPrice { get; set; }
        public decimal TotalQuantity { get; set; }
    }
}
