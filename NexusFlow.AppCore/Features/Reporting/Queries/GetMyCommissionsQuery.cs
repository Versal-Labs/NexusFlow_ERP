using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Queries
{
    public class RepCommissionLineDto
    {
        public string InvoiceDate { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal InvoiceTotal { get; set; }
        public decimal CommissionAmount { get; set; }
        public string Status { get; set; } = string.Empty; // Unearned, Earned, Paid
    }

    public class GetMyCommissionsQuery : IRequest<Result<List<RepCommissionLineDto>>>
    {
        public int SalesRepId { get; set; } // We will pass the logged-in user's ID here
    }

    public class GetMyCommissionsHandler : IRequestHandler<GetMyCommissionsQuery, Result<List<RepCommissionLineDto>>>
    {
        private readonly IErpDbContext _context;
        public GetMyCommissionsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<RepCommissionLineDto>>> Handle(GetMyCommissionsQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.CommissionLedgers
                .Include(c => c.SalesInvoice)
                    .ThenInclude(i => i.Customer)
                .Where(c => c.SalesRepId == request.SalesRepId)
                .OrderByDescending(c => c.SalesInvoice.InvoiceDate)
                .Select(c => new RepCommissionLineDto
                {
                    InvoiceDate = c.SalesInvoice.InvoiceDate.ToString("yyyy-MM-dd"),
                    InvoiceNo = c.SalesInvoice.InvoiceNumber,
                    CustomerName = c.SalesInvoice.Customer.Name,
                    InvoiceTotal = c.SalesInvoice.GrandTotal,
                    CommissionAmount = c.CommissionAmount,
                    Status = c.Status.ToString()
                })
                .ToListAsync(cancellationToken);

            return Result<List<RepCommissionLineDto>>.Success(data);
        }
    }
}
