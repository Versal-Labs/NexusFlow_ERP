using System.ComponentModel.DataAnnotations.Schema;
using NexusFlow.Domain.Common;

namespace NexusFlow.Domain.Entities.System
{
    [Table("InstallationRecords", Schema = "System")]
    public class InstallationRecord : AuditableEntity
    {
        public string InstanceId { get; set; } = string.Empty;
        public string ProductVersion { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string TemplateVersion { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? CompletedAtUtc { get; set; }
    }

    [Table("AppliedInstallationSteps", Schema = "System")]
    public class AppliedInstallationStep : AuditableEntity
    {
        public string StepKey { get; set; } = string.Empty;
        public string StepVersion { get; set; } = string.Empty;
        public DateTimeOffset AppliedAtUtc { get; set; }
    }
}
