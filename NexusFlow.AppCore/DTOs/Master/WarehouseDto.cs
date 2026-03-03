using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class WarehouseDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;

        public WarehouseType Type { get; set; }
        public int? LinkedSupplierId { get; set; }
        public string LinkedSupplierName { get; set; } = string.Empty; // For the UI Grid

        public int? OverrideInventoryAccountId { get; set; }
        public bool IsActive { get; set; }
    }
}
