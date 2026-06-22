using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public sealed record ClearEndorsedChequeCommand(int ChequeId, DateTime ClearedDate)
        : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public DateTime FinancialDate => ClearedDate;
    }

    public sealed class ClearEndorsedChequeHandler : IRequestHandler<ClearEndorsedChequeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public ClearEndorsedChequeHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(ClearEndorsedChequeCommand request, CancellationToken cancellationToken)
        {
            var cheque = await _context.ChequeRegisters
                .Include(x => x.OriginalReceipt).ThenInclude(x => x.Allocations).ThenInclude(x => x.SalesInvoice)
                .Include(x => x.EndorsedPayment).ThenInclude(x => x.Allocations).ThenInclude(x => x.SupplierBill)
                .FirstOrDefaultAsync(x => x.Id == request.ChequeId, cancellationToken);

            if (cheque == null) return Result<int>.Failure("Cheque not found.");
            if (cheque.Status != ChequeStatus.Endorsed)
                return Result<int>.Failure("Only an endorsed cheque can be manually marked as cleared.");

            cheque.Status = ChequeStatus.Cleared;
            cheque.ClearedDate = request.ClearedDate;

            foreach (var allocation in cheque.OriginalReceipt.Allocations.Where(x => x.SalesInvoice != null))
            {
                var invoice = allocation.SalesInvoice!;
                invoice.PaymentStatus = invoice.AmountPaid >= invoice.GrandTotal
                    ? InvoicePaymentStatus.Paid
                    : invoice.AmountPaid > 0 ? InvoicePaymentStatus.Partial : InvoicePaymentStatus.Unpaid;
            }

            if (cheque.EndorsedPayment != null)
            {
                foreach (var allocation in cheque.EndorsedPayment.Allocations.Where(x => x.SupplierBill != null))
                {
                    var bill = allocation.SupplierBill!;
                    bill.PaymentStatus = bill.AmountPaid >= bill.GrandTotal
                        ? InvoicePaymentStatus.Paid
                        : bill.AmountPaid > 0 ? InvoicePaymentStatus.Partial : InvoicePaymentStatus.Unpaid;
                }
            }

            var invoiceIds = cheque.OriginalReceipt.Allocations.Where(x => x.SalesInvoiceId.HasValue)
                .Select(x => x.SalesInvoiceId!.Value).Distinct().ToList();
            var commissions = await _context.CommissionLedgers
                .Where(x => invoiceIds.Contains(x.SalesInvoiceId) && x.Status == CommissionStatus.PendingClearance)
                .ToListAsync(cancellationToken);
            commissions.ForEach(x => x.Status = CommissionStatus.ReadyToPay);

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(cheque.Id, "Endorsed cheque marked as cleared and linked documents finalized.");
        }
    }
}
