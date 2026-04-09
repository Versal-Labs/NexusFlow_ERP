using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("ChequeRegister", Schema = "Finance")]
    public class ChequeRegister : AuditableEntity
    {
        public string ChequeNumber { get; set; } = string.Empty;
        public int BankBranchId { get; set; }
        public BankBranch BankBranch { get; set; } = null!;
        public DateTime ChequeDate { get; set; } // PDC Date
        public decimal Amount { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        // 1. The original Customer Receipt that generated this cheque
        public int OriginalReceiptId { get; set; }
        [ForeignKey("OriginalReceiptId")]
        public PaymentTransaction OriginalReceipt { get; set; } = null!; // <--- ADDED THIS

        // 2. Links to the Supplier Payment if we Endorse (Swap) it
        public int? EndorsedPaymentId { get; set; } // <--- ADDED THIS
        [ForeignKey("EndorsedPaymentId")]
        public PaymentTransaction? EndorsedPayment { get; set; } // <--- ADDED THIS

        // The original Customer Receipt that generated this cheque

        // Where did we deposit it? (Needed for Bounce Reversals)
        public int? DepositedBankAccountId { get; set; }
        public Account? DepositedBankAccount { get; set; }

        public ChequeStatus Status { get; set; } = ChequeStatus.InSafe;
        public string? BounceReason { get; set; }
    }
}
