using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("GRNItems", Schema = "Purchasing")]
    public class GRNItem : AuditableEntity
    {
        public int GrnId { get; set; }
        public GRN Grn { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public decimal QuantityReceived { get; set; }

        // The cost *at the moment of receipt* (usually same as PO, but can vary with exchange rates)
        public decimal UnitCost { get; set; }

        public decimal LineTotal { get; set; }
    }
}
