using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum CommissionRuleType
    {
        GlobalFlatRate = 1,  // Applies to the entire invoice subtotal
        CategoryBased = 2    // Applies only to specific product categories
    }
}
