using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("PaymentTransactions", Schema = "Finance")]
    public class PaymentTransaction : AuditableEntity
    {
        public string ReferenceNo { get; set; } = string.Empty; // e.g., "PAY-2024-001"
        public DateTime Date { get; set; }

        public PaymentType Type { get; set; } // Receipt (In) or Payment (Out)
        public PaymentMethod Method { get; set; } // Cash, Bank Transfer, Cheque

        public decimal Amount { get; set; }
        public string? Remarks { get; set; }

        // --- LINKING ---
        // Who is this transaction with?
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }

        // Optional: Link to specific document (Invoice ID or PO ID)
        // For advanced ERPs, we use a separate "PaymentAllocation" table, 
        // but for now, we will simply reference the Document Number string.
        public string? RelatedDocumentNo { get; set; }
    }
}
