using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("Customers", Schema = "Sales")]
    public class Customer : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Odel", "Fashion Bug"
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        // Tax Registration (Important for VAT Invoices)
        public string TaxRegNo { get; set; } = string.Empty;

        // Credit Limit (Optional Validation)
        public decimal CreditLimit { get; set; }
    }
}
