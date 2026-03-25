using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Customers.Queries
{
    public class GetCustomersQuery : IRequest<Result<List<CustomerDto>>> { }

    public class GetCustomersHandler : IRequestHandler<GetCustomersQuery, Result<List<CustomerDto>>>
    {
        private readonly IErpDbContext _context;

        public GetCustomersHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.Customers
                .AsNoTracking()
                .Select(c => new CustomerDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    TradeName = c.TradeName,
                    TaxRegNo = c.TaxRegNo, // Mapped to JSON 'taxRegNo'
                    CustomerGroupId = c.CustomerGroupId,
                    PriceLevelId = c.PriceLevelId,
                    SalesRepId = c.SalesRepId,
                    ContactPerson = c.ContactPerson,
                    Email = c.Email,
                    Phone = c.Phone,
                    Mobile = c.Mobile,
                    AddressLine1 = c.AddressLine1,
                    AddressLine2 = c.AddressLine2,
                    City = c.City,
                    Country = c.Country,
                    DefaultReceivableAccountId = c.DefaultReceivableAccountId,
                    DefaultRevenueAccountId = c.DefaultRevenueAccountId,
                    PaymentTermId = c.PaymentTermId,
                    CurrencyCode = c.CurrencyCode,
                    CreditLimit = c.CreditLimit,
                    CreditPeriodDays = c.CreditPeriodDays,
                    BankName = c.BankName,
                    BankBranch = c.BankBranch,
                    BankAccountNumber = c.BankAccountNumber,
                    BankSwiftCode = c.BankSwiftCode,
                    IsActive = c.IsActive,
                    InternalNotes = c.InternalNotes
                })
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return Result<List<CustomerDto>>.Success(data);
        }
    }
}
