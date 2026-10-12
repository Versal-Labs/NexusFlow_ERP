using System.ComponentModel.DataAnnotations.Schema;
using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Sales;

namespace NexusFlow.Domain.Entities.Finance;

[Table("CustomerCreditApplications", Schema = "Finance")]
public sealed class CustomerCreditApplication : AuditableEntity
{
    public int CreditOpeningItemId { get; set; }
    public CustomerOpeningItem CreditOpeningItem { get; set; } = null!;
    public int? SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
    public int? CustomerOpeningItemId { get; set; }
    public CustomerOpeningItem? CustomerOpeningItem { get; set; }
    public int? CustomerDebitMemoId { get; set; }
    public CustomerDebitMemo? CustomerDebitMemo { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public DateTime? ReversedDate { get; set; }
    public string? ReversalReason { get; set; }
}
