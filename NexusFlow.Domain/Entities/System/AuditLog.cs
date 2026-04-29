using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.System
{
    [Table("AuditLogs", Schema = "System")]
    public class AuditLog : BaseEntity
    {
        public string Action { get; set; } = string.Empty;       // e.g., "STORAGE_FAILURE_FALLBACK", "LOGIN_FAILED", "STOCK_TAKE_APPROVED"
        public string EntityName { get; set; } = string.Empty;   // e.g., "Product-Images", "StockTake"
        public string UserId { get; set; } = "SYSTEM";           // The user who triggered the event
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Details { get; set; } = string.Empty;      // Human-readable details or specific error messages
        public string? IPAddress { get; set; }                   // Optional security tracking
    }
}
