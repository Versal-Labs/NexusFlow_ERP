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
        public decimal UnappliedCredit { get; set; }
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
            var suppliers = await _context.Suppliers.Where(x => !request.SupplierId.HasValue || x.Id == request.SupplierId).ToListAsync(cancellationToken);
            var agingData = new List<ApAgingDto>();
            var balances = new NexusFlow.AppCore.Services.SupplierBalanceService(_context);
            foreach (var supplier in suppliers)
            {
                var snapshot = await balances.GetAsync(supplier.Id, today, cancellationToken);
                var dto = new ApAgingDto { SupplierId = supplier.Id, SupplierName = supplier.Name, Phone = supplier.Phone, TotalOutstanding = snapshot.NetBalance, UnappliedCredit = snapshot.Debts.Sum(x => x.Balance) - snapshot.NetBalance };
                foreach (var debt in snapshot.Debts.Where(x => x.Balance > 0))
                {
                    var days = (today - debt.DueDate.Date).Days;
                    if (days <= 0) dto.Current += debt.Balance;
                    else if (days <= 30) dto.Days1To30 += debt.Balance;
                    else if (days <= 60) dto.Days31To60 += debt.Balance;
                    else if (days <= 90) dto.Days61To90 += debt.Balance;
                    else dto.Over90Days += debt.Balance;
                }
                if (dto.TotalOutstanding != 0 || dto.UnappliedCredit != 0 || snapshot.Debts.Any(x => x.Balance != 0)) agingData.Add(dto);
            }
            agingData = agingData.OrderByDescending(x => x.TotalOutstanding).ToList();

            return Result<List<ApAgingDto>>.Success(agingData);
        }
    }
}
