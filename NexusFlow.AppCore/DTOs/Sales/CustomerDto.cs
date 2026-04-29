using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Sales
{
    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TradeName { get; set; } = string.Empty;
        public string TaxRegNo { get; set; } = string.Empty;

        public int CustomerGroupId { get; set; }
        public int PriceLevelId { get; set; }
        public int? SalesRepId { get; set; }

        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;

        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;

        // TIER-1 UPGRADE: Geographical Hierarchy
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public int DefaultReceivableAccountId { get; set; }
        public int DefaultRevenueAccountId { get; set; }
        public int PaymentTermId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public int CreditPeriodDays { get; set; }

        // TIER-1 UPGRADE: Strongly Typed Banks
        public int? BankId { get; set; }
        public int? BankBranchId { get; set; }
        public string BankAccountNumber { get; set; } = string.Empty;

        public bool IsActive { get; set; }
        public string InternalNotes { get; set; } = string.Empty;
    }
}
