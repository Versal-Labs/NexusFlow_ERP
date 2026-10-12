using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.Domain.Entities.Sales;

[Table("CustomerOpeningItems", Schema = "Sales")]
public sealed class CustomerOpeningItem : AuditableEntity
{
    public int MigrationBatchId { get; set; }
    public CustomerMigrationBatch MigrationBatch { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int ReceivableAccountId { get; set; }
    [MaxLength(50)] public string LegacyCustomerCode { get; set; } = "";
    [MaxLength(100)] public string ReferenceNo { get; set; } = "";
    [MaxLength(40)] public string Kind { get; set; } = "";
    public DateTime DocumentDate { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal SourceDebitAmount { get; set; }
    public decimal AmountSettled { get; set; }
    public int SourceRow { get; set; }
    public string Notes { get; set; } = "";
}
