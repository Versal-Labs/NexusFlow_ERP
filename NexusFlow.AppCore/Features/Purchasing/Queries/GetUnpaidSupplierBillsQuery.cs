using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Queries
{
    // ==========================================
    // 1. THE DTO
    // ==========================================
    public class UnpaidSupplierBillDto
    {
        public string TargetType { get; set; } = "Bill";
        public DateTime DueDate { get; set; }
        public int Id { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public decimal Balance { get; set; }
    }

    // ==========================================
    // 2. THE QUERY
    // ==========================================
    public class GetUnpaidSupplierBillsQuery : IRequest<Result<List<UnpaidSupplierBillDto>>>
    {
        public int SupplierId { get; set; }
    }

    // ==========================================
    // 3. THE HANDLER
    // ==========================================
    public class GetUnpaidSupplierBillsHandler : IRequestHandler<GetUnpaidSupplierBillsQuery, Result<List<UnpaidSupplierBillDto>>>
    {
        private readonly IErpDbContext _context;

        public GetUnpaidSupplierBillsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<UnpaidSupplierBillDto>>> Handle(GetUnpaidSupplierBillsQuery request, CancellationToken cancellationToken)
        {
            // Fetch bills for the supplier that are POSTED and NOT fully paid
            var unpaidBills = await _context.SupplierBills
                .AsNoTracking()
                .Where(b => b.SupplierId == request.SupplierId
                         && b.IsPosted
                         && b.PaymentStatus != InvoicePaymentStatus.Voided
                         && b.GrandTotal > b.AmountPaid)
                .OrderBy(b => b.DueDate) // Order by Due Date so oldest bills appear first for allocation
                .Select(b => new UnpaidSupplierBillDto
                {
                    Id = b.Id,
                    BillNumber = b.BillNumber,
                    Date = b.BillDate,
                    Total = b.GrandTotal,
                    // The Balance is mathematically calculated on the fly
                    Balance = b.GrandTotal - b.AmountPaid
                })
                .ToListAsync(cancellationToken);

            var snapshot = await new NexusFlow.AppCore.Services.SupplierBalanceService(_context).GetAsync(request.SupplierId, DateTime.UtcNow.Date, cancellationToken);
            unpaidBills = snapshot.Debts.Where(x => x.Balance > 0).OrderBy(x => x.DueDate).Select(x => new UnpaidSupplierBillDto { Id = x.Id, TargetType = x.TargetType, BillNumber = x.Reference, Date = x.Date, DueDate = x.DueDate, Total = x.Amount, Balance = x.Balance }).ToList();
            return Result<List<UnpaidSupplierBillDto>>.Success(unpaidBills);
        }
    }
}
