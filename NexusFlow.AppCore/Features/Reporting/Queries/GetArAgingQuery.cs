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
        public decimal GrossOutstanding { get; set; }
        public decimal UnappliedCredit { get; set; }
        public decimal Current { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90Days { get; set; }
    }

    public class GetArAgingQuery : IRequest<Result<List<ArAgingDto>>>
    {
        public int? CustomerId { get; set; }
        public int? SalesRepId { get; set; }
        public DateTime? AsOfDate { get; set; }
    }

    public class GetArAgingHandler : IRequestHandler<GetArAgingQuery, Result<List<ArAgingDto>>>
    {
        private readonly IErpDbContext _context;
        public GetArAgingHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<ArAgingDto>>> Handle(GetArAgingQuery request, CancellationToken cancellationToken)
        {
            var today = (request.AsOfDate ?? DateTime.UtcNow).Date;
            var query = _context.Customers.AsNoTracking().AsQueryable();
            if (request.CustomerId.HasValue) query = query.Where(x => x.Id == request.CustomerId);
            if (request.SalesRepId.HasValue) query = query.Where(x => x.SalesRepId == request.SalesRepId);
            var customers = await query.ToListAsync(cancellationToken);
            var balances = new NexusFlow.AppCore.Services.CustomerBalanceService(_context);
            var agingData = new List<ArAgingDto>();
            var snapshots = await balances.GetManyAsync(customers.Select(x => x.Id), today, cancellationToken);
            foreach (var customer in customers)
            {
                var snapshot = snapshots[customer.Id];
                var dto = new ArAgingDto { CustomerId = customer.Id, CustomerName = customer.Name, Phone = customer.Phone,
                    TotalOutstanding = snapshot.NetBalance, GrossOutstanding = snapshot.Debits.Sum(x => x.Balance), UnappliedCredit = snapshot.UnappliedCredit };
                foreach (var debit in snapshot.Debits.Where(x => x.Balance > 0))
                {
                    int days = (today - debit.DueDate).Days;
                    if (days <= 0) dto.Current += debit.Balance;
                    else if (days <= 30) dto.Days1To30 += debit.Balance;
                    else if (days <= 60) dto.Days31To60 += debit.Balance;
                    else if (days <= 90) dto.Days61To90 += debit.Balance;
                    else dto.Over90Days += debit.Balance;
                }
                if (dto.GrossOutstanding != 0 || dto.UnappliedCredit != 0) agingData.Add(dto);
            }
            agingData = agingData.OrderByDescending(x => x.TotalOutstanding).ToList();

            return Result<List<ArAgingDto>>.Success(agingData);
        }
    }
}
