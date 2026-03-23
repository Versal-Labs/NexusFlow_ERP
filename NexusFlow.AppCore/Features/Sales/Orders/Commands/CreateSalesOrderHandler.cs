using MediatR;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Commands
{
    // Make sure to define a SalesOrderDto matching these properties in your DTOs folder
    public class CreateSalesOrderCommand : IRequest<Result<int>>
    {
        public SalesOrderDto Order { get; set; }
        public CreateSalesOrderCommand(SalesOrderDto order) => Order = order;
    }

    public class CreateSalesOrderHandler : IRequestHandler<CreateSalesOrderCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;

        public CreateSalesOrderHandler(IErpDbContext context, INumberSequenceService sequenceService)
        {
            _context = context;
            _sequenceService = sequenceService;
        }

        public async Task<Result<int>> Handle(CreateSalesOrderCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Order;

            // 1. Validation
            var customer = await _context.Customers.FindAsync(new object[] { dto.CustomerId }, cancellationToken);
            if (customer == null) return Result<int>.Failure("Customer not found.");

            var rep = await _context.Employees.FindAsync(new object[] { dto.SalesRepId }, cancellationToken);
            if (rep == null || !rep.IsSalesRep) return Result<int>.Failure("Invalid Sales Representative.");

            if (!dto.Items.Any()) return Result<int>.Failure("Order must contain at least one item.");

            // 2. Generate Number & Map
            var order = new SalesOrder
            {
                OrderNumber = await _sequenceService.GenerateNextNumberAsync("ORD", cancellationToken),
                OrderDate = dto.OrderDate,
                CustomerId = dto.CustomerId,
                SalesRepId = dto.SalesRepId,
                Status = dto.IsDraft ? SalesOrderStatus.Draft : SalesOrderStatus.Submitted,
                Notes = dto.Notes,
                TotalAmount = 0
            };

            // 3. Process Items
            foreach (var item in dto.Items)
            {
                if (item.Quantity <= 0) continue;

                decimal lineTotal = (item.Quantity * item.UnitPrice) - item.Discount;

                order.Items.Add(new SalesOrderItem
                {
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Discount = item.Discount,
                    LineTotal = lineTotal
                });

                order.TotalAmount += lineTotal;
            }

            _context.SalesOrders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            string statusText = order.Status == SalesOrderStatus.Draft ? "saved as draft" : "submitted to Back-Office";
            return Result<int>.Success(order.Id, $"Sales Order {order.OrderNumber} {statusText}.");
        }
    }
}
