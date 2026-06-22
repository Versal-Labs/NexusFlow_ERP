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
            if (dto.BasisQuantity <= 0) return Result<int>.Failure("BOM basis quantity must be greater than zero.");
            if (dto.Components.Count == 0 || dto.Components.Any(x => x.Quantity <= 0))
                return Result<int>.Failure("BOM components must contain positive quantities.");
            if (dto.Components.Any(x => x.MaterialVariantId == dto.ProductVariantId))
                return Result<int>.Failure("A finished good cannot be used as its own material.");
            if (dto.Components.Select(x => x.MaterialVariantId).Distinct().Count() != dto.Components.Count)
                return Result<int>.Failure("Duplicate material variants are not allowed in one BOM revision.");

            BillOfMaterial bom;

            if (dto.Id == 0)
            {
                // Only one approved revision may drive newly released production at a time.
                bool exists = await _context.BillOfMaterials
                    .AnyAsync(b => b.ProductVariantId == dto.ProductVariantId && b.IsActive && b.IsApproved, cancellationToken);

                if (exists) return Result<int>.Failure("An active approved BOM already exists. Edit it to create the next revision.");

                bom = NewRevision(dto, 1);
                await _context.BillOfMaterials.AddAsync(bom, cancellationToken);
            }
            else
            {
                var previous = await _context.BillOfMaterials
                    .Include(b => b.Components)
                    .FirstOrDefaultAsync(b => b.Id == dto.Id, cancellationToken);

                if (previous == null) return Result<int>.Failure("BOM not found.");
                if (dto.RowVersion.Length > 0 && !previous.RowVersion.SequenceEqual(dto.RowVersion))
                    return Result<int>.Failure("This BOM was changed by another user. Reload it before creating a revision.");

                // Approved recipes are immutable: editing creates a new revision while released orders keep their snapshot.
                previous.IsActive = false;
                previous.EffectiveTo = DateTime.UtcNow.Date.AddDays(-1);
                bom = NewRevision(dto, previous.RevisionNumber + 1);
                await _context.BillOfMaterials.AddAsync(bom, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(bom.Id, $"BOM revision {bom.RevisionNumber} approved successfully.");

            BillOfMaterial NewRevision(BomDto source, int revision)
            {
                var entity = new BillOfMaterial
                {
                    Name = source.Name,
                    ProductVariantId = source.ProductVariantId,
                    IsActive = source.IsActive,
                    RevisionNumber = revision,
                    BasisQuantity = source.BasisQuantity,
                    EffectiveFrom = source.EffectiveFrom.Date,
                    IsApproved = true,
                    ApprovedAtUtc = DateTime.UtcNow
                };
                foreach (var component in source.Components)
                {
                    entity.Components.Add(new BomComponent { MaterialVariantId = component.MaterialVariantId, Quantity = component.Quantity });
                }
                return entity;
            }
        }
    }
}
