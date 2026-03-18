using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("Customers", Schema = "Sales")]
    public class Customer : AuditableEntity
    {
        #region 1. Identity & Legal
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string TradeName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string TaxRegNo { get; set; } = string.Empty;
        #endregion

        #region 2. Categorization & Ownership
        public int CustomerGroupId { get; set; }
        public int PriceLevelId { get; set; }

        // Links to the Employee/User who manages this account (For Commission Ledger later)
        public int? SalesRepId { get; set; }
        #endregion

        #region 3. Communication
        [Required, MaxLength(100)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required, MaxLength(150), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Mobile { get; set; } = string.Empty;
        #endregion

        #region 4. Address (Primary Billing)
        [Required, MaxLength(200)]
        public string AddressLine1 { get; set; } = string.Empty;

        [MaxLength(200)]
        public string AddressLine2 { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Country { get; set; } = "LK";
        #endregion

        #region 5. Financial Mappings
        public int DefaultReceivableAccountId { get; set; }
        public int DefaultRevenueAccountId { get; set; }
        public int PaymentTermId { get; set; }

        [Required, MaxLength(3)]
        public string CurrencyCode { get; set; } = "LKR";

        public decimal CreditLimit { get; set; } = 0;

        // Explicit Credit Period in Days (Overrides Payment Term if needed for aging reports)
        public int CreditPeriodDays { get; set; } = 0;
        #endregion

        #region 6. Banking Details (For Direct Debits / Refunds)
        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BankBranch { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string BankSwiftCode { get; set; } = string.Empty;
        #endregion

        #region 7. System Control
        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        public string InternalNotes { get; set; } = string.Empty;
        #endregion
    }
}
