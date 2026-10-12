using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NexusFlow.Domain.Common;

namespace NexusFlow.Domain.Entities.Finance;

[Table("CustomerMigrationBatches", Schema = "Finance")]
public sealed class CustomerMigrationBatch : AuditableEntity
{
    public DateTime CutoverDate { get; set; }
    public DateTime SourceAsOfDate { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "Draft";
    [MaxLength(64)] public string SourceHash { get; set; } = "";
    [MaxLength(64)] public string ReviewHash { get; set; } = "";
    [MaxLength(255)] public string OutstandingFileName { get; set; } = "";
    [MaxLength(255)] public string ChequeFileName { get; set; } = "";
    public byte[] OutstandingFile { get; set; } = [];
    public byte[] ChequeFile { get; set; } = [];
    public string SourceJson { get; set; } = "";
    public string ReviewJson { get; set; } = "";
    public int? JournalEntryId { get; set; }
    public int? ReversalJournalEntryId { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? ReversedDate { get; set; }
    public string? ReversalReason { get; set; }
    public bool IsSignedOff { get; set; }
    public DateTime? SignedOffAt { get; set; }
    public string? SignedOffBy { get; set; }
}
