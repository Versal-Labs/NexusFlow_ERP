using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("Categories", Schema = "Master")]
    public class Category : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // e.g., "MENS-SHIRT"

        // Hierarchy Support (For CSV 'type' and 'type1')
        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public ICollection<Category> SubCategories { get; set; } = new List<Category>();

        // ==========================================================
        // ACCOUNTING POSTING GROUPS (Moved from Product)
        // ==========================================================
        public int? SalesAccountId { get; set; }
        public Account? SalesAccount { get; set; }

        public int? InventoryAccountId { get; set; }
        public Account? InventoryAccount { get; set; }

        public int? CogsAccountId { get; set; }
        public Account? CogsAccount { get; set; }
    }
}
