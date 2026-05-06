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

    // NEW: The Wrapper Response
    public class UnpaidInvoicesResponseDto
    {
        public decimal UnappliedCredit { get; set; }
        public List<UnpaidInvoiceDto> Invoices { get; set; } = new();
    }

    // UPDATE: Change the return type
    public class GetUnpaidInvoicesQuery : IRequest<Result<UnpaidInvoicesResponseDto>>
    {
        public int CustomerId { get; set; }
    }
}
