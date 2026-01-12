using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Finance
{
    public class BalanceSheetReport
    {
        public DateTime AsOfDate { get; set; }

        public BalanceSheetSection Assets { get; set; } = new();
        public BalanceSheetSection Liabilities { get; set; } = new();
        public BalanceSheetSection Equity { get; set; } = new();

        public bool IsBalanced => Math.Abs(Assets.Total - (Liabilities.Total + Equity.Total)) < 0.01m;
    }

    public class BalanceSheetSection
    {
        public List<TrialBalanceLine> Accounts { get; set; } = new();
        public decimal Total { get; set; }
    }
}
