using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
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
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string TradeName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string TaxRegNo { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BusinessRegNo { get; set; } = string.Empty;
        #endregion

        #region 2. Categorization
        public int SupplierGroupId { get; set; }
        public int? RatingId { get; set; }
        #endregion

        #region 3. Communication
        [Required, MaxLength(100)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required, MaxLength(150), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid Accounts Email Format")]
        public string? AccountsEmail { get; set; } // Added the '?' and removed '= string.Empty;'

        [Required, MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Mobile { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Website { get; set; } = string.Empty;
        #endregion

        #region 4. Address & Geographic Hierarchy
        [Required, MaxLength(200)]
        public string AddressLine1 { get; set; } = string.Empty;

        [MaxLength(200)]
        public string AddressLine2 { get; set; } = string.Empty;

        // TIER-1 UPGRADE: Track full geographical hierarchy
        [Required, MaxLength(100)]
        public string Province { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string District { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string ZipCode { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Country { get; set; } = "LK";
        #endregion

        #region 5. Financial Settings
        public int? DefaultPayableAccountId { get; set; }
        public int? DefaultExpenseAccountId { get; set; }

        [Required, MaxLength(3)]
        public string CurrencyCode { get; set; } = "LKR";

        public int PaymentTermId { get; set; }
        public decimal CreditLimit { get; set; } = 0;
        #endregion

        #region 6. Banking Details (Strongly Typed)
        public int? BankId { get; set; }
        public Bank? Bank { get; set; }

        public int? BankBranchId { get; set; }
        public BankBranch? BankBranch { get; set; }

        [MaxLength(50)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string BankSwiftCode { get; set; } = string.Empty;

        [MaxLength(20)]
        public string BankIBAN { get; set; } = string.Empty;
        #endregion

        #region 7. System Control
        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        public string InternalNotes { get; set; } = string.Empty;
        #endregion
    }
}
