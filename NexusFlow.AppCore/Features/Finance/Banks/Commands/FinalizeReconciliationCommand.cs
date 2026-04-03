using MediatR;
using NexusFlow.AppCore.Interfaces;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Banks.Commands
{
    public class FinalizeReconciliationCommand : IRequest<Result<int>>
    {
        public int BankAccountId { get; set; }
        public DateTime StatementDate { get; set; }
        public decimal StatementEndingBalance { get; set; }
        public List<int> ClearedJournalLineIds { get; set; } = new();
    }

    public class FinalizeReconciliationHandler : IRequestHandler<FinalizeReconciliationCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public FinalizeReconciliationHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(FinalizeReconciliationCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. CREATE THE RECONCILIATION RECORD
                var reconciliation = new BankReconciliation
                {
                    BankAccountId = request.BankAccountId,
                    StatementDate = request.StatementDate,
                    StatementEndingBalance = request.StatementEndingBalance,
                    IsFinalized = true
                };

                _context.BankReconciliations.Add(reconciliation);
                await _context.SaveChangesAsync(cancellationToken); // Get the ID

                // 2. FETCH THE SELECTED JOURNAL LINES
                var linesToClear = await _context.JournalLines
                    .Include(l => l.JournalEntry)
                    .Where(l => request.ClearedJournalLineIds.Contains(l.Id) && l.AccountId == request.BankAccountId)
                    .ToListAsync(cancellationToken);

                if (linesToClear.Count != request.ClearedJournalLineIds.Count)
                    return Result<int>.Failure("One or more selected transactions are invalid or do not belong to this bank account.");

                // 3. LOCK THE LEDGER LINES
                var clearedReferenceNumbers = new HashSet<string>();
                foreach (var line in linesToClear)
                {
                    if (line.IsCleared) return Result<int>.Failure($"Transaction {line.JournalEntry.ReferenceNo} is already cleared.");

                    line.IsCleared = true;
                    line.BankReconciliationId = reconciliation.Id;

                    // Store reference numbers to trace back to the cheques
                    clearedReferenceNumbers.Add(line.JournalEntry.ReferenceNo);
                }

                // 4. THE ENTERPRISE CHEQUE CLEARING ENGINE
                // Find any cheques that were deposited to this bank account and match the cleared journal references
                var depositedCheques = await _context.ChequeRegisters
                    .Where(c => c.DepositedBankAccountId == request.BankAccountId
                             && c.Status == ChequeStatus.Deposited)
                    .ToListAsync(cancellationToken);

                int chequesClearedCount = 0;
                foreach (var cheque in depositedCheques)
                {
                    // Remember: In DepositChequeHandler, we set the Journal Ref as "DEP-{ChequeNumber}"
                    string expectedDepRef = $"DEP-{cheque.ChequeNumber}";

                    if (clearedReferenceNumbers.Contains(expectedDepRef))
                    {
                        cheque.Status = ChequeStatus.Cleared;
                        chequesClearedCount++;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(reconciliation.Id, $"Bank Reconciliation finalized! Ledger locked and {chequesClearedCount} deposited cheques successfully marked as Cleared.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Reconciliation failed: {ex.Message}");
            }
        }
    }
}
