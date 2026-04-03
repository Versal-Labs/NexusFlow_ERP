using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Banks.Queries
{
    // ==========================================
    // 1. GET BEGINNING BALANCE (From last finalized reconciliation)
    // ==========================================
    public record GetBeginningBalanceQuery(int BankAccountId) : IRequest<Result<decimal>>;

    public class GetBeginningBalanceHandler : IRequestHandler<GetBeginningBalanceQuery, Result<decimal>>
    {
        private readonly IErpDbContext _context;
        public GetBeginningBalanceHandler(IErpDbContext context) => _context = context;

        public async Task<Result<decimal>> Handle(GetBeginningBalanceQuery request, CancellationToken cancellationToken)
        {
            var lastRecon = await _context.BankReconciliations
                .Where(r => r.BankAccountId == request.BankAccountId && r.IsFinalized)
                .OrderByDescending(r => r.StatementDate)
                .FirstOrDefaultAsync(cancellationToken);

            return Result<decimal>.Success(lastRecon?.StatementEndingBalance ?? 0m);
        }
    }

    // ==========================================
    // 2. GET UNCLEARED TRANSACTIONS (The Split-Board Data)
    // ==========================================
    public class UnclearedTransactionDto
    {
        public int JournalLineId { get; set; }
        public DateTime Date { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "IN" or "OUT"
    }

    public record GetUnclearedTransactionsQuery(int BankAccountId, DateTime StatementDate) : IRequest<Result<List<UnclearedTransactionDto>>>;

    public class GetUnclearedTransactionsHandler : IRequestHandler<GetUnclearedTransactionsQuery, Result<List<UnclearedTransactionDto>>>
    {
        private readonly IErpDbContext _context;
        public GetUnclearedTransactionsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<UnclearedTransactionDto>>> Handle(GetUnclearedTransactionsQuery request, CancellationToken cancellationToken)
        {
            var lines = await _context.JournalLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountId == request.BankAccountId
                         && !l.IsCleared
                         && l.JournalEntry.Date.Date <= request.StatementDate.Date)
                .OrderBy(l => l.JournalEntry.Date)
                .Select(l => new UnclearedTransactionDto
                {
                    JournalLineId = l.Id,
                    Date = l.JournalEntry.Date,
                    ReferenceNo = l.JournalEntry.ReferenceNo,
                    Description = l.Description,
                    Amount = l.Debit > 0 ? l.Debit : l.Credit,
                    Type = l.Debit > 0 ? "IN" : "OUT" // Debit to Bank = Money In. Credit to Bank = Money Out.
                })
                .ToListAsync(cancellationToken);

            return Result<List<UnclearedTransactionDto>>.Success(lines);
        }
    }

    // ==========================================
    // 3. ON-THE-FLY BANK ADJUSTMENTS (Fees & Interest)
    // ==========================================
    public record PostBankAdjustmentCommand(int BankAccountId, DateTime Date, decimal Amount, string Type, string Reference, string Note) : IRequest<Result<int>>;

    public class PostBankAdjustmentHandler : IRequestHandler<PostBankAdjustmentCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;

        public PostBankAdjustmentHandler(IErpDbContext context, IJournalService journalService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(PostBankAdjustmentCommand request, CancellationToken cancellationToken)
        {
            if (request.Amount <= 0) return Result<int>.Failure("Amount must be greater than zero.");

            int offsetAccountId = 0;
            var journalLines = new List<JournalLineRequest>();

            if (request.Type == "FEE")
            {
                offsetAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Expense.BankFees", cancellationToken);
                if (offsetAccountId == 0) return Result<int>.Failure("Bank Fee Expense account not configured.");

                // Bank Fee: Credit Bank, Debit Expense
                journalLines.Add(new JournalLineRequest { AccountId = offsetAccountId, Debit = request.Amount, Credit = 0, Note = request.Note });
                journalLines.Add(new JournalLineRequest { AccountId = request.BankAccountId, Debit = 0, Credit = request.Amount, Note = request.Note });
            }
            else if (request.Type == "INTEREST")
            {
                offsetAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Revenue.InterestIncome", cancellationToken);
                if (offsetAccountId == 0) return Result<int>.Failure("Interest Income account not configured.");

                // Interest: Debit Bank, Credit Revenue
                journalLines.Add(new JournalLineRequest { AccountId = request.BankAccountId, Debit = request.Amount, Credit = 0, Note = request.Note });
                journalLines.Add(new JournalLineRequest { AccountId = offsetAccountId, Debit = 0, Credit = request.Amount, Note = request.Note });
            }

            var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
            {
                Date = request.Date,
                Description = $"Bank Adjustment: {request.Note}",
                Module = "Treasury",
                ReferenceNo = request.Reference,
                Lines = journalLines
            });

            if (!jResult.Succeeded) return Result<int>.Failure($"Adjustment Failed: {jResult.Message}");

            return Result<int>.Success(jResult.Data, "Adjustment posted successfully.");
        }
    }
}
