using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    public class CreditNoteItem
    {
        public int Id { get; set; }
        public int CreditNoteId { get; set; }
        public CreditNote CreditNote { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public decimal ReturnedQuantity { get; set; }
        public decimal UnitPrice { get; set; } // Must match original invoice
        public decimal LineTotal { get; set; }

        // TIER-1: Preserving exact COGS for accurate inventory restoration
        public decimal RestoredCogsValue { get; set; }
    }
}
