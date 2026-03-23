using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }
        public string? ParentCategoryName { get; set; }

        // ==========================================
        // ENTERPRISE POSTING GROUP CONFIGURATION
        // ==========================================
        public int? SalesAccountId { get; set; }
        public string? SalesAccountName { get; set; }

        public int? InventoryAccountId { get; set; }
        public string? InventoryAccountName { get; set; }

        public int? CogsAccountId { get; set; }
        public string? CogsAccountName { get; set; }
    }
}
