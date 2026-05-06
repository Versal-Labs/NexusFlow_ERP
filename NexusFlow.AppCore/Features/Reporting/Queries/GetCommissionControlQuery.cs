using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Queries
{
    public class CommissionControlDto
    {
        public int SalesRepId { get; set; }
        public string SalesRepName { get; set; } = string.Empty;
        public decimal TotalUnearned { get; set; }         // 1. Invoice created, no cash
        public decimal TotalPendingClearance { get; set; } // 2. Cheque deposited, waiting to clear
        public decimal TotalReadyToPay { get; set; }       // 3. Cash in bank, ready for HR to pay
        public decimal TotalPaid { get; set; }             // 4. Paid out to rep
    }

    public class GetCommissionControlQuery : IRequest<Result<List<CommissionControlDto>>>
    {
        public DateTime? StartDate { get; set; } // Usually based on Invoice Date or Payment Date
        public DateTime? EndDate { get; set; }
    }

    public class GetCommissionControlHandler : IRequestHandler<GetCommissionControlQuery, Result<List<CommissionControlDto>>>
    {
        private readonly IErpDbContext _context;
        public GetCommissionControlHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<CommissionControlDto>>> Handle(GetCommissionControlQuery request, CancellationToken cancellationToken)
        {
            var query = _context.CommissionLedgers
                .Include(c => c.SalesRep)
                .Include(c => c.SalesInvoice)
                .AsQueryable();

            if (request.StartDate.HasValue)
                query = query.Where(c => c.SalesInvoice.InvoiceDate >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(c => c.SalesInvoice.InvoiceDate <= request.EndDate.Value.AddDays(1).AddTicks(-1));

            var data = await query
        .GroupBy(c => new { c.SalesRepId, c.SalesRep.FirstName, c.SalesRep.LastName })
        .Select(g => new CommissionControlDto
        {
            SalesRepId = g.Key.SalesRepId,
            SalesRepName = $"{g.Key.FirstName} {g.Key.LastName}",

            TotalUnearned = g.Where(x => x.Status == CommissionStatus.Unearned).Sum(x => x.CommissionAmount),
            TotalPendingClearance = g.Where(x => x.Status == CommissionStatus.PendingClearance).Sum(x => x.CommissionAmount),
            TotalReadyToPay = g.Where(x => x.Status == CommissionStatus.ReadyToPay).Sum(x => x.CommissionAmount),
            TotalPaid = g.Where(x => x.Status == CommissionStatus.Paid).Sum(x => x.CommissionAmount)
        })
        .ToListAsync(cancellationToken);

            return Result<List<CommissionControlDto>>.Success(data);
        }
    }
}
