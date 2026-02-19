using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("Suppliers", Schema = "Purchasing")]
    public class Supplier : AuditableEntity
    {
        #region 1. Identity & Legal
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty; // Legal Registered Name

        [MaxLength(200)]
        public string TradeName { get; set; } = string.Empty; // "Doing Business As" (DBA)

        [Required, MaxLength(50)]
        public string TaxRegNo { get; set; } = string.Empty; // VAT / GST / TIN (Critical for Compliance)

        [MaxLength(50)]
        public string BusinessRegNo { get; set; } = string.Empty; // Certificate of Incorporation No.
        #endregion

        #region 2. Categorization
        // Linked to SystemLookups (Type = "SupplierGroup") - e.g., "Raw Material", "Services", "Utilities"
        public int SupplierGroupId { get; set; }

        // Linked to SystemLookups (Type = "VendorRating") - e.g., "Gold", "Silver", "Blacklisted"
        public int? RatingId { get; set; }
        #endregion

        #region 3. Communication
        [Required, MaxLength(100)]
        public string ContactPerson { get; set; } = string.Empty; // Primary Point of Contact

        [Required, MaxLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty; // Main email for POs

        [MaxLength(150)]
        public string AccountsEmail { get; set; } = string.Empty; // Separate email for Remittance Advice

        [Required, MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Mobile { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Website { get; set; } = string.Empty;
        #endregion

        #region 4. Address (Primary Billing)
        // In complex ERPs, we often separate Billing vs Shipping addresses. 
        // For now, this is the LEGAL Billing Address.
        [Required, MaxLength(200)]
        public string AddressLine1 { get; set; } = string.Empty;

        [MaxLength(200)]
        public string AddressLine2 { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [MaxLength(100)]
        public string State { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string ZipCode { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Country { get; set; } = string.Empty; // ISO Code preferred (e.g., "USA", "LKR")
        #endregion

        #region 5. Financial & Procurement Settings
        // The GL Account (Liability) to credit when we post an Invoice (e.g., 2000 - Trade Payables)
        public int? DefaultPayableAccountId { get; set; }

        // The GL Account (Expense) to debit by default (e.g., 5000 - Purchases). 
        // Useful for Service vendors (e.g., Landlord -> Rent Expense).
        public int? DefaultExpenseAccountId { get; set; }

        [Required, MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD"; // The currency they invoice us in.

        // Linked to SystemLookups (Type = "PaymentTerm") - e.g., "Net 30", "2/10 Net 30"
        public int PaymentTermId { get; set; }

        public decimal CreditLimit { get; set; } = 0; // Max exposure allowed
        #endregion

        #region 6. Banking (For EFT/Wire Automation)
        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BankBranch { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string BankSwiftCode { get; set; } = string.Empty; // Critical for International Wires

        [MaxLength(20)]
        public string BankIBAN { get; set; } = string.Empty; // Critical for Europe
        #endregion

        #region 7. System Control
        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        public string InternalNotes { get; set; } = string.Empty; // "Strictly check quality on delivery"
        #endregion
    }
}
