using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public class RecordPaymentHandler : IRequestHandler<RecordPaymentCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;

        public RecordPaymentHandler(IErpDbContext context, IJournalService journalService)
        {
            _context = context;
            _journalService = journalService;
        }

        public async Task<Result<int>> Handle(RecordPaymentCommand command, CancellationToken cancellationToken)
        {
            // 1. Validation
            if (command.Amount <= 0)
                return Result<int>.Failure("Amount must be greater than zero.");

            if (command.Type == PaymentType.CustomerReceipt && command.CustomerId == null)
                return Result<int>.Failure("Customer ID is required for Receipts.");

            if (command.Type == PaymentType.SupplierPayment && command.SupplierId == null)
                return Result<int>.Failure("Supplier ID is required for Payments.");

            // 2. Generate Reference
            int count = await _context.PaymentTransactions.CountAsync(cancellationToken) + 1;
            string payRef = $"PAY-{DateTime.UtcNow.Year}-{count:D6}";

            // 3. Save Transaction Record
            var payment = new PaymentTransaction
            {
                ReferenceNo = payRef,
                Date = command.Date,
                Type = command.Type,
                Method = command.Method,
                Amount = command.Amount,
                CustomerId = command.CustomerId,
                SupplierId = command.SupplierId,
                RelatedDocumentNo = command.RelatedDocumentNo,
                Remarks = command.Remarks
            };

            _context.PaymentTransactions.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            // =============================================================
            // 4. POST TO GL (The Accounting Logic)
            // =============================================================

            // A. Identify Bank/Cash Account
            // In a real app, you select the specific Bank Account from a dropdown.
            // Here, we look up default Cash (1010) or Bank (1020).
            string bankCode = command.Method == PaymentMethod.Cash ? "1010" : "1020";
            var bankAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == bankCode);

            if (bankAccount == null) return Result<int>.Failure($"System Error: Bank Account {bankCode} not found.");

            // B. Identify Party Account (AR or AP)
            int partyAccountId = 0;
            string partyName = "";

            // We need the config map again to find the Control Accounts
            var configs = await _context.SystemConfigs.ToDictionaryAsync(k => k.Key, v => v.Value);

            if (command.Type == PaymentType.CustomerReceipt)
            {
                // We need "Accounts Receivable" (1040)
                partyAccountId = int.Parse(configs["Account.Sales.Receivable"]);
                partyName = $"Customer #{command.CustomerId}";
            }
            else
            {
                // We need "Accounts Payable" (2010)
                // Ideally check Supplier.DefaultPayableAccountId, but falling back to config for brevity
                partyAccountId = int.Parse(configs["Account.Liability.TradeCreditors"]);
                partyName = $"Supplier #{command.SupplierId}";
            }

            // C. Construct Journal Lines
            var journalLines = new List<JournalLineRequest>();

            if (command.Type == PaymentType.CustomerReceipt)
            {
                // LOGIC: Money In
                // DEBIT: Bank/Cash (Asset Increases)
                journalLines.Add(new() { AccountId = bankAccount.Id, Debit = command.Amount, Credit = 0 });

                // CREDIT: Receivable (Asset Decreases - Customer owes less)
                journalLines.Add(new() { AccountId = partyAccountId, Debit = 0, Credit = command.Amount, Note = partyName });
            }
            else
            {
                // LOGIC: Money Out
                // DEBIT: Payable (Liability Decreases - We owe less)
                journalLines.Add(new() { AccountId = partyAccountId, Debit = command.Amount, Credit = 0, Note = partyName });

                // CREDIT: Bank/Cash (Asset Decreases)
                journalLines.Add(new() { AccountId = bankAccount.Id, Debit = 0, Credit = command.Amount });
            }

            // D. Post
            var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
            {
                Date = command.Date,
                Description = $"{command.Type}: {payRef} ({command.Remarks})",
                Module = "Treasury",
                ReferenceNo = payRef,
                Lines = journalLines
            });

            if (!journalResult.Succeeded) return Result<int>.Failure($"Payment Saved, but GL Failed: {journalResult.Message}");

            return Result<int>.Success(payment.Id, "Payment Recorded & Financials Updated.");
        }
    }
}
