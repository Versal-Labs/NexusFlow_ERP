using MediatR;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          
namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public class RecordSupplierPaymentHandler : IRequestHandler<RecordSupplierPaymentCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public RecordSupplierPaymentHandler(IErpDbContext context, IJournalService journalService, INumberSequenceService sequenceService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _sequenceService = sequenceService; _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(RecordSupplierPaymentCommand command, CancellationToken cancellationToken)
        {
            if (command.PaymentAmount <= 0) return Result<int>.Failure("Payment amount must be greater than zero.");
            if (command.Method == PaymentMethod.EndorsedCustomerCheque)
                return Result<int>.Failure("Endorsed customer cheques must be posted through the controlled cheque endorsement workflow.");

            // EDGE CASE: Overpayment Guard
            decimal totalAllocated = command.Allocations.Sum(a => a.Amount);
            if (totalAllocated > command.PaymentAmount)
                return Result<int>.Failure($"Cannot allocate more ({totalAllocated}) than the payment total ({command.PaymentAmount}).");

            using var transaction = await _context.BeginTransactionAsync(global::System.Data.IsolationLevel.Serializable, cancellationToken);
            try
            {
                string payRef = await _sequenceService.GenerateNextNumberAsync("Payment", cancellationToken);

                int targetGlAccountId = command.AccountId ?? 0;
                if (targetGlAccountId == 0) return Result<int>.Failure("A valid Pay-From account or Cheque must be selected.");

                // ==========================================
                // 2. CREATE PAYMENT TRANSACTION
                // ==========================================
                var payment = new PaymentTransaction
                {
                    ReferenceNo = payRef,
                    Date = command.Date,
                    Type = PaymentType.SupplierPayment,
                    Method = command.Method,
                    Amount = command.PaymentAmount,
                    SupplierId = command.SupplierId,
                    Remarks = command.Remarks
                };

                _context.PaymentTransactions.Add(payment);

                // ==========================================
                // 3. ALLOCATE TO SUPPLIER BILLS (AP)
                // ==========================================
                var snapshot = await new NexusFlow.AppCore.Services.SupplierBalanceService(_context).GetAsync(command.SupplierId, command.Date, cancellationToken);
                foreach (var alloc in command.Allocations)
                {
                    if (alloc.Amount <= 0 || decimal.Round(alloc.Amount, 2) != alloc.Amount || alloc.OpeningItemId.HasValue || alloc.DebitMemoId.HasValue)
                        throw new InvalidOperationException("Each supplier allocation must have a positive LKR amount and a supplier bill/opening item target.");
                    var target = snapshot.Debts.SingleOrDefault(x => x.Id == (alloc.SupplierOpeningItemId ?? alloc.InvoiceId) && x.TargetType == (alloc.SupplierOpeningItemId.HasValue ? "OpeningItem" : "Bill"));
                    if (target == null || command.Allocations.Where(x => (x.SupplierOpeningItemId ?? x.InvoiceId) == target.Id && x.SupplierOpeningItemId.HasValue == alloc.SupplierOpeningItemId.HasValue).Sum(x => x.Amount) > target.Balance)
                        throw new InvalidOperationException("Allocation belongs to a different supplier or exceeds the outstanding balance.");
                    if (alloc.SupplierOpeningItemId.HasValue) payment.Allocations.Add(new PaymentAllocation { SupplierOpeningItemId = alloc.SupplierOpeningItemId, AmountAllocated = alloc.Amount });
                }
                if (command.Allocations.Any(x => !x.SupplierOpeningItemId.HasValue))
                {
                    var billIds = command.Allocations.Where(x => !x.SupplierOpeningItemId.HasValue).Select(a => a.InvoiceId).ToList();
                    var bills = await _context.SupplierBills.Where(b => billIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, cancellationToken);

                    foreach (var alloc in command.Allocations.Where(x => !x.SupplierOpeningItemId.HasValue))
                    {
                        if (alloc.Amount <= 0 || !bills.TryGetValue(alloc.InvoiceId, out var bill)) continue;

                        payment.Allocations.Add(new PaymentAllocation { SupplierBillId = bill.Id, AmountAllocated = alloc.Amount });
                        bill.AmountPaid += alloc.Amount;
                        bill.PaymentStatus = bill.AmountPaid >= bill.GrandTotal ? InvoicePaymentStatus.Paid : InvoicePaymentStatus.Partial;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken); // Save to get Payment ID

                // ==========================================
                // 5. DOUBLE-ENTRY GL POSTING (Handles AP Advances Perfectly)
                // ==========================================
                var supplier = await _context.Suppliers.FindAsync(new object[] { command.SupplierId }, cancellationToken);

                var journalLines = new List<JournalLineRequest>
                {
                    // DEBIT: Accounts Payable (Reduces Liability). Unallocated amounts naturally sit here as an AP Advance!
                    new() { AccountId = supplier!.DefaultPayableAccountId!.Value, Debit = command.PaymentAmount, Credit = 0, Note = $"Payment to {supplier.Name}" },
                    
                    // CREDIT: Target GL (Reduces Asset: Cash, Bank, or Vault if Swapped)
                    new() { AccountId = targetGlAccountId, Debit = 0, Credit = command.PaymentAmount, Note = $"Source: {payRef}" }
                };

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = command.Date,
                    Description = $"Supplier Payment: {payRef}",
                    Module = "Treasury",
                    ReferenceNo = payRef,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Failed: {jResult.Message}");

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(payment.Id, "Supplier Payment recorded successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Payment failed: {ex.Message}");
            }
        }
    }
}
