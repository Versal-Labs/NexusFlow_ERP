using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Queries
{
    public class SalesOrderDetailsDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<SalesOrderItemDetailsDto> Items { get; set; } = new();
    }

    public class SalesOrderItemDetailsDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal LineTotal { get; set; }
    }

    // CQRS Query
    public class GetSalesOrderByIdQuery : IRequest<Result<SalesOrderDetailsDto>>
    {
        public int OrderId { get; set; }
    }

    // CQRS Handler
    public class GetSalesOrderByIdHandler : IRequestHandler<GetSalesOrderByIdQuery, Result<SalesOrderDetailsDto>>
    {
        private readonly IErpDbContext _context;

        public GetSalesOrderByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<SalesOrderDetailsDto>> Handle(GetSalesOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var order = await _context.SalesOrders
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(v => v.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

            if (order == null) return Result<SalesOrderDetailsDto>.Failure("Order not found.");

            var dto = new SalesOrderDetailsDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.TotalAmount,
                Items = order.Items.Select(i => new SalesOrderItemDetailsDto
                {
                    // Format beautifully: [SKU] Product Name
                    Description = i.ProductVariant?.Product?.Name != null ? $"[{i.ProductVariant.SKU}] {i.ProductVariant.Product.Name}" : "Unknown Item",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount,
                    LineTotal = (i.Quantity * i.UnitPrice) - i.Discount
                }).ToList()
            };

            return Result<SalesOrderDetailsDto>.Success(dto);
        }
    }
}
