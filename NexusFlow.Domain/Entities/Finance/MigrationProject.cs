using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NexusFlow.Domain.Common;

namespace NexusFlow.Domain.Entities.Finance;

[Table("MigrationProjects", Schema = "Migration")]
public sealed class MigrationProject : AuditableEntity
{
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(80)] public string SourceSystem { get; set; } = "";
    public DateTime OpeningDate { get; set; }
    public DateTime ClosingDate { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "LKR";
    [MaxLength(24)] public string Status { get; set; } = "Preparing";
    public string SettingsJson { get; set; } = "{}";
    public int Version { get; set; }
    [MaxLength(64)] public string? ApprovedHash { get; set; }
    [MaxLength(450)] public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public string? FinalEvidence { get; set; }
}

[Table("MigrationDatasets", Schema = "Migration")]
public sealed class MigrationDataset : AuditableEntity
{
    public int ProjectId { get; set; }
    [MaxLength(40)] public string Module { get; set; } = "";
    public int Revision { get; set; }
    public bool IsCurrent { get; set; } = true;
    [MaxLength(255)] public string FileName { get; set; } = "";
    [MaxLength(64)] public string FileHash { get; set; } = "";
    public string StorageReference { get; set; } = "";
    public DateTime ClosingDate { get; set; }
    public string RowsJson { get; set; } = "[]";
    public string ErrorsJson { get; set; } = "[]";
}

[Table("MigrationScopes", Schema = "Migration")]
public sealed class MigrationScope : AuditableEntity
{
    public int ProjectId { get; set; }
    [MaxLength(24)] public string Kind { get; set; } = "";
    [MaxLength(100)] public string Code { get; set; } = "";
    public int? EntityId { get; set; }
    [MaxLength(24)] public string Status { get; set; } = "Pending";
    public string RequiredModulesJson { get; set; } = "[]";
    public string Evidence { get; set; } = "";
    public DateTime? ReleasedAt { get; set; }
}

[Table("MigrationUnits", Schema = "Migration")]
public sealed class MigrationUnit : AuditableEntity
{
    public int ProjectId { get; set; }
    [MaxLength(40)] public string Module { get; set; } = "";
    [MaxLength(160)] public string ScopeKey { get; set; } = "";
    [MaxLength(64)] public string Hash { get; set; } = "";
    [MaxLength(24)] public string Status { get; set; } = "Posted";
    public int? JournalEntryId { get; set; }
    public int? ReversalJournalEntryId { get; set; }
    public DateTime? ReversedDate { get; set; }
    public string RowsJson { get; set; } = "[]";
}

[Table("MigrationOrigins", Schema = "Migration")]
public sealed class MigrationOrigin : AuditableEntity
{
    public bool IsCurrent { get; set; } = true;
    public int ProjectId { get; set; }
    public int UnitId { get; set; }
    [MaxLength(80)] public string SourceSystem { get; set; } = "";
    [MaxLength(40)] public string RecordKind { get; set; } = "";
    [MaxLength(64)] public string IdentityHash { get; set; } = "";
    [MaxLength(64)] public string ContentHash { get; set; } = "";
    [MaxLength(300)] public string LegacyRecordId { get; set; } = "";
    [MaxLength(60)] public string EntityKind { get; set; } = "";
    public int EntityId { get; set; }
}

[Table("MigrationRuns", Schema = "Migration")]
public sealed class MigrationRun : AuditableEntity
{
    public int ProjectId { get; set; }
    [MaxLength(64)] public string ApprovedHash { get; set; } = "";
    [MaxLength(24)] public string Status { get; set; } = "Queued";
    public string SelectionJson { get; set; } = "{}";
    [MaxLength(100)] public string? JobId { get; set; }
    public int CommittedUnits { get; set; }
    public string? Error { get; set; }
    public DateTime? CompletedAt { get; set; }
}

[Table("MigrationAudit", Schema = "Migration")]
public sealed class MigrationAudit : AuditableEntity
{
    public int ProjectId { get; set; }
    [MaxLength(40)] public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
}

[Table("SupplierOpeningItems", Schema = "Purchasing")]
public sealed class SupplierOpeningItem : AuditableEntity
{
    public int MigrationUnitId { get; set; }
    public int SupplierId { get; set; }
    [MaxLength(100)] public string ReferenceNo { get; set; } = "";
    [MaxLength(40)] public string Kind { get; set; } = "";
    public DateTime DocumentDate { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int PayableAccountId { get; set; }
    // Positive = payable. Negative = credit/advance; never create a fictitious bill.
    public decimal Amount { get; set; }
}

[Table("SupplierCreditApplications", Schema = "Finance")]
public sealed class SupplierCreditApplication : AuditableEntity
{
    public int CreditOpeningItemId { get; set; }
    public int? SupplierOpeningItemId { get; set; }
    public int? SupplierBillId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
}

[Table("OpeningBankItems", Schema = "Finance")]
public sealed class OpeningBankItem : AuditableEntity
{
    [MaxLength(50)] public string ChequeNumber { get; set; } = "";
    public DateTime? ChequeDate { get; set; }
    public int? ReceivedChequeId { get; set; }
    public ChequeRegister? ReceivedCheque { get; set; }
    public int MigrationUnitId { get; set; }
    public int BankAccountId { get; set; }
    public int? SupplierId { get; set; }
    [MaxLength(100)] public string Reference { get; set; } = "";
    [MaxLength(40)] public string Kind { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public bool LedgerAlreadyReflected { get; set; }
    public DateTime? ClearedDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public int? ResolutionJournalId { get; set; }
}
