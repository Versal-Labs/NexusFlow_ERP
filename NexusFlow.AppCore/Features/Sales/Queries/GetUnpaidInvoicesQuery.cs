using MediatR;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Queries
{
    public class UnpaidInvoiceDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance { get; set; }
    }

    public class GetUnpaidInvoicesQuery : IRequest<Result<List<UnpaidInvoiceDto>>>
    {
        public int CustomerId { get; set; }
    }
}
