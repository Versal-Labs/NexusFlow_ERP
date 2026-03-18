using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Purchasing
{
    public class SupplierDto
    {
        public int Id { get; set; }

        // --- 1. Identity ---
        [Required] public string Name { get; set; } = string.Empty;
        public string TradeName { get; set; } = string.Empty;
        [Required] public string TaxRegNo { get; set; } = string.Empty;
        public string BusinessRegNo { get; set; } = string.Empty;

        // --- 2. Categorization ---
        public int SupplierGroupId { get; set; } // Dropdown
        public string? SupplierGroupName { get; set; } // For Grid Display
        public int? RatingId { get; set; }

        // --- 3. Contact ---
        public string ContactPerson { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        public string AccountsEmail { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;

        // --- 4. Address ---
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        // --- 5. Financials ---
        public int? DefaultPayableAccountId { get; set; }
        public int? DefaultExpenseAccountId { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public int PaymentTermId { get; set; }
        public decimal CreditLimit { get; set; }

        // --- 6. Banking ---
        public string BankName { get; set; } = string.Empty;
        public string BankBranch { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankSwiftCode { get; set; } = string.Empty;
        public string BankIBAN { get; set; } = string.Empty;

        // --- 7. System ---
        public bool IsActive { get; set; } = true;
        public string InternalNotes { get; set; } = string.Empty;
    }
}
