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
    public class SaveBomCommand : IRequest<Result<int>>
    {
        public BomDto Payload { get; set; }

        public SaveBomCommand(BomDto payload)
        {
            Payload = payload;
        }
    }

    public class SaveBomHandler : IRequestHandler<SaveBomCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public SaveBomHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(SaveBomCommand request, CancellationToken cancellationToken)
        {
            var payload = request.Payload;
            BillOfMaterial bom;

            if (payload.Id > 0)
            {
                bom = await _context.BillOfMaterials
                    .Include(b => b.Components)
                    .FirstOrDefaultAsync(b => b.Id == payload.Id, cancellationToken);

                if (bom == null) return Result<int>.Failure("BOM not found.");

                bom.Name = payload.Name;
                bom.ProductVariantId = payload.ProductVariantId;
                bom.IsActive = payload.IsActive;

                // Handle Component removal and updates
                _context.BomComponents.RemoveRange(bom.Components.Where(c => !payload.Components.Any(pc => pc.Id == c.Id)));

                foreach (var comp in payload.Components)
                {
                    var existingComp = bom.Components.FirstOrDefault(c => c.Id == comp.Id);
                    if (existingComp != null)
                    {
                        existingComp.MaterialVariantId = comp.MaterialVariantId;
                        existingComp.Quantity = comp.Quantity;
                    }
                    else
                    {
                        bom.Components.Add(new BomComponent
                        {
                            MaterialVariantId = comp.MaterialVariantId,
                            Quantity = comp.Quantity
                        });
                    }
                }
            }
            else
            {
                bom = new BillOfMaterial
                {
                    Name = payload.Name,
                    ProductVariantId = payload.ProductVariantId,
                    IsActive = payload.IsActive,
                    Components = payload.Components.Select(c => new BomComponent
                    {
                        MaterialVariantId = c.MaterialVariantId,
                        Quantity = c.Quantity
                    }).ToList()
                };

                await _context.BillOfMaterials.AddAsync(bom, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(bom.Id, "BOM saved successfully.");
        }
    }
}
