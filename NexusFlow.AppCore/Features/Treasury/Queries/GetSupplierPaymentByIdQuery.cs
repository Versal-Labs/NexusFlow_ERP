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
    public class PaymentDetailDto
    {
        public int Id { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public int Method { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty; // E.g., "Bank of America" or "Vault"

        public List<PaymentAllocationDetailDto> Allocations { get; set; } = new();
    }

    public class PaymentAllocationDetailDto
    {
        public string BillNumber { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public decimal AmountApplied { get; set; }
    }

    public class GetSupplierPaymentByIdQuery : IRequest<Result<PaymentDetailDto>>
    {
        public int PaymentId { get; set; }
    }

    public class GetSupplierPaymentByIdHandler : IRequestHandler<GetSupplierPaymentByIdQuery, Result<PaymentDetailDto>>
    {
        private readonly IErpDbContext _context;

        public GetSupplierPaymentByIdHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<PaymentDetailDto>> Handle(GetSupplierPaymentByIdQuery request, CancellationToken cancellationToken)
        {
            var payment = await _context.PaymentTransactions
                .Include(p => p.Supplier)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.SupplierBill)
                .FirstOrDefaultAsync(p => p.Id == request.PaymentId && p.Type == PaymentType.SupplierPayment, cancellationToken);

            if (payment == null) return Result<PaymentDetailDto>.Failure("Payment not found.");

            // Resolve the Source Name (Did it come from a Bank, Cash, or was it a Swapped Cheque?)
            string sourceName = "Unknown";
            if (payment.Method == (PaymentMethod)5) // Endorsed Cheque
            {
                var endorsedCheque = await _context.ChequeRegisters.FirstOrDefaultAsync(c => c.EndorsedPaymentId == payment.Id, cancellationToken);
                sourceName = endorsedCheque != null ? $"Swapped Cheque: {endorsedCheque.ChequeNumber}" : "Vault (Swapped)";
            }
            else
            {
                // To get the exact bank account, we would look at the Journal Entry, but we can leave this generic for now 
                // or fetch it if you mapped 'AccountId' to the PaymentTransaction table.
                sourceName = payment.Method == PaymentMethod.Cash ? "Cash Account" : "Bank Transfer";
            }

            var dto = new PaymentDetailDto
            {
                Id = payment.Id,
                ReferenceNo = payment.ReferenceNo,
                Date = payment.Date,
                Amount = payment.Amount,
                Method = (int)payment.Method,
                SupplierName = payment.Supplier?.Name ?? "Unknown Supplier",
                Remarks = payment.Remarks ?? "",
                SourceName = sourceName,
                Allocations = payment.Allocations
                    .Where(a => a.SupplierBill != null)
                    .Select(a => new PaymentAllocationDetailDto
                    {
                        BillNumber = a.SupplierBill!.BillNumber,
                        BillDate = a.SupplierBill.BillDate,
                        AmountApplied = a.AmountAllocated
                    }).ToList()
            };

            return Result<PaymentDetailDto>.Success(dto);
        }
    }
}
