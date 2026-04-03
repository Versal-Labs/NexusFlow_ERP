using MediatR;
using NexusFlow.AppCore.DTOs.Master;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Boms.Commands
{
    public record SaveBomCommand(BomDto Payload) : IRequest<Result<int>>;

    public class SaveBomHandler : IRequestHandler<SaveBomCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public SaveBomHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(SaveBomCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Payload;
            BillOfMaterial bom;

            if (dto.Id == 0)
            {
                // ENTERPRISE GUARD: One active BOM per product variant to prevent manufacturing conflicts.
                bool exists = await _context.BillOfMaterials
                    .AnyAsync(b => b.ProductVariantId == dto.ProductVariantId, cancellationToken);

                if (exists) return Result<int>.Failure("A Bill of Materials already exists for this Product Variant.");

                bom = new BillOfMaterial();
                await _context.BillOfMaterials.AddAsync(bom, cancellationToken);
            }
            else
            {
                bom = await _context.BillOfMaterials
                    .Include(b => b.Components)
                    .FirstOrDefaultAsync(b => b.Id == dto.Id, cancellationToken);

                if (bom == null) return Result<int>.Failure("BOM not found.");
            }

            bom.Name = dto.Name;
            bom.ProductVariantId = dto.ProductVariantId;
            bom.IsActive = dto.IsActive;

            // Master-Detail Update Strategy: Wipe and Replace for exact consistency
            bom.Components.Clear();

            foreach (var comp in dto.Components)
            {
                // ENTERPRISE GUARD: Prevent infinite manufacturing loops
                if (comp.MaterialVariantId == dto.ProductVariantId)
                    return Result<int>.Failure("A finished good cannot be used as a raw material for itself!");

                bom.Components.Add(new BomComponent
                {
                    MaterialVariantId = comp.MaterialVariantId,
                    Quantity = comp.Quantity
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(bom.Id, "Bill of Materials saved successfully.");
        }
    }
}
