using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("Suppliers", Schema = "Purchasing")]
    public class Supplier : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Pacific Denim Ltd"
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TaxRegNo { get; set; } = string.Empty; // Supplier's VAT number
        public string Address { get; set; } = string.Empty;

        // The GL Account where we record what we owe them (AP)
        public int? DefaultPayableAccountId { get; set; }
        public int DefaultCreditPeriodDays { get; set; } = 30;
    }
}
