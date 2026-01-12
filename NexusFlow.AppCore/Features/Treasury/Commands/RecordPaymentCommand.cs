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
        public decimal Amount { get; set; }
        public string Remarks { get; set; } = string.Empty;

        // One of these must be provided
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }

        public string? RelatedDocumentNo { get; set; } // e.g. "INV-2024-001"
    }
}
