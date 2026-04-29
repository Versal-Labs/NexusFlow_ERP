using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Purchasing
{
    public class PurchaseOrderDto
    {
        public int Id { get; set; }
        public string PoNumber { get; set; } = string.Empty; // Auto-generated
        public DateTime Date { get; set; }
        public DateTime ExpectedDate { get; set; }

        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public string Status { get; set; } = "Draft"; // Enum as String
        public decimal TotalAmount { get; set; }
        public string Note { get; set; } = string.Empty;

        public List<PurchaseOrderItemDto> Items { get; set; } = new();
    }

    public class PurchaseOrderItemDto
    {
        public int Id { get; set; }
        public int ProductVariantId { get; set; }
        public string ProductName { get; set; } = string.Empty; // "Shirt (Red/L)"
        public string SKU { get; set; } = string.Empty;
        public string UomSymbol { get; set; } = string.Empty;

        public decimal QuantityOrdered { get; set; }
        public decimal QuantityReceived { get; set; } // Track progress
        public decimal UnitCost { get; set; }
        public decimal LineTotal => QuantityOrdered * UnitCost;
    }
}
