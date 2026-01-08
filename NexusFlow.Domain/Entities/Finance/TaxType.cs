using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("TaxTypes", Schema = "Finance")]
    public class TaxType : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "VAT"
        public string Description { get; set; } = string.Empty;

        // Links to GL Account (Where do we post the liability?)
        // e.g., "VAT Payable" (Liability Account)
        public int AccountId { get; set; }
        public Account Account { get; set; }

        public ICollection<TaxRate> Rates { get; set; } = new List<TaxRate>();
    }
}
