using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("BillOfMaterials", Schema = "Master")]
    public class BillOfMaterial : AuditableEntity
    {
        // The "Result" Product (e.g., Jean - Red - 32)
        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public string Name { get; set; } = string.Empty; // e.g., "Standard Production Recipe"
        public bool IsActive { get; set; } = true;

        // The Ingredients
        public ICollection<BomComponent> Components { get; set; } = new List<BomComponent>();
    }
}
