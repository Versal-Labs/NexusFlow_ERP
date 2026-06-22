using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public class EndorseChequeCommand : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public int ChequeId { get; set; }
        public int SupplierId { get; set; }
        public DateTime EndorsementDate { get; set; }
        public List<PaymentAllocationRequest> Allocations { get; set; } = new();
        public DateTime FinancialDate => EndorsementDate;
    }

    public class EndorseChequeHandler : IRequestHandler<EndorseChequeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public EndorseChequeHandler(IErpDbContext context, IJournalService journalService, INumberSequenceService sequenceService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _sequenceService = sequenceService; _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(EndorseChequeCommand request, CancellationToken cancellationToken)
        {
            if (request.Allocations.GroupBy(x => x.InvoiceId).Any(x => x.Count() > 1))
                return Result<int>.Failure("Duplicate supplier bill allocations are not allowed.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. VALIDATE CHEQUE & SUPPLIER
                var cheque = await _context.ChequeRegisters.FirstOrDefaultAsync(c => c.Id == request.ChequeId, cancellationToken);

                if (cheque == null) return Result<int>.Failure("Cheque not found.");
                if (cheque.Status != ChequeStatus.InSafe)
                    return Result<int>.Failure($"Cannot endorse a cheque that is currently {cheque.Status}. It must be in the Safe.");

                var supplier = await _context.Suppliers.FindAsync(new object[] { request.SupplierId }, cancellationToken);
                if (supplier == null || supplier.DefaultPayableAccountId == 0)
                    return Result<int>.Failure("Supplier or AP Account mapping missing.");

                if (request.Allocations.Any(x => x.Amount <= 0))
                    return Result<int>.Failure("Allocation amounts must be greater than zero.");

                decimal totalAllocated = request.Allocations.Sum(a => a.Amount);
                if (totalAllocated > cheque.Amount)
                    return Result<int>.Failure("You cannot allocate more money to bills than the cheque is worth.");

                var billIds = request.Allocations.Select(x => x.InvoiceId).ToList();
                var bills = await _context.SupplierBills
                    .Where(x => billIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, cancellationToken);

                foreach (var allocation in request.Allocations)
                {
                    if (!bills.TryGetValue(allocation.InvoiceId, out var bill) || bill.SupplierId != request.SupplierId || !bill.IsPosted)
                        return Result<int>.Failure($"Supplier bill {allocation.InvoiceId} is invalid for this endorsement.");
                    if (allocation.Amount > bill.GrandTotal - bill.AmountPaid)
                        return Result<int>.Failure($"Allocation exceeds the remaining balance of bill {bill.BillNumber}.");
                }

                // 2. CREATE SUPPLIER PAYMENT TRANSACTION
                string payRef = await _sequenceService.GenerateNextNumberAsync(NumberSequenceKeys.Payment, cancellationToken);

                var payment = new PaymentTransaction
                {
                    ReferenceNo = payRef,
                    Date = request.EndorsementDate,
                    Type = PaymentType.SupplierPayment,
                    Method = PaymentMethod.EndorsedCustomerCheque,
                    Amount = cheque.Amount,
                    SupplierId = request.SupplierId,
                    Remarks = $"Endorsed Customer Cheque: {cheque.ChequeNumber}"
                };

                _context.PaymentTransactions.Add(payment);

                if (request.Allocations.Any())
                {
                    foreach (var alloc in request.Allocations)
                    {
                        payment.Allocations.Add(new PaymentAllocation
                        {
                            SupplierBillId = alloc.InvoiceId,
                            AmountAllocated = alloc.Amount
                        });

                        var bill = bills[alloc.InvoiceId];
                        bill.AmountPaid += alloc.Amount;
                        bill.PaymentStatus = bill.AmountPaid >= bill.GrandTotal
                            ? InvoicePaymentStatus.PendingClearance
                            : InvoicePaymentStatus.Partial;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken); // Save to get Payment ID

                // 4. UPDATE CHEQUE STATUS
                cheque.Status = ChequeStatus.Endorsed;
                cheque.EndorsedPaymentId = payment.Id;
                cheque.EndorsedDate = request.EndorsementDate;

                // 5. POST GENERAL LEDGER
                int safeAccountId = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.UndepositedFunds, cancellationToken);

                var journalLines = new List<JournalLineRequest>
                {
                    // Accounting invariant: endorsement transfers the cheque asset to the supplier and conditionally settles AP.
                    new JournalLineRequest { AccountId = supplier.DefaultPayableAccountId ?? 0, Debit = cheque.Amount, Credit = 0, Note = $"Endorsed Cheque {cheque.ChequeNumber}" },
                    new JournalLineRequest { AccountId = safeAccountId, Debit = 0, Credit = cheque.Amount, Note = $"Cheque Swapped to {supplier.Name}" }
                };

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.EndorsementDate,
                    Description = $"Cheque Endorsement: {cheque.ChequeNumber} to {supplier.Name}",
                    Module = "Treasury",
                    ReferenceNo = payRef,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Failed: {jResult.Message}");

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(payment.Id, "Cheque successfully endorsed to supplier.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Endorsement failed: {ex.Message}");
            }
        }
    }
}
