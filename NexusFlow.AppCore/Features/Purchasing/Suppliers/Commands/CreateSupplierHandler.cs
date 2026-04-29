using MediatR;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Suppliers.Commands
{
    public class CreateSupplierCommand : IRequest<Result<int>> { public SupplierDto Supplier { get; set; } }
    public class UpdateSupplierCommand : IRequest<Result<int>> { public SupplierDto Supplier { get; set; } }

    // HANDLER
    public class SupplierCommandHandler :
        IRequestHandler<CreateSupplierCommand, Result<int>>,
        IRequestHandler<UpdateSupplierCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public SupplierCommandHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(CreateSupplierCommand request, CancellationToken token)
        {
            var dto = request.Supplier;
            var entity = new Supplier();
            MapDtoToEntity(dto, entity);

            _context.Suppliers.Add(entity);
            await _context.SaveChangesAsync(token);
            return Result<int>.Success(entity.Id, "Supplier Created");
        }

        public async Task<Result<int>> Handle(UpdateSupplierCommand request, CancellationToken token)
        {
            var dto = request.Supplier;
            var entity = await _context.Suppliers.FindAsync(new object[] { dto.Id }, token);
            if (entity == null) return Result<int>.Failure("Supplier not found");

            MapDtoToEntity(dto, entity);
            await _context.SaveChangesAsync(token);
            return Result<int>.Success(entity.Id, "Supplier Updated");
        }

        private void MapDtoToEntity(SupplierDto dto, Supplier entity)
        {
            entity.Name = dto.Name;
            entity.TradeName = dto.TradeName;
            entity.TaxRegNo = dto.TaxRegNo;
            entity.BusinessRegNo = dto.BusinessRegNo;
            entity.SupplierGroupId = dto.SupplierGroupId;
            entity.RatingId = dto.RatingId;
            entity.ContactPerson = dto.ContactPerson;
            entity.Email = dto.Email;
            entity.AccountsEmail = dto.AccountsEmail;
            entity.Phone = dto.Phone;
            entity.Mobile = dto.Mobile;
            entity.Website = dto.Website;
            entity.AddressLine1 = dto.AddressLine1;
            entity.AddressLine2 = dto.AddressLine2;

            // MAPPED LOCATIONS
            entity.Province = dto.Province;
            entity.District = dto.District;
            entity.City = dto.City;
            entity.ZipCode = dto.ZipCode;
            entity.Country = dto.Country;

            entity.DefaultPayableAccountId = dto.DefaultPayableAccountId;
            entity.DefaultExpenseAccountId = dto.DefaultExpenseAccountId;
            entity.CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "LKR" : dto.CurrencyCode;
            entity.PaymentTermId = dto.PaymentTermId;
            entity.CreditLimit = dto.CreditLimit;

            // MAPPED BANKS
            entity.BankId = dto.BankId;
            entity.BankBranchId = dto.BankBranchId;
            entity.BankAccountNumber = dto.BankAccountNumber;
            entity.BankSwiftCode = dto.BankSwiftCode;
            entity.BankIBAN = dto.BankIBAN;

            entity.IsActive = dto.IsActive;
            entity.InternalNotes = dto.InternalNotes;
        }
    }
}
