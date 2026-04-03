using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Commands
{
    public record ChangeSalesOrderStatusCommand(int OrderId, SalesOrderStatus NewStatus) : IRequest<Result<int>>;

    public class ChangeSalesOrderStatusHandler : IRequestHandler<ChangeSalesOrderStatusCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public ChangeSalesOrderStatusHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(ChangeSalesOrderStatusCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.SalesOrders.FindAsync(new object[] { request.OrderId }, cancellationToken);
            if (order == null) return Result<int>.Failure("Sales Order not found.");

            // ENTERPRISE GUARD: Prevent altering documents that are already finalized
            if (order.Status == SalesOrderStatus.Converted || order.Status == SalesOrderStatus.Cancelled)
                return Result<int>.Failure("Cannot alter a Sales Order that has already been Converted to an Invoice or Cancelled.");

            order.Status = request.NewStatus;
            await _context.SaveChangesAsync(cancellationToken);

            string action = request.NewStatus == SalesOrderStatus.Draft ? "revoked to Draft" : "submitted to Back-Office";
            return Result<int>.Success(order.Id, $"Order {order.OrderNumber} successfully {action}.");
        }
    }
}
