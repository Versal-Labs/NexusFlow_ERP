using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Finance
{
    public class TrialBalanceReport
    {
        public DateTime AsOfDate { get; set; }
        public List<TrialBalanceLine> Lines { get; set; } = new();

        // The "Control Totals" (Must match)
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }

        public bool IsBalanced => TotalDebit == TotalCredit;
    }

    public class TrialBalanceLine
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty; // Asset, Liability, etc.

        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        // Net Balance (Positive = Debit, Negative = Credit)
        public decimal NetBalance => Debit - Credit;
    }
}
