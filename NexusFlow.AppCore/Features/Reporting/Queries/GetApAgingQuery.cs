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
    public class ApAgingDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal TotalOutstanding { get; set; }
        public decimal Current { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90Days { get; set; }
    }

    public class GetApAgingQuery : IRequest<Result<List<ApAgingDto>>>
    {
        public int? SupplierId { get; set; }
    }

    public class GetApAgingHandler : IRequestHandler<GetApAgingQuery, Result<List<ApAgingDto>>>
    {
        private readonly IErpDbContext _context;
        public GetApAgingHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<ApAgingDto>>> Handle(GetApAgingQuery request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            var query = _context.SupplierBills
                .Include(b => b.Supplier)
                .Where(b => b.IsPosted && b.PaymentStatus != InvoicePaymentStatus.Paid)
                .AsQueryable();

            if (request.SupplierId.HasValue)
                query = query.Where(b => b.SupplierId == request.SupplierId.Value);

            var openBills = await query.ToListAsync(cancellationToken);

            var agingData = openBills
                .GroupBy(b => new { b.SupplierId, b.Supplier.Name, b.Supplier.Phone })
                .Select(g =>
                {
                    var dto = new ApAgingDto
                    {
                        SupplierId = g.Key.SupplierId,
                        SupplierName = g.Key.Name,
                        Phone = g.Key.Phone
                    };

                    foreach (var bill in g)
                    {
                        decimal balance = bill.GrandTotal - bill.AmountPaid;
                        dto.TotalOutstanding += balance;

                        int daysOverdue = (today - bill.DueDate.Date).Days;

                        if (daysOverdue <= 0) dto.Current += balance;
                        else if (daysOverdue <= 30) dto.Days1To30 += balance;
                        else if (daysOverdue <= 60) dto.Days31To60 += balance;
                        else if (daysOverdue <= 90) dto.Days61To90 += balance;
                        else dto.Over90Days += balance;
                    }
                    return dto;
                })
                .Where(d => d.TotalOutstanding > 0)
                .OrderByDescending(d => d.TotalOutstanding)
                .ToList();

            return Result<List<ApAgingDto>>.Success(agingData);
        }
    }
}
