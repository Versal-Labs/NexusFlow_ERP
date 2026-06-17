using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;

namespace NexusFlow.Domain.Entities.System
{
    public class DocumentTemplate : AuditableEntity
    {
        public DocumentType DocumentType { get; set; }
        public string TemplateName { get; set; } = null!;
        public TaxProfile TaxProfile { get; set; }
        public string BlobUrl { get; set; } = null!;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
    }
}
