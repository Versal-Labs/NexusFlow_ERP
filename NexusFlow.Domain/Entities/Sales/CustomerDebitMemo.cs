using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("CustomerDebitMemos", Schema = "Sales")]
    public sealed class CustomerDebitMemo : AuditableEntity
    {
        public string DebitMemoNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
        public int? ChequeRegisterId { get; set; }
        public ChequeRegister? ChequeRegister { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountPaid { get; set; }
        public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Unpaid;
        public string Reason { get; set; } = string.Empty;
    }
}
