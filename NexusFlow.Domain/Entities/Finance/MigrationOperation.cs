using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NexusFlow.Domain.Common;

namespace NexusFlow.Domain.Entities.Finance;

// Progress is deliberately non-temporal. Large, immutable data lives in separate tables.
[Table("MigrationOperations", Schema = "Migration")]
public sealed class MigrationOperation : AuditableEntity
{
    public int ProjectId { get; set; }
    public Guid ClientRequestId { get; set; }
    [MaxLength(24)] public string Kind { get; set; } = "";
    [MaxLength(24)] public string Status { get; set; } = "Queued";
    [MaxLength(450)] public string InitiatedBy { get; set; } = "";
    [MaxLength(256)] public string InitiatedByName { get; set; } = "";
    [MaxLength(64)] public string InputFingerprint { get; set; } = "";
    public int InputVersion { get; set; }
    [MaxLength(100)] public string? JobId { get; set; }
    public int? RunId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    [MaxLength(160)] public string Phase { get; set; } = "Waiting for a worker";
    [MaxLength(200)] public string? CurrentItem { get; set; }
    public int? Total { get; set; }
    public int Processed { get; set; }
    public int Succeeded { get; set; }
    public int NeedsAttention { get; set; }
    [MaxLength(4000)] public string? Error { get; set; }
    [MaxLength(64)] public string? ResultHash { get; set; }
    public bool CanApprove { get; set; }
}

[Table("MigrationOperationPayloads", Schema = "Migration")]
public sealed class MigrationOperationPayload : BaseEntity
{
    public int OperationId { get; set; }
    public string RequestJson { get; set; } = "{}";
}

[Table("MigrationOperationItems", Schema = "Migration")]
public sealed class MigrationOperationItem : BaseEntity
{
    public int OperationId { get; set; }
    public int Ordinal { get; set; }
    [MaxLength(40)] public string Module { get; set; } = "";
    [MaxLength(24)] public string Kind { get; set; } = "";
    [MaxLength(160)] public string Code { get; set; } = "";
    public int? ScopeId { get; set; }
    [MaxLength(24)] public string Status { get; set; } = "Pending";
    [MaxLength(4000)] public string? Message { get; set; }
    public int Rows { get; set; }
    public DateTime? CompletedAt { get; set; }
}

[Table("MigrationOperationItemPayloads", Schema = "Migration")]
public sealed class MigrationOperationItemPayload : BaseEntity
{
    public int ItemId { get; set; }
    public string DetailJson { get; set; } = "{}";
}

[Table("MigrationOperationIssues", Schema = "Migration")]
public sealed class MigrationOperationIssue : BaseEntity
{
    public int OperationId { get; set; }
    public int? ItemId { get; set; }
    [MaxLength(12)] public string Severity { get; set; } = "Error";
    [MaxLength(40)] public string Module { get; set; } = "";
    [MaxLength(160)] public string Code { get; set; } = "";
    public int Row { get; set; }
    public int AffectedRows { get; set; } = 1;
    [MaxLength(2000)] public string Message { get; set; } = "";
}

[Table("MigrationRunPayloads", Schema = "Migration")]
public sealed class MigrationRunData : BaseEntity
{
    public int RunId { get; set; }
    public string PayloadJson { get; set; } = "{}";
}
