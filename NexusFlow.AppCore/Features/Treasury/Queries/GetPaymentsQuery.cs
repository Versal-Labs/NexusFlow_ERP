using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Queries
{
    // ==========================================
    // 1. THE DTO (Matches the JS Grid perfectly)
    // ==========================================
    public class PaymentGridDto
    {
        public int Id { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public int Method { get; set; } // 1=Cash, 2=Transfer, 4=OwnCheque, 5=EndorsedCheque
        public decimal Amount { get; set; }
    }

    // ==========================================
    // 2. THE QUERY
    // ==========================================
    public class GetPaymentsQuery : IRequest<Result<List<PaymentGridDto>>>
    {
        // 1 = Receipt, 2 = SupplierPayment
        public int? Type { get; set; }
        public int? SupplierId { get; set; }
        public int? Method { get; set; }
    }

    // ==========================================
    // 3. THE HANDLER
    // ==========================================
    public class GetPaymentsHandler : IRequestHandler<GetPaymentsQuery, Result<List<PaymentGridDto>>>
    {
        private readonly IErpDbContext _context;

        public GetPaymentsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<PaymentGridDto>>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.PaymentTransactions
                .AsNoTracking()
                .Include(p => p.Supplier)
                .AsQueryable();

            // Apply Filters from the UI Grid
            if (request.Type.HasValue)
            {
                query = query.Where(p => (int)p.Type == request.Type.Value);
            }

            if (request.SupplierId.HasValue)
            {
                query = query.Where(p => p.SupplierId == request.SupplierId.Value);
            }

            if (request.Method.HasValue)
            {
                query = query.Where(p => (int)p.Method == request.Method.Value);
            }

            // Project directly into the DTO
            var payments = await query
                .OrderByDescending(p => p.Date)
                .ThenByDescending(p => p.Id)
                .Select(p => new PaymentGridDto
                {
                    Id = p.Id,
                    ReferenceNo = p.ReferenceNo,
                    Date = p.Date,

                    // Smart resolve: If it's AP, get Supplier Name. If AR, get Customer Name.
                    PartyName = p.Type == PaymentType.SupplierPayment
                        ? (p.Supplier != null ? p.Supplier.Name : "Unknown Supplier")
                        : (p.Customer != null ? p.Customer.Name : "Unknown Customer"),

                    Method = (int)p.Method,
                    Amount = p.Amount
                })
                .ToListAsync(cancellationToken);

            return Result<List<PaymentGridDto>>.Success(payments);
        }
    }
}
