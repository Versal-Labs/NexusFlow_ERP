using MediatR;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public class RecordSupplierPaymentCommand : IRequest<Result<int>>
    {
        public PaymentMethod Method { get; set; }
        public DateTime Date { get; set; }
        public decimal PaymentAmount { get; set; }
        public string? Remarks { get; set; }

        public int SupplierId { get; set; }

        // The Bank/Cash account paying the bill (Not needed if swapping a cheque)
        public int? AccountId { get; set; }

        // The Vault Cheque being handed to the supplier
        public int? EndorsedChequeId { get; set; }

        public List<PaymentAllocationRequest> Allocations { get; set; } = new();
    }
}
