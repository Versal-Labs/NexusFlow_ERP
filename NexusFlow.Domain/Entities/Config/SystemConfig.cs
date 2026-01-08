using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Entities.Config
{
    public class SystemConfig : AuditableEntity
    {
        // The "Key" will be the primary identifier for lookups (e.g., "Tax.VAT")
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty; // e.g., "15"

        // Helps the UI know how to render the input (e.g., "Decimal", "Boolean", "String")
        public string DataType { get; set; } = "String";

        public string Description { get; set; } = string.Empty; // For Admin UI tooltips
    }
}
