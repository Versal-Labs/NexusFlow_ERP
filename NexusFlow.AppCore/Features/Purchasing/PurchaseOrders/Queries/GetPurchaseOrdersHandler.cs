using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Queries
{
    public class GetPurchaseOrdersQuery : IRequest<Result<List<PurchaseOrderDto>>>
    {
        // Optional: Add filters here later (e.g., SupplierId, FromDate, ToDate)
    }

    // 2. The Handler Logic
    public class GetPurchaseOrdersHandler : IRequestHandler<GetPurchaseOrdersQuery, Result<List<PurchaseOrderDto>>>
    {
        private readonly IErpDbContext _context;

        public GetPurchaseOrdersHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<PurchaseOrderDto>>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
        {
            var list = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Supplier) // Join Supplier to get the Name
                .OrderByDescending(p => p.Id) // Show newest first
                .Select(p => new PurchaseOrderDto
                {
                    Id = p.Id,
                    PoNumber = p.PoNumber,
                    Date = p.Date,
                    SupplierId = p.SupplierId,
                    SupplierName = p.Supplier.Name, // Mapped from Include
                    Status = p.Status.ToString(),   // Enum to String
                    TotalAmount = p.TotalAmount,
                    Note = p.Note ?? string.Empty,

                    // We typically don't load line items for the main grid to keep it fast.
                    // Items are loaded only when clicking "View/Edit".
                    Items = new List<PurchaseOrderItemDto>()
                })
                .ToListAsync(cancellationToken);

            return Result<List<PurchaseOrderDto>>.Success(list);
        }
    }
}
