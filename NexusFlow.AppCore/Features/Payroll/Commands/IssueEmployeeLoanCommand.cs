using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Payroll;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Commands
{
    public class IssueEmployeeLoanCommand : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public int EmployeeId { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestRatePercentage { get; set; } // Annual Rate (e.g., 5.0 for 5%)
        public int TermInMonths { get; set; }
        public DateTime DisbursementDate { get; set; }
        public string StartDeductionMonth { get; set; } = string.Empty; // Format: "YYYY-MM"
        public DateTime FinancialDate => DisbursementDate;
    }

    public class IssueEmployeeLoanHandler : IRequestHandler<IssueEmployeeLoanCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public IssueEmployeeLoanHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(IssueEmployeeLoanCommand request, CancellationToken cancellationToken)
        {
            if (request.PrincipalAmount <= 0 || request.TermInMonths <= 0)
                return Result<int>.Failure("Principal and Term must be greater than zero.");

            decimal emiAmount = 0;
            decimal monthlyInterestRate = (request.InterestRatePercentage / 100) / 12;

            // EMI Calculation (Reducing Balance Formula)
            if (monthlyInterestRate > 0)
            {
                decimal mathPower = (decimal)Math.Pow((double)(1 + monthlyInterestRate), request.TermInMonths);
                emiAmount = request.PrincipalAmount * monthlyInterestRate * mathPower / (mathPower - 1);
            }
            else
            {
                // 0% Interest Company Loan
                emiAmount = request.PrincipalAmount / request.TermInMonths;
            }

            emiAmount = Math.Round(emiAmount, 2);

            var loan = new EmployeeLoan
            {
                EmployeeId = request.EmployeeId,
                PrincipalAmount = request.PrincipalAmount,
                InterestRatePercentage = request.InterestRatePercentage,
                TermInMonths = request.TermInMonths,
                DisbursementDate = request.DisbursementDate,
                EMIAmount = emiAmount,
                Status = LoanStatus.Active
            };

            // Generate Amortization Schedule
            decimal remainingBalance = request.PrincipalAmount;

            // Parse Start Month
            var parts = request.StartDeductionMonth.Split('-');
            var targetDate = new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), 1);

            for (int i = 1; i <= request.TermInMonths; i++)
            {
                decimal interestComponent = Math.Round(remainingBalance * monthlyInterestRate, 2);
                decimal principalComponent = emiAmount - interestComponent;

                // Handle rounding error on the final month
                if (i == request.TermInMonths)
                {
                    principalComponent = remainingBalance;
                    emiAmount = principalComponent + interestComponent;
                }

                loan.RepaymentSchedules.Add(new LoanRepaymentSchedule
                {
                    InstallmentNumber = i,
                    TargetMonth = targetDate.ToString("yyyy-MM"),
                    PrincipalComponent = principalComponent,
                    InterestComponent = interestComponent,
                    TotalInstallment = emiAmount,
                    IsPaid = false
                });

                remainingBalance -= principalComponent;
                targetDate = targetDate.AddMonths(1);
            }

            _context.EmployeeLoans.Add(loan);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(loan.Id, $"Loan issued successfully. Monthly EMI: LKR {emiAmount}");
        }
    }
}
