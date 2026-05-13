using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Jobs.Interfaces;
using NexusFlow.Domain.Entities.Payroll;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Jobs
{
    public class GenerateDraftPayrollJob : IGenerateDraftPayrollJob
    {
        private readonly IErpDbContext _context;
        private readonly ILogger<GenerateDraftPayrollJob> _logger;

        public GenerateDraftPayrollJob(IErpDbContext context, ILogger<GenerateDraftPayrollJob> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ExecuteAsync(int year, int month, CancellationToken cancellationToken = default)
        {
            string monthYearStr = $"{year}-{month:D2}"; // e.g., "2026-05"
            _logger.LogInformation($"Generating Draft Payroll for {monthYearStr}");

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // 1. IDEMPOTENCY CHECK
            var existingPeriod = await _context.PayrollPeriods
                .Include(p => p.Slips).ThenInclude(s => s.LineItems)
                .FirstOrDefaultAsync(p => p.MonthYear == monthYearStr, cancellationToken);

            if (existingPeriod != null)
            {
                if (existingPeriod.Status >= PayrollPeriodStatus.Posted)
                {
                    _logger.LogError($"Cannot regenerate payroll for {monthYearStr}. It is already Posted/Paid.");
                    return; // SAFEGUARD: Never overwrite a finalized payroll!
                }

                // If it's still a Draft, delete the old slips to make room for the new calculation
                _context.PayrollSlips.RemoveRange(existingPeriod.Slips);
                _context.PayrollPeriods.Remove(existingPeriod);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // 2. CREATE NEW PERIOD
            var period = new PayrollPeriod
            {
                MonthYear = monthYearStr,
                StartDate = startDate,
                EndDate = endDate,
                Status = PayrollPeriodStatus.Draft
            };
            _context.PayrollPeriods.Add(period);
            await _context.SaveChangesAsync(cancellationToken); // Save to get ID

            // 3. FETCH GLOBAL DATA
            var employees = await _context.Employees.ToListAsync(cancellationToken);
            var globalComponents = await _context.PayrollComponents.Where(c => c.IsActive).ToListAsync(cancellationToken);
            var employeeComponents = await _context.EmployeePayrollComponents.Where(c => c.IsActive).ToListAsync(cancellationToken);

            var attendanceRecords = await _context.DailyAttendanceRecords
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .ToListAsync(cancellationToken);

            // Fetch ReadyToPay Commissions
            var unpdaidCommissions = await _context.CommissionLedgers
                .Where(c => c.Status == CommissionStatus.ReadyToPay)
                .ToListAsync(cancellationToken);

            // Fetch Loans & Advances targeted for this month
            var loanSchedules = await _context.LoanRepaymentSchedules
                .Where(l => l.TargetMonth == monthYearStr && !l.IsPaid)
                .ToListAsync(cancellationToken);

            var advances = await _context.SalaryAdvances
                .Where(a => a.DeductionMonth == monthYearStr && !a.IsDeducted)
                .ToListAsync(cancellationToken);

            var newSlips = new List<PayrollSlip>();

            // 4. THE PAYROLL ENGINE (Per Employee Loop)
            foreach (var emp in employees)
            {
                var slip = new PayrollSlip { PayrollPeriodId = period.Id, EmployeeId = emp.Id };

                // --- NEW: PRORATION ENGINE ---
                int daysInMonth = DateTime.DaysInMonth(year, month);

                // Determine the employee's active window within this specific month
                DateTime activeStartDate = emp.HireDate > startDate ? emp.HireDate.Date : startDate;
                DateTime activeEndDate = (emp.ResignationDate.HasValue && emp.ResignationDate.Value < endDate)
                                         ? emp.ResignationDate.Value.Date
                                         : endDate;

                int activeDays = 0;
                if (activeStartDate <= activeEndDate)
                {
                    activeDays = (activeEndDate - activeStartDate).Days + 1;
                }

                // The magical multiplier (e.g., 15 days active / 31 days in month = 0.4838)
                decimal prorationFactor = (decimal)activeDays / daysInMonth;

                // If they weren't active at all this month (e.g. resigned months ago), skip them entirely!
                if (prorationFactor <= 0) continue;

                // A. BASIC SALARY (Prorated)
                decimal actualBasicSalary = Math.Round(emp.BasicSalary * prorationFactor, 2);
                decimal epfBaseAmount = actualBasicSalary;

                string basicDesc = prorationFactor < 1.0m
                    ? $"Basic Salary (Prorated: {activeDays}/{daysInMonth} days)"
                    : "Basic Salary";

                // A. BASIC SALARY
                slip.LineItems.Add(new PayrollSlipLineItem { Description = basicDesc, Amount = actualBasicSalary, Type = 1 });
                slip.GrossBasic = actualBasicSalary;

                // B. ALLOWANCES & DEDUCTIONS (Config Engine)
                var empSpecificConfigs = employeeComponents.Where(c => c.EmployeeId == emp.Id).ToList();

                foreach (var config in empSpecificConfigs)
                {
                    var component = globalComponents.FirstOrDefault(c => c.Id == config.PayrollComponentId);
                    if (component == null) continue;

                    decimal rate = config.OverrideRate ?? component.DefaultRate;
                    decimal calculatedAmount = 0;

                    if (component.CalculationType == CalculationType.FixedAmount)
                    {
                        calculatedAmount = Math.Round(rate * prorationFactor, 2);
                    }
                    else if (component.CalculationType == CalculationType.PerAttendanceDay)
                    {
                        int daysPresent = attendanceRecords.Count(a => a.EmployeeId == emp.Id && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.HalfDay));
                        calculatedAmount = rate * daysPresent;
                    }
                    else if (component.CalculationType == CalculationType.PercentageOfBasic)
                    {
                        calculatedAmount = Math.Round(actualBasicSalary * (rate / 100), 2);
                    }

                    if (calculatedAmount > 0)
                    {
                        slip.LineItems.Add(new PayrollSlipLineItem
                        {
                            Description = component.Name,
                            Amount = calculatedAmount,
                            Type = component.Type == PayrollComponentType.Allowance ? 1 : 2
                        });

                        if (component.Type == PayrollComponentType.Allowance) slip.TotalAllowances += calculatedAmount;
                        else slip.TotalDeductions += calculatedAmount;

                        if (component.IsEPFCalculable && component.Type == PayrollComponentType.Allowance)
                        {
                            epfBaseAmount += calculatedAmount; // Add to statutory base
                        }
                    }
                }

                // C. COMMISSIONS (For Sales Reps)
                var empCommissions = unpdaidCommissions.Where(c => c.SalesRepId == emp.Id).Sum(c => c.CommissionAmount);
                if (empCommissions > 0)
                {
                    slip.LineItems.Add(new PayrollSlipLineItem { Description = "Sales Commission", Amount = empCommissions, Type = 1 });
                    slip.TotalAllowances += empCommissions;
                }

                // D. LOANS AND ADVANCES
                var empLoanEMI = loanSchedules.Where(l => l.EmployeeLoan.EmployeeId == emp.Id).Sum(l => l.TotalInstallment);
                if (empLoanEMI > 0)
                {
                    slip.LineItems.Add(new PayrollSlipLineItem { Description = "Loan Repayment (EMI)", Amount = empLoanEMI, Type = 2 });
                    slip.TotalDeductions += empLoanEMI;
                }

                var empAdvance = advances.Where(a => a.EmployeeId == emp.Id).Sum(a => a.Amount);
                if (empAdvance > 0)
                {
                    slip.LineItems.Add(new PayrollSlipLineItem { Description = "Salary Advance Deduction", Amount = empAdvance, Type = 2 });
                    slip.TotalDeductions += empAdvance;
                }

                // E. STATUTORY (EPF / ETF) - Sri Lanka Labor Law
                // Employee EPF (8% Deduction)
                decimal employeeEpf = epfBaseAmount * 0.08m;
                slip.LineItems.Add(new PayrollSlipLineItem { Description = "EPF Deduction (8%)", Amount = employeeEpf, Type = 2 });
                slip.TotalDeductions += employeeEpf;

                // Employer EPF (12%) & ETF (3%) - Company Liability
                slip.EmployerEPF = epfBaseAmount * 0.12m;
                slip.EmployerETF = epfBaseAmount * 0.03m;

                // F. FINALIZE NET PAY
                slip.NetPay = (slip.GrossBasic + slip.TotalAllowances) - slip.TotalDeductions;
                newSlips.Add(slip);
            }

            // 5. SAVE ALL DRAFTS
            await _context.PayrollSlips.AddRangeAsync(newSlips, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Draft Payroll generated successfully for {newSlips.Count} employees.");
        }
    }
}
