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
    public class SaveCustomerHandler : IRequestHandler<SaveCustomerCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public SaveCustomerHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(SaveCustomerCommand request, CancellationToken cancellationToken)
        {
            Customer customer;

            if (request.Id == 0) // CREATE
            {
                if (await _context.Customers.AnyAsync(c => c.Name == request.Name, cancellationToken))
                    return Result<int>.Failure($"Customer '{request.Name}' already exists.");

                customer = new Customer { IsActive = true };
                _context.Customers.Add(customer);
            }
            else // UPDATE
            {
                customer = await _context.Customers.FindAsync(new object[] { request.Id }, cancellationToken);
                if (customer == null) return Result<int>.Failure("Customer not found.");
            }

            // Map Fields
            customer.Name = request.Name;
            customer.TaxRegNo = request.TaxRegNo;
            customer.CustomerGroupId = request.CustomerGroupId;
            customer.PriceLevelId = request.PriceLevelId;
            customer.SalesRepId = request.SalesRepId;
            customer.ContactPerson = request.ContactPerson;
            customer.Email = request.Email;
            customer.Phone = request.Phone;
            customer.AddressLine1 = request.AddressLine1;
            customer.Province = request.Province;
            customer.District = request.District;
            customer.City = request.City;
            customer.Country = request.Country;
            customer.DefaultReceivableAccountId = request.DefaultReceivableAccountId;
            customer.DefaultRevenueAccountId = request.DefaultRevenueAccountId;
            customer.PaymentTermId = request.PaymentTermId;
            customer.CreditLimit = request.CreditLimit;
            customer.CreditPeriodDays = request.CreditPeriodDays;
            customer.BankId = request.BankId;
            customer.BankBranchId = request.BankBranchId;
            customer.BankAccountNumber = request.BankAccountNumber;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(customer.Id, $"Customer {(request.Id == 0 ? "created" : "updated")} successfully.");
        }
    }
}
