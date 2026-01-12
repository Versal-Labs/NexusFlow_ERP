using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.System
{
    [Table("AuditLogs", Schema = "System")]
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty; // e.g., "admin@nexusflow.com"
        public string Type { get; set; } = string.Empty;   // "Create", "Update", "Delete"
        public string TableName { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }

        public string PrimaryKey { get; set; } = string.Empty; // The ID of the row changed

        // We store the data as JSON strings
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? AffectedColumns { get; set; }
    }
}
