using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Common
{
    public abstract class AuditableEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public string? LastModifiedBy { get; set; }
    }
}
