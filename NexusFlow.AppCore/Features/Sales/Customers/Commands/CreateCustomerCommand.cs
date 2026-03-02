using MediatR;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Customers.Commands
{
    public class CreateCustomerCommand : IRequest<Result<int>>
    {
        public string Name { get; set; } = string.Empty;
        public string TaxRegNo { get; set; } = string.Empty;
        public int CustomerGroupId { get; set; }
        public int PriceLevelId { get; set; }
        public int? SalesRepId { get; set; }

        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public string AddressLine1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public int DefaultReceivableAccountId { get; set; }
        public int DefaultRevenueAccountId { get; set; }
        public int PaymentTermId { get; set; }
        public decimal CreditLimit { get; set; }
        public int CreditPeriodDays { get; set; }

        public string BankName { get; set; } = string.Empty;
        public string BankBranch { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankSwiftCode { get; set; } = string.Empty;
    }
}
