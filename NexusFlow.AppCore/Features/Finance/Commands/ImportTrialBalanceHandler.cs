using MediatR;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Commands
{
    public class TbImportDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class ImportTrialBalanceCommand : IRequest<Result<string>>
    {
        public DateTime CutoverDate { get; set; }
        public List<TbImportDto> Lines { get; set; } = new();
    }

    public class ImportTrialBalanceHandler : IRequestHandler<ImportTrialBalanceCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;

        public ImportTrialBalanceHandler(IErpDbContext context, IJournalService journalService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _accountResolver = accountResolver;
        }

        public async Task<Result<string>> Handle(ImportTrialBalanceCommand request, CancellationToken cancellationToken)
        {
            if (!request.Lines.Any()) return Result<string>.Failure("No Trial Balance lines provided.");

            int obeAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Equity.OpeningBalance", cancellationToken);
            if (obeAccountId == 0) return Result<string>.Failure("CRITICAL: OBE account missing from System Config.");

            // TIER-1 ERP GUARD: Ensure the uploaded TB balances!
            decimal totalDebit = request.Lines.Sum(l => l.Debit);
            decimal totalCredit = request.Lines.Sum(l => l.Credit);

            if (Math.Round(totalDebit, 2) != Math.Round(totalCredit, 2))
                return Result<string>.Failure($"The uploaded Trial Balance does not balance! Debits: {totalDebit:C}, Credits: {totalCredit:C}");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            string migrationRef = $"MIG-TB-{DateTime.UtcNow:yyyyMMddHHmmss}";

            try
            {
                var accountsByCode = await _context.Accounts.ToDictionaryAsync(a => a.Code, a => a, cancellationToken);
                var journalLines = new List<JournalLineRequest>();

                foreach (var line in request.Lines)
                {
                    if (line.Debit == 0 && line.Credit == 0) continue;

                    if (!accountsByCode.TryGetValue(line.AccountCode.Trim(), out var account))
                        throw new Exception($"Account Code '{line.AccountCode}' does not exist in NexusFlow Chart of Accounts.");

                    // IMPORTANT: Skip Accounts Receivable, Accounts Payable, and Inventory accounts
                    // because we already migrated those through the Sub-Ledger wizards!
                    if (account.Name.Contains("Receivable") || account.Name.Contains("Payable") || account.Name.Contains("Inventory"))
                        throw new Exception($"Do not import '{account.Name}' ({account.Code}) in the Trial Balance upload. These must be migrated via their respective Sub-Ledger wizards to prevent corruption.");

                    journalLines.Add(new JournalLineRequest
                    {
                        AccountId = account.Id,
                        Debit = line.Debit,
                        Credit = line.Credit,
                        Note = "Legacy Trial Balance Cutover"
                    });
                }

                // Offset the entire entry against Opening Balance Equity
                // If Debits > Credits in the sub-ledger exclusions, the OBE will naturally balance it.
                decimal netOffset = totalDebit - totalCredit; // Will be 0 if everything is included, but because we skipped AR/AP/INV, there will be an offset.

                // Instead of forcing a math offset, the safest Tier-1 approach is to let the user map the old system's OBE/Retained earnings 
                // directly into our OBE account via the spreadsheet. 

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.CutoverDate,
                    Description = $"Final GL Trial Balance Cutover",
                    Module = "Finance",
                    ReferenceNo = migrationRef,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception(jResult.Message);

                await transaction.CommitAsync(cancellationToken);
                return Result<string>.Success($"Successfully posted Trial Balance Cutover. Please check the 'Opening Balance Equity' account to ensure it is now $0.00.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<string>.Failure($"Trial Balance Migration Failed: {ex.Message}");
            }
        }
    }
}
