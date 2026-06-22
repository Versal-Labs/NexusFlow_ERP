using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Payroll;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Commands
{
    public class PostPayrollCommand : IRequest<Result<string>>, IFinancialPeriodControlledRequest
    {
        public int PayrollPeriodId { get; set; }
        public DateTime PostingDate { get; set; } = DateTime.UtcNow.Date;
        public DateTime FinancialDate => PostingDate;
    }

    public class PostPayrollHandler : IRequestHandler<PostPayrollCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public PostPayrollHandler(IErpDbContext context, IJournalService journalService, INumberSequenceService sequenceService, IFinancialAccountResolver accountResolver)
        {
            _context = context;
            _journalService = journalService;
            _sequenceService = sequenceService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<string>> Handle(PostPayrollCommand request, CancellationToken cancellationToken)
        {
            var period = await _context.PayrollPeriods
                .Include(p => p.Slips)
                    .ThenInclude(s => s.LineItems)
                .FirstOrDefaultAsync(p => p.Id == request.PayrollPeriodId, cancellationToken);

            if (period == null) return Result<string>.Failure("Payroll period not found.");
            if (period.Status >= PayrollPeriodStatus.Posted) return Result<string>.Failure("This payroll has already been posted.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. UPDATE EXTERNAL MODULES (Loans, Advances, Commissions)
                var employeeIds = period.Slips.Select(s => s.EmployeeId).ToList();

                // Mark Loans as Paid
                var loanSchedules = await _context.LoanRepaymentSchedules
                    .Where(l => l.TargetMonth == period.MonthYear && !l.IsPaid && employeeIds.Contains(l.EmployeeLoan.EmployeeId))
                    .ToListAsync(cancellationToken);
                foreach (var schedule in loanSchedules) { schedule.IsPaid = true; }

                // Mark Advances as Deducted
                var advances = await _context.SalaryAdvances
                    .Where(a => a.DeductionMonth == period.MonthYear && !a.IsDeducted && employeeIds.Contains(a.EmployeeId))
                    .ToListAsync(cancellationToken);
                foreach (var advance in advances) { advance.IsDeducted = true; }

                // Mark Commissions as Paid
                var commissions = await _context.CommissionLedgers
                    .Where(c => c.Status == CommissionStatus.ReadyToPay && employeeIds.Contains(c.SalesRepId))
                    .ToListAsync(cancellationToken);
                foreach (var comm in commissions) { comm.Status = CommissionStatus.Paid; }


                // 2. BUILD THE GENERAL LEDGER JOURNAL ENTRY
                int basicSalaryExpenseAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.BasicSalaryExpense, cancellationToken);
                int epfExpenseAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.EmployerEpfExpense, cancellationToken);
                int etfExpenseAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.EmployerEtfExpense, cancellationToken);
                int epfPayableAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.EpfPayable, cancellationToken);
                int etfPayableAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.EtfPayable, cancellationToken);
                int netSalariesPayableAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.NetSalariesPayable, cancellationToken);
                int loanReceivableAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.EmployeeLoansReceivable, cancellationToken);
                int advanceReceivableAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.SalaryAdvancesReceivable, cancellationToken);

                var journalLines = new List<JournalLineRequest>();

                decimal totalGrossBasic = period.Slips.Sum(s => s.GrossBasic);
                decimal totalEmployerEpf = period.Slips.Sum(s => s.EmployerEPF);
                decimal totalEmployerEtf = period.Slips.Sum(s => s.EmployerETF);
                decimal totalEmployeeEpf = period.Slips.SelectMany(s => s.LineItems).Where(l => l.Description.Contains("EPF Deduction")).Sum(l => l.Amount);
                decimal totalNetPay = period.Slips.Sum(s => s.NetPay);

                decimal totalLoans = period.Slips.SelectMany(s => s.LineItems).Where(l => l.Description.Contains("Loan")).Sum(l => l.Amount);
                decimal totalAdvances = period.Slips.SelectMany(s => s.LineItems).Where(l => l.Description.Contains("Advance")).Sum(l => l.Amount);
                decimal totalCommissions = period.Slips.SelectMany(s => s.LineItems).Where(l => l.Description.Contains("Commission")).Sum(l => l.Amount);

                // --- DEBITS (Expenses) ---
                journalLines.Add(new JournalLineRequest { AccountId = basicSalaryExpenseAcc, Debit = totalGrossBasic, Credit = 0, Note = "Basic Salaries" });
                journalLines.Add(new JournalLineRequest { AccountId = epfExpenseAcc, Debit = totalEmployerEpf, Credit = 0, Note = "Employer EPF 12%" });
                journalLines.Add(new JournalLineRequest { AccountId = etfExpenseAcc, Debit = totalEmployerEtf, Credit = 0, Note = "Employer ETF 3%" });

                if (totalCommissions > 0)
                {
                    int commExpenseAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.CommissionExpense, cancellationToken);
                    journalLines.Add(new JournalLineRequest { AccountId = commExpenseAcc, Debit = totalCommissions, Credit = 0, Note = "Sales Commissions" });
                }

                // Add configured Allowances (Dynamically grouping by the component's configured Debit account)
                // Note: In a full implementation, you'd join with PayrollComponent here to get the exact GL accounts.
                // For brevity, assuming other allowances go to a general allowance expense.
                decimal otherAllowances = period.Slips.Sum(s => s.TotalAllowances) - totalCommissions;
                if (otherAllowances > 0)
                {
                    int allowExpenseAcc = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.AllowanceExpense, cancellationToken);
                    journalLines.Add(new JournalLineRequest { AccountId = allowExpenseAcc, Debit = otherAllowances, Credit = 0, Note = "Other Allowances" });
                }

                // --- CREDITS (Liabilities & Asset Reductions) ---
                journalLines.Add(new JournalLineRequest { AccountId = epfPayableAcc, Debit = 0, Credit = (totalEmployerEpf + totalEmployeeEpf), Note = "Total EPF Payable (20%)" });
                journalLines.Add(new JournalLineRequest { AccountId = etfPayableAcc, Debit = 0, Credit = totalEmployerEtf, Note = "Total ETF Payable (3%)" });
                journalLines.Add(new JournalLineRequest { AccountId = netSalariesPayableAcc, Debit = 0, Credit = totalNetPay, Note = "Net Salaries Payable" });

                if (totalLoans > 0)
                    journalLines.Add(new JournalLineRequest { AccountId = loanReceivableAcc, Debit = 0, Credit = totalLoans, Note = "Loan Recoveries" });

                if (totalAdvances > 0)
                    journalLines.Add(new JournalLineRequest { AccountId = advanceReceivableAcc, Debit = 0, Credit = totalAdvances, Note = "Advance Recoveries" });

                // 3. POST THE JOURNAL
                string jeRef = $"PR-{period.MonthYear.Replace("-", "")}";
                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.PostingDate,
                    Description = $"Payroll Posting for {period.MonthYear}",
                    Module = "Payroll",
                    ReferenceNo = jeRef,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Posting Failed: {jResult.Message}");

                // 4. UPDATE PAYROLL STATUS
                period.Status = PayrollPeriodStatus.Posted;
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return Result<string>.Success(jeRef, "Payroll Approved and Posted successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<string>.Failure($"Posting failed: {ex.Message}");
            }
        }
    }
}
