using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace NexusFlow.AppCore.Features.Sales.Customers.Commands
{
    public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public CreateCustomerHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            var exists = await _context.Customers.AnyAsync(
                c => string.Equals(c.Name, request.Name, StringComparison.OrdinalIgnoreCase),
                cancellationToken);
            if (exists) return Result<int>.Failure($"Customer with name '{request.Name}' already exists.");

            var customer = new Customer
            {
                Name = request.Name,
                TaxRegNo = request.TaxRegNo,
                CustomerGroupId = request.CustomerGroupId,
                PriceLevelId = request.PriceLevelId,
                SalesRepId = request.SalesRepId,

                ContactPerson = request.ContactPerson,
                Email = request.Email,
                Phone = request.Phone,

                AddressLine1 = request.AddressLine1,
                City = request.City,
                Country = request.Country,

                DefaultReceivableAccountId = request.DefaultReceivableAccountId,
                DefaultRevenueAccountId = request.DefaultRevenueAccountId,
                PaymentTermId = request.PaymentTermId,
                CreditLimit = request.CreditLimit,
                CreditPeriodDays = request.CreditPeriodDays,

                BankName = request.BankName,
                BankBranch = request.BankBranch,
                BankAccountNumber = request.BankAccountNumber,
                BankSwiftCode = request.BankSwiftCode,

                IsActive = true
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(customer.Id, "Customer created successfully.");
        }
    }
}
