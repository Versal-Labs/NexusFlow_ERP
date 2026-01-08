using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Config
{
    [Table("NumberSequences", Schema = "Config")]
    public class NumberSequence : AuditableEntity
    {
        // E.g., "Sales", "Inventory", "Finance"
        public string Module { get; set; } = string.Empty;

        // E.g., "INV" or "PO"
        public string Prefix { get; set; } = string.Empty;

        // The auto-incrementing part. e.g., 1001
        public int NextNumber { get; set; }

        // E.g., "-" or "/"
        public string Delimiter { get; set; } = "-";

        // Optional: E.g., "/2024"
        public string? Suffix { get; set; }

        // Helper method to preview the next ID (e.g., "INV-1001")
        public string PreviewNext()
        {
            return $"{Prefix}{Delimiter}{NextNumber}{Suffix}";
        }
    }
}
