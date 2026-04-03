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

            // EDGE CASE: Overpayment Guard
            decimal totalAllocated = command.Allocations.Sum(a => a.Amount);
            if (totalAllocated > command.PaymentAmount)
                return Result<int>.Failure($"Cannot allocate more ({totalAllocated}) than the payment total ({command.PaymentAmount}).");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                string payRef = await _sequenceService.GenerateNextNumberAsync("Payment", cancellationToken);

                int targetGlAccountId = command.AccountId ?? 0;
                ChequeRegister? chequeToEndorse = null;

                // ==========================================
                // 1. CHEQUE SWAPPING LOGIC (Endorsement)
                // ==========================================
                // Assuming '5' is the enum value for EndorsedCheque
                if ((int)command.Method == 5)
                {
                    if (!command.EndorsedChequeId.HasValue)
                        return Result<int>.Failure("You must select a valid cheque from the safe to endorse.");

                    // The asset being reduced is the Vault (Undeposited Funds)
                    targetGlAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Asset.UndepositedFunds", cancellationToken);

                    chequeToEndorse = await _context.ChequeRegisters.FindAsync(new object[] { command.EndorsedChequeId.Value }, cancellationToken);

                    if (chequeToEndorse == null || chequeToEndorse.Status != ChequeStatus.InSafe)
                        return Result<int>.Failure("The selected cheque is invalid or is no longer in the safe.");
                }

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
                if (command.Allocations.Any())
                {
                    var billIds = command.Allocations.Select(a => a.InvoiceId).ToList();
                    var bills = await _context.SupplierBills.Where(b => billIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, cancellationToken);

                    foreach (var alloc in command.Allocations)
                    {
                        if (alloc.Amount <= 0 || !bills.TryGetValue(alloc.InvoiceId, out var bill)) continue;

                        payment.Allocations.Add(new PaymentAllocation { SupplierBillId = bill.Id, AmountAllocated = alloc.Amount });
                        bill.AmountPaid += alloc.Amount;
                        bill.PaymentStatus = bill.AmountPaid >= bill.GrandTotal ? InvoicePaymentStatus.Paid : InvoicePaymentStatus.Partial;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken); // Save to get Payment ID

                // ==========================================
                // 4. MARK CHEQUE AS ENDORSED
                // ==========================================
                if (chequeToEndorse != null)
                {
                    chequeToEndorse.Status = ChequeStatus.Endorsed;
                    chequeToEndorse.EndorsedPaymentId = payment.Id;
                    await _context.SaveChangesAsync(cancellationToken);
                }

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
