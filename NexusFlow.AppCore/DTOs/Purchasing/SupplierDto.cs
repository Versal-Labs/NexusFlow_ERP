using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Purchasing
{
    public class SupplierDto
    {
        public int Id { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        public string TradeName { get; set; } = string.Empty;
        [Required] public string TaxRegNo { get; set; } = string.Empty;
        public string BusinessRegNo { get; set; } = string.Empty;

        public int SupplierGroupId { get; set; }
        public int? RatingId { get; set; }

        [Required] public string ContactPerson { get; set; } = string.Empty;
        [Required, EmailAddress(ErrorMessage = "Invalid Email Format")] public string Email { get; set; } = string.Empty;
        [EmailAddress] public string AccountsEmail { get; set; } = string.Empty;

        [Required, RegularExpression(@"^\+?[0-9\s\-\(\)]{9,15}$", ErrorMessage = "Invalid Phone Number Format")]
        public string Phone { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;

        [Required] public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        [Required] public string Province { get; set; } = string.Empty;
        [Required] public string District { get; set; } = string.Empty;
        [Required] public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Country { get; set; } = "LK";

        [Required] public int? DefaultPayableAccountId { get; set; }
        public int? DefaultExpenseAccountId { get; set; }
        [Required] public string CurrencyCode { get; set; } = "LKR";
        public int PaymentTermId { get; set; }
        public decimal CreditLimit { get; set; }

        public int? BankId { get; set; }
        public int? BankBranchId { get; set; }
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankSwiftCode { get; set; } = string.Empty;
        public string BankIBAN { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public string InternalNotes { get; set; } = string.Empty;
    }
}
