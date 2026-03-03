using MediatR;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Queries
{
    public class ReceiptDto
    {
        public int Id { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? RelatedDocumentNo { get; set; }
    }

    public class GetReceiptsQuery : IRequest<Result<List<ReceiptDto>>> { }
}
