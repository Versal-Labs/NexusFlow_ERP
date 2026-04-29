using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Queries
{
    public class SalesRegisterDto
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string SalesRep { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal VAT { get; set; }
        public decimal GrandTotal { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class GetSalesRegisterQuery : IRequest<Result<List<SalesRegisterDto>>>
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CustomerId { get; set; }
        public int? SalesRepId { get; set; }
    }

    public class GetSalesRegisterHandler : IRequestHandler<GetSalesRegisterQuery, Result<List<SalesRegisterDto>>>
    {
        private readonly IErpDbContext _context;

        public GetSalesRegisterHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<SalesRegisterDto>>> Handle(GetSalesRegisterQuery request, CancellationToken cancellationToken)
        {
            var query = _context.SalesInvoices
                .Include(i => i.Customer)
                .Include(i => i.SalesRep)
                .Where(i => i.IsPosted) // Only show finalized sales in reports
                .AsQueryable();

            if (request.StartDate.HasValue) query = query.Where(i => i.InvoiceDate >= request.StartDate.Value);
            if (request.EndDate.HasValue) query = query.Where(i => i.InvoiceDate <= request.EndDate.Value.AddDays(1).AddTicks(-1));
            if (request.CustomerId.HasValue) query = query.Where(i => i.CustomerId == request.CustomerId.Value);
            if (request.SalesRepId.HasValue) query = query.Where(i => i.SalesRepId == request.SalesRepId.Value);

            var data = await query
                .OrderByDescending(i => i.InvoiceDate)
                .Select(i => new SalesRegisterDto
                {
                    InvoiceNo = i.InvoiceNumber,
                    Date = i.InvoiceDate.ToString("yyyy-MM-dd"),
                    Customer = i.Customer.Name,
                    SalesRep = i.SalesRep != null ? $"{i.SalesRep.FirstName} {i.SalesRep.LastName}" : "Unassigned",
                    SubTotal = i.SubTotal,
                    VAT = i.TotalTax,
                    GrandTotal = i.GrandTotal,
                    Status = i.PaymentStatus.ToString()
                })
                .ToListAsync(cancellationToken);

            return Result<List<SalesRegisterDto>>.Success(data);
        }
    }
}
