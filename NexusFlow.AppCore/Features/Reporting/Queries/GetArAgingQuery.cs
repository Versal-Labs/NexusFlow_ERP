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
    public class ArAgingDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal TotalOutstanding { get; set; }
        public decimal Current { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90Days { get; set; }
    }

    public class GetArAgingQuery : IRequest<Result<List<ArAgingDto>>>
    {
        public int? CustomerId { get; set; }
    }

    public class GetArAgingHandler : IRequestHandler<GetArAgingQuery, Result<List<ArAgingDto>>>
    {
        private readonly IErpDbContext _context;
        public GetArAgingHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<ArAgingDto>>> Handle(GetArAgingQuery request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            // 1. Fetch all open invoices
            var query = _context.SalesInvoices
                .Include(i => i.Customer)
                .Where(i => i.IsPosted && i.PaymentStatus != InvoicePaymentStatus.Paid)
                .AsQueryable();

            if (request.CustomerId.HasValue)
                query = query.Where(i => i.CustomerId == request.CustomerId.Value);

            var openInvoices = await query.ToListAsync(cancellationToken);

            // 2. Group by Customer and Bucket the Aging in memory
            var agingData = openInvoices
                .GroupBy(i => new { i.CustomerId, i.Customer.Name, i.Customer.Phone })
                .Select(g =>
                {
                    var dto = new ArAgingDto
                    {
                        CustomerId = g.Key.CustomerId,
                        CustomerName = g.Key.Name,
                        Phone = g.Key.Phone
                    };

                    foreach (var inv in g)
                    {
                        decimal balance = inv.GrandTotal - inv.AmountPaid;
                        dto.TotalOutstanding += balance;

                        int daysOverdue = (today - inv.DueDate.Date).Days;

                        if (daysOverdue <= 0) dto.Current += balance;
                        else if (daysOverdue <= 30) dto.Days1To30 += balance;
                        else if (daysOverdue <= 60) dto.Days31To60 += balance;
                        else if (daysOverdue <= 90) dto.Days61To90 += balance;
                        else dto.Over90Days += balance;
                    }
                    return dto;
                })
                .Where(d => d.TotalOutstanding > 0) // Ignore zero balances
                .OrderByDescending(d => d.TotalOutstanding)
                .ToList();

            return Result<List<ArAgingDto>>.Success(agingData);
        }
    }
}
