using NexusFlow.Domain.Common;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexusFlow.Domain.Entities.System
{
    [Table("GeneratedDocuments", Schema = "System")]
    public class GeneratedDocument : AuditableEntity
    {
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string OutputAction { get; set; } = string.Empty;
        public string BlobUrl { get; set; } = string.Empty;
        public string Sha256Hash { get; set; } = string.Empty;
        public string OverrideDifferencesJson { get; set; } = "{}";
        public DateTime GeneratedAtUtc { get; set; }
        public string GeneratedByUserId { get; set; } = string.Empty;
    }
}
