using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("Warehouses", Schema = "Master")]
    public class Warehouse : AuditableEntity
    {
        public string Code { get; set; } = string.Empty; // e.g., WH-MAIN or WH-GARMENT1
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;

        // NEW: Categorize the warehouse
        public WarehouseType Type { get; set; } = WarehouseType.Internal;

        // NEW: If Type == Subcontractor, link to the actual Vendor profile for AP Billing
        public int? LinkedSupplierId { get; set; }

        [ForeignKey("LinkedSupplierId")]
        public Supplier? LinkedSupplier { get; set; }

        // Financial Mapping
        public int? OverrideInventoryAccountId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
