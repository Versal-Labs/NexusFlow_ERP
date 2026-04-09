using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Reports.Queries
{
    // ==========================================
    // 1. DTOs
    // ==========================================
    public class AccountBalanceDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    public class ProfitAndLossDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Basis { get; set; } = string.Empty;

        public List<AccountBalanceDto> RevenueAccounts { get; set; } = new();
        public List<AccountBalanceDto> CogsAccounts { get; set; } = new();
        public List<AccountBalanceDto> ExpenseAccounts { get; set; } = new();

        public decimal TotalRevenue => RevenueAccounts.Sum(a => a.Balance);
        public decimal TotalCogs => CogsAccounts.Sum(a => a.Balance);
        public decimal GrossProfit => TotalRevenue - TotalCogs;
        public decimal TotalExpenses => ExpenseAccounts.Sum(a => a.Balance);
        public decimal NetIncome => GrossProfit - TotalExpenses;
    }

    public class GetProfitAndLossQuery : IRequest<Result<ProfitAndLossDto>>
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Basis { get; set; } = "Accrual"; // "Accrual" or "Cash"
    }

    // ==========================================
    // 2. THE ENGINE
    // ==========================================
    public class GetProfitAndLossHandler : IRequestHandler<GetProfitAndLossQuery, Result<ProfitAndLossDto>>
    {
        private readonly IErpDbContext _context;
        public GetProfitAndLossHandler(IErpDbContext context) => _context = context;

        public async Task<Result<ProfitAndLossDto>> Handle(GetProfitAndLossQuery request, CancellationToken cancellationToken)
        {
            var report = new ProfitAndLossDto { StartDate = request.StartDate, EndDate = request.EndDate, Basis = request.Basis };

            // Temporary dictionaries to aggregate balances in memory before DTO mapping
            var revDict = new Dictionary<int, AccountBalanceDto>();
            var expDict = new Dictionary<int, AccountBalanceDto>();

            if (request.Basis.Equals("Accrual", StringComparison.OrdinalIgnoreCase))
            {
                // ---------------------------------------------------------
                // ACCRUAL BASIS: Simply read the posted GL lines
                // ---------------------------------------------------------
                var lines = await _context.JournalLines
                    .Include(jl => jl.Account)
                    .Include(jl => jl.JournalEntry)
                    .Where(jl => jl.JournalEntry.Date.Date >= request.StartDate.Date && jl.JournalEntry.Date.Date <= request.EndDate.Date)
                    .Where(jl => jl.Account.Type == AccountType.Revenue || jl.Account.Type == AccountType.Expense)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                foreach (var jl in lines)
                {
                    if (jl.Account.Type == AccountType.Revenue)
                    {
                        if (!revDict.ContainsKey(jl.AccountId)) revDict[jl.AccountId] = new AccountBalanceDto { AccountCode = jl.Account.Code, AccountName = jl.Account.Name, Balance = 0 };
                        revDict[jl.AccountId].Balance += (jl.Credit - jl.Debit); // Revenue is Credit Normal
                    }
                    else if (jl.Account.Type == AccountType.Expense)
                    {
                        if (!expDict.ContainsKey(jl.AccountId)) expDict[jl.AccountId] = new AccountBalanceDto { AccountCode = jl.Account.Code, AccountName = jl.Account.Name, Balance = 0 };
                        expDict[jl.AccountId].Balance += (jl.Debit - jl.Credit); // Expense is Debit Normal
                    }
                }
            }
            else if (request.Basis.Equals("Cash", StringComparison.OrdinalIgnoreCase))
            {
                // ---------------------------------------------------------
                // CASH BASIS: The Dedicated Tracing Engine
                // ---------------------------------------------------------

                // 1. Fetch Receipts/Payments that occurred in the date range
                var cashTxns = await _context.PaymentTransactions
                    .Include(p => p.Allocations)
                        .ThenInclude(a => a.SalesInvoice) // For AR
                    .Include(p => p.Allocations)
                        .ThenInclude(a => a.SupplierBill) // For AP
                    .Where(p => !p.IsVoided && p.Date.Date >= request.StartDate.Date && p.Date.Date <= request.EndDate.Date)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                // Fetch all related Journal Entries for the underlying Invoices/Bills in one go to prevent N+1 queries
                var invoiceRefs = cashTxns.SelectMany(p => p.Allocations.Where(a => a.SalesInvoice != null).Select(a => a.SalesInvoice!.InvoiceNumber)).ToList();
                var billRefs = cashTxns.SelectMany(p => p.Allocations.Where(a => a.SupplierBill != null).Select(a => a.SupplierBill!.BillNumber)).ToList();
                var allRefs = invoiceRefs.Concat(billRefs).Distinct().ToList();

                var originalJournalEntries = await _context.JournalEntries
                    .Include(je => je.Lines).ThenInclude(l => l.Account)
                    .Where(je => allRefs.Contains(je.ReferenceNo))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                foreach (var txn in cashTxns)
                {
                    foreach (var alloc in txn.Allocations)
                    {
                        if (alloc.SalesInvoice != null)
                        {
                            // REVENUE TRACING
                            var inv = alloc.SalesInvoice;
                            if (inv.GrandTotal == 0) continue;

                            decimal paymentRatio = alloc.AmountAllocated / inv.GrandTotal;
                            var invJe = originalJournalEntries.FirstOrDefault(je => je.ReferenceNo == inv.InvoiceNumber);

                            if (invJe != null)
                            {
                                var revLines = invJe.Lines.Where(l => l.Account.Type == AccountType.Revenue);
                                foreach (var line in revLines)
                                {
                                    decimal recognizedRev = (line.Credit - line.Debit) * paymentRatio;
                                    if (recognizedRev == 0) continue;

                                    if (!revDict.ContainsKey(line.AccountId)) revDict[line.AccountId] = new AccountBalanceDto { AccountCode = line.Account.Code, AccountName = line.Account.Name, Balance = 0 };
                                    revDict[line.AccountId].Balance += recognizedRev;
                                }
                            }
                        }
                        else if (alloc.SupplierBill != null)
                        {
                            // EXPENSE TRACING
                            var bill = alloc.SupplierBill;
                            if (bill.GrandTotal == 0) continue;

                            decimal paymentRatio = alloc.AmountAllocated / bill.GrandTotal;
                            var billJe = originalJournalEntries.FirstOrDefault(je => je.ReferenceNo == bill.BillNumber);

                            if (billJe != null)
                            {
                                var expLines = billJe.Lines.Where(l => l.Account.Type == AccountType.Expense);
                                foreach (var line in expLines)
                                {
                                    decimal recognizedExp = (line.Debit - line.Credit) * paymentRatio;
                                    if (recognizedExp == 0) continue;

                                    if (!expDict.ContainsKey(line.AccountId)) expDict[line.AccountId] = new AccountBalanceDto { AccountCode = line.Account.Code, AccountName = line.Account.Name, Balance = 0 };
                                    expDict[line.AccountId].Balance += recognizedExp;
                                }
                            }
                        }
                    }
                }

                // 2. Add Direct Cash Expenses/Revenues (Entries hitting Bank + Rev/Exp directly without an invoice)
                // 2. Add Direct Cash Expenses/Revenues (Entries hitting Bank + Rev/Exp directly without an invoice)
                var directLines = await _context.JournalLines
                    .Include(jl => jl.Account)
                    .Include(jl => jl.JournalEntry) // STOP HERE. Do not fetch the sibling lines to avoid the cycle!
                    .Where(jl => jl.JournalEntry.Date.Date >= request.StartDate.Date && jl.JournalEntry.Date.Date <= request.EndDate.Date)
                    .Where(jl => jl.Account.Type == AccountType.Revenue || jl.Account.Type == AccountType.Expense)
                    // Ensure the entry also hits an Asset/Liability (Bank) but wasn't mapped through AP/AR sub-ledgers
                    .Where(jl => jl.JournalEntry.Module != "Sales" && jl.JournalEntry.Module != "Purchasing")
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                foreach (var jl in directLines)
                {
                    if (jl.Account.Type == AccountType.Revenue)
                    {
                        if (!revDict.ContainsKey(jl.AccountId)) revDict[jl.AccountId] = new AccountBalanceDto { AccountCode = jl.Account.Code, AccountName = jl.Account.Name, Balance = 0 };
                        revDict[jl.AccountId].Balance += (jl.Credit - jl.Debit);
                    }
                    else if (jl.Account.Type == AccountType.Expense)
                    {
                        if (!expDict.ContainsKey(jl.AccountId)) expDict[jl.AccountId] = new AccountBalanceDto { AccountCode = jl.Account.Code, AccountName = jl.Account.Name, Balance = 0 };
                        expDict[jl.AccountId].Balance += (jl.Debit - jl.Credit);
                    }
                }
            }
            else
            {
                return Result<ProfitAndLossDto>.Failure("Invalid Report Basis specified.");
            }

            // 3. Map Dictionaries to DTO Lists and Sort
            report.RevenueAccounts = revDict.Values.Where(v => v.Balance != 0).OrderBy(a => a.AccountCode).ToList();

            // Optional: Split COGS out of Expenses if your accounts start with a specific number (e.g., '5')
            var allExpenses = expDict.Values.Where(v => v.Balance != 0).ToList();
            report.CogsAccounts = allExpenses.Where(a => a.AccountCode.StartsWith("5")).OrderBy(a => a.AccountCode).ToList();
            report.ExpenseAccounts = allExpenses.Where(a => !a.AccountCode.StartsWith("5")).OrderBy(a => a.AccountCode).ToList();

            return Result<ProfitAndLossDto>.Success(report);
        }
    }
}
