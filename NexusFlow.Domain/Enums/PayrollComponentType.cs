using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum PayrollComponentType
    {
        Allowance = 1,     // Adds to Gross Pay (e.g., Food, Transport)
        Deduction = 2,     // Subtracts from Net Pay (e.g., Fines, Damages)
        Statutory = 3      // Legal/Tax (e.g., PAYE/APIT, Stamp Duty)
    }

    public enum CalculationType
    {
        FixedAmount = 1,        // Flat LKR 5000 per month
        PerAttendanceDay = 2,   // LKR 500 * (Days Present)
        PercentageOfBasic = 3,  // 10% of Basic Salary
        ManualEntry = 4         // Hand-typed by HR during the Draft phase (e.g., one-off bonus)
    }

    public enum LoanStatus
    {
        Active = 1,   // Currently being deducted monthly
        Cleared = 2,  // Fully paid off
        Defaulted = 3 // Employee left without paying
    }
}
