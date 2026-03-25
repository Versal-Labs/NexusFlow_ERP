using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Sales
{
    public class CustomerDto
    {
        public int Id { get; set; }

        // --- Required Fields (Matching your DB Entity) ---
        public string Name { get; set; } = string.Empty;
        public string TaxRegNo { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;

        // --- Optional Fields (Must be string? to prevent EF Core crashes) ---
        public string? TradeName { get; set; }
        public string? Mobile { get; set; }
        public string? AddressLine2 { get; set; }
        public string? BankName { get; set; }
        public string? BankBranch { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankSwiftCode { get; set; }
        public string? InternalNotes { get; set; }

        // --- Value Types ---
        public int CustomerGroupId { get; set; }
        public int PriceLevelId { get; set; }
        public int? SalesRepId { get; set; }
        public int DefaultReceivableAccountId { get; set; }
        public int DefaultRevenueAccountId { get; set; }
        public int PaymentTermId { get; set; }
        public decimal CreditLimit { get; set; }
        public int CreditPeriodDays { get; set; }
        public bool IsActive { get; set; }
    }
}
