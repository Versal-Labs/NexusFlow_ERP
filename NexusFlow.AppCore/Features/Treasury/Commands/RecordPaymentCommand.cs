using MediatR;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public class RecordPaymentCommand : IRequest<Result<int>>
    {
        public DateTime Date { get; set; }
        public PaymentType Type { get; set; }
        public PaymentMethod Method { get; set; }

        public decimal ReceiptAmount { get; set; } // The actual physical money/cheque received
        public int AccountId { get; set; } // Destination Bank/Cash
        public int? CustomerId { get; set; }
        public string Remarks { get; set; } = string.Empty;

        // CHEQUE SPECIFIC FIELDS
        public string? ChequeNumber { get; set; }
        public int? BankBranchId { get; set; }
        public DateTime? ChequeDate { get; set; }

        public List<PaymentAllocationRequest> Allocations { get; set; } = new();
    }

    public class PaymentAllocationRequest
    {
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
    }
}
