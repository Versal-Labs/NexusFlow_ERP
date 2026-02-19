using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Suppliers.Queries
{
    public class GetSuppliersQuery : IRequest<Result<List<SupplierDto>>>
    {
        // Optional: Add filters here later (e.g., bool IsActiveOnly = true)
    }

    public class SupplierQueryHandler :
        IRequestHandler<GetSuppliersQuery, Result<List<SupplierDto>>>,
        IRequestHandler<GetSupplierByIdQuery, Result<SupplierDto>>
    {
        private readonly IErpDbContext _context;

        public SupplierQueryHandler(IErpDbContext context)
        {
            _context = context;
        }

        // --- GET ALL ---
        public async Task<Result<List<SupplierDto>>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
        {
            // Note: We don't map every single field for the list view to improve performance.
            // We only map what is needed for the Grid.
            var list = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(s => new SupplierDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    TaxRegNo = s.TaxRegNo,
                    IsActive = s.IsActive,
                    // If you have a SupplierGroup table, include it to get the name
                    // SupplierGroupName = s.SupplierGroup.Value 
                })
                .ToListAsync(cancellationToken);

            return Result<List<SupplierDto>>.Success(list);
        }

        // --- GET BY ID ---
        public async Task<Result<SupplierDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
        {
            var s = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (s == null) return Result<SupplierDto>.Failure("Supplier not found.");

            // Full Mapping for the Edit Form
            var dto = new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                TradeName = s.TradeName,
                TaxRegNo = s.TaxRegNo,
                BusinessRegNo = s.BusinessRegNo,

                SupplierGroupId = s.SupplierGroupId,
                RatingId = s.RatingId,

                ContactPerson = s.ContactPerson,
                Email = s.Email,
                AccountsEmail = s.AccountsEmail,
                Phone = s.Phone,
                Mobile = s.Mobile,
                Website = s.Website,

                AddressLine1 = s.AddressLine1,
                AddressLine2 = s.AddressLine2,
                City = s.City,
                State = s.State,
                ZipCode = s.ZipCode,
                Country = s.Country,

                DefaultPayableAccountId = s.DefaultPayableAccountId,
                DefaultExpenseAccountId = s.DefaultExpenseAccountId,
                CurrencyCode = s.CurrencyCode,
                PaymentTermId = s.PaymentTermId,
                CreditLimit = s.CreditLimit,

                BankName = s.BankName,
                BankBranch = s.BankBranch,
                BankAccountNumber = s.BankAccountNumber,
                BankSwiftCode = s.BankSwiftCode,
                BankIBAN = s.BankIBAN,

                IsActive = s.IsActive,
                InternalNotes = s.InternalNotes
            };

            return Result<SupplierDto>.Success(dto);
        }
    }
}
