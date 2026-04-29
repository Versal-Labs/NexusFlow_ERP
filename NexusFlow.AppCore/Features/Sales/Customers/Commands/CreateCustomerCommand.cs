using MediatR;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Customers.Commands
{
    public class SaveCustomerCommand : IRequest<Result<int>>
    {
        public int Id { get; set; } // 0 for Create, >0 for Update

        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string TaxRegNo { get; set; } = string.Empty;
        public int CustomerGroupId { get; set; }
        public int PriceLevelId { get; set; }
        public int? SalesRepId { get; set; }

        [Required] public string ContactPerson { get; set; } = string.Empty;

        // STRICT VALIDATIONS
        [Required, EmailAddress(ErrorMessage = "Invalid Email Format")]
        public string Email { get; set; } = string.Empty;

        [Required, RegularExpression(@"^\+?[0-9\s\-\(\)]{9,15}$", ErrorMessage = "Invalid Phone Number Format")]
        public string Phone { get; set; } = string.Empty;

        [Required] public string AddressLine1 { get; set; } = string.Empty;
        [Required] public string Province { get; set; } = string.Empty;
        [Required] public string District { get; set; } = string.Empty;
        [Required] public string City { get; set; } = string.Empty;
        public string Country { get; set; } = "LK";

        public int DefaultReceivableAccountId { get; set; }
        public int DefaultRevenueAccountId { get; set; }
        public int PaymentTermId { get; set; }
        public decimal CreditLimit { get; set; }
        public int CreditPeriodDays { get; set; }

        // STRONGLY TYPED BANKS
        public int? BankId { get; set; }
        public int? BankBranchId { get; set; }
        public string BankAccountNumber { get; set; } = string.Empty;
    }
}
