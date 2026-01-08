using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("TaxRates", Schema = "Finance")]
    public class TaxRate : AuditableEntity
    {
        public int TaxTypeId { get; set; }
        public TaxType TaxType { get; set; }

        public decimal Rate { get; set; } // e.g., 18.00

        // Validation: StartDate is required. EndDate is null until changed.
        public DateTime EffectiveDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
