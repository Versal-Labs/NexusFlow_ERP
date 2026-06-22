using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
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
        public int RevisionNumber { get; set; } = 1;
        public decimal BasisQuantity { get; set; } = 1m;
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
        public DateTime? EffectiveTo { get; set; }
        public bool IsApproved { get; set; } = true;
        public DateTime? ApprovedAtUtc { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // The Ingredients
        public ICollection<BomComponent> Components { get; set; } = new List<BomComponent>();
    }
}
