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
    public class GetPurchaseOrderByIdQuery : IRequest<Result<PurchaseOrderDto>>
    {
        public int Id { get; set; }
    }

    public class GetPurchaseOrderByIdHandler : IRequestHandler<GetPurchaseOrderByIdQuery, Result<PurchaseOrderDto>>
    {
        private readonly IErpDbContext _context;

        public GetPurchaseOrderByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<PurchaseOrderDto>> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var po = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Supplier)
                .Include(p => p.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (po == null) return Result<PurchaseOrderDto>.Failure("PO not found.");

            var dto = new PurchaseOrderDto
            {
                Id = po.Id,
                PoNumber = po.PoNumber,
                Date = po.Date,
                ExpectedDate = po.ExpectedDate ?? po.Date, // Fallback
                SupplierId = po.SupplierId,
                SupplierName = po.Supplier?.Name ?? "Unknown",
                Status = po.Status.ToString(),
                TotalAmount = po.TotalAmount,
                Note = po.Note ?? string.Empty,
                Items = po.Items.Select(i => new PurchaseOrderItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,

                    // TIER-1 FIX: Safe null navigation using '?.' and fallback string
                    ProductName = i.ProductVariant?.Product != null
                        ? $"{i.ProductVariant.Product.Name} ({i.ProductVariant.Size}/{i.ProductVariant.Color})"
                        : "Unknown Product",

                    SKU = i.ProductVariant?.SKU ?? "N/A",
                    QuantityOrdered = i.QuantityOrdered,
                    QuantityReceived = i.QuantityReceived,
                    UnitCost = i.UnitCost
                }).ToList()
            };

            return Result<PurchaseOrderDto>.Success(dto);
        }
    }
}
