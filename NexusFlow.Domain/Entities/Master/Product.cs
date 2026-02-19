using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("Products", Schema = "Master")]
    public class Product : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Slim Fit Oxford Shirt"
        public string Description { get; set; } = string.Empty;

        // Foreign Keys
        public int BrandId { get; set; }
        public Brand Brand { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }

        public int UnitOfMeasureId { get; set; }
        public UnitOfMeasure UnitOfMeasure { get; set; }
        public ProductType Type { get; set; }

        public int SalesAccountId { get; set; }
        [ForeignKey("SalesAccountId")]
        public Account SalesAccount { get; set; }

        // 2. Where do we book the Asset value? (e.g. "1200 - Inventory Asset")
        // Nullable because 'Services' don't have stock value.
        public int? InventoryAccountId { get; set; }
        [ForeignKey("InventoryAccountId")]
        public Account? InventoryAccount { get; set; }

        // 3. Where do we book the Cost? (e.g. "5000 - Cost of Goods Sold")
        public int CogsAccountId { get; set; }
        [ForeignKey("CogsAccountId")]
        public Account CogsAccount { get; set; }

        // Navigation
        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }
}
