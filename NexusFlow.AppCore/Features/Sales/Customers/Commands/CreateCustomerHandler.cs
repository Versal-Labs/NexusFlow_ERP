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
            var legacyCode = string.IsNullOrWhiteSpace(request.LegacyCustomerCode) ? null : request.LegacyCustomerCode.Trim().ToUpperInvariant();
            if (legacyCode?.Length > 50) return Result<int>.Failure("Legacy customer code must not exceed 50 characters.");
            if (legacyCode != null && await _context.Customers.AnyAsync(c => c.Id != request.Id && c.LegacyCustomerCode == legacyCode, cancellationToken))
                return Result<int>.Failure("Legacy customer code is already assigned to another customer.");
            if (request.Id != 0 && (await _context.CustomerOpeningItems.AnyAsync(x => x.CustomerId == request.Id && x.MigrationBatch.Status == "Posted", cancellationToken)
                || await _context.ChequeRegisters.AnyAsync(x => x.CustomerId == request.Id && x.MigrationBatchId != null && x.MigrationBatch!.Status == "Posted", cancellationToken)))
            {
                var existing = await _context.Customers.FindAsync(new object[] { request.Id }, cancellationToken);
                if (existing != null && (existing.LegacyCustomerCode != legacyCode || existing.DefaultReceivableAccountId != request.DefaultReceivableAccountId))
                    return Result<int>.Failure("Legacy code and AR account are locked after opening migration. Use a reviewed financial correction.");
            }
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
            customer.LegacyCustomerCode = legacyCode;
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
            customer.DetailsIncomplete = new[] { customer.Email, customer.ContactPerson, customer.Phone, customer.AddressLine1, customer.TaxRegNo }.Any(string.IsNullOrWhiteSpace);

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(customer.Id, $"Customer {(request.Id == 0 ? "created" : "updated")} successfully.");
        }
    }
}
