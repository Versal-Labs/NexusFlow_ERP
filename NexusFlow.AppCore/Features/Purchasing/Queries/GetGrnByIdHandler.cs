using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Queries
{
    public class GetGrnByIdQuery : IRequest<Result<GrnDetailsDto>> { public int Id { get; set; } }

    public class GrnDetailsDto : GrnDto
    {
        public List<GrnItemDto> Items { get; set; } = new();
    }

    public class GetGrnByIdHandler : IRequestHandler<GetGrnByIdQuery, Result<GrnDetailsDto>>
    {
        private readonly IErpDbContext _context;
        public GetGrnByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<GrnDetailsDto>> Handle(GetGrnByIdQuery request, CancellationToken cancellationToken)
        {
            var grn = await _context.GRNs
                .Include(g => g.PurchaseOrder).ThenInclude(po => po.Supplier)
                .Include(g => g.Warehouse)
                .Include(g => g.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == request.Id);

            if (grn == null) return Result<GrnDetailsDto>.Failure("GRN not found.");

            var dto = new GrnDetailsDto
            {
                Id = grn.Id,
                GrnNumber = grn.GrnNumber,
                PoNumber = grn.PurchaseOrder.PoNumber,
                ReceiptDate = grn.ReceivedDate,
                SupplierName = grn.PurchaseOrder.Supplier.Name,
                WarehouseName = grn.Warehouse.Name,
                ReferenceNo = grn.SupplierInvoiceNo,
                TotalValue = grn.TotalAmount,
                Items = grn.Items.Select(i => new GrnItemDto
                {
                    ProductVariantId = i.ProductVariantId,

                    // Safe null-checking
                    ProductName = i.ProductVariant?.Product != null
                ? $"{i.ProductVariant.Product.Name} ({i.ProductVariant.Size}/{i.ProductVariant.Color})"
                : "Unknown Product",

                    SKU = i.ProductVariant?.SKU ?? "N/A",
                    QuantityReceived = i.QuantityReceived,
                    UnitCost = i.UnitCost
                }).ToList()
            };

            return Result<GrnDetailsDto>.Success(dto);
        }
    }
}
