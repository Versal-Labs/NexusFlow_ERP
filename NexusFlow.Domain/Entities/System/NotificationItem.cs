using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.System
{
    [Table("Notifications", Schema = "System")]
    public class NotificationItem : AuditableEntity
    {
        public string UserId { get; set; } = string.Empty; // Who is this for?
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty; // Clickable link (e.g. to a specific PO)
        public bool IsRead { get; set; } = false;
        public string Type { get; set; } = "Info"; // Info, Success, Warning, Error
    }
}
