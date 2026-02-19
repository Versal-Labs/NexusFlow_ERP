using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Config
{
    [Table("SystemLookups", Schema = "Config")]
    public class SystemLookup : AuditableEntity
    {
        // Discriminator: "PaymentMethod", "ExpenseCategory", "TicketPriority"
        public string Type { get; set; } = string.Empty;

        // Internal Code: "CARD", "CASH" (Used by code/logic)
        public string Code { get; set; } = string.Empty;

        // Display Value: "Credit Card", "Cash on Hand" (Shown to User)
        public string Value { get; set; } = string.Empty;

        // Ordering for Dropdowns
        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
