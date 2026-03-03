using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Warehouses.Commands
{
    public class SaveWarehouseHandler : IRequestHandler<SaveWarehouseCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public SaveWarehouseHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(SaveWarehouseCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Warehouse;

            // Integrity Check: Clean up Subcontractor Data
            if (dto.Type != WarehouseType.Subcontractor)
            {
                dto.LinkedSupplierId = null; // Enforce data consistency
            }

            // Check for duplicate Code
            bool codeExists = await _context.Warehouses
                .AnyAsync(w => w.Code == dto.Code && w.Id != dto.Id, cancellationToken);
            if (codeExists) return Result<int>.Failure($"Warehouse Code '{dto.Code}' is already in use.");

            Warehouse warehouse;

            if (dto.Id == 0) // CREATE
            {
                warehouse = new Warehouse();
                _context.Warehouses.Add(warehouse);
            }
            else // UPDATE
            {
                warehouse = await _context.Warehouses.FindAsync(new object[] { dto.Id }, cancellationToken);
                if (warehouse == null) return Result<int>.Failure("Warehouse not found.");
            }

            // Map Data
            warehouse.Code = dto.Code;
            warehouse.Name = dto.Name;
            warehouse.Location = dto.Location;
            warehouse.ManagerName = dto.ManagerName;
            warehouse.Type = dto.Type;
            warehouse.LinkedSupplierId = dto.LinkedSupplierId;
            warehouse.OverrideInventoryAccountId = dto.OverrideInventoryAccountId;
            warehouse.IsActive = dto.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(warehouse.Id, "Warehouse saved successfully.");
        }
    }
}
