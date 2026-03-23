using MediatR;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Commands
{
    public class ConvertOrderToInvoiceCommand : IRequest<Result<int>>
    {
        public int SalesOrderId { get; set; }
        public int WarehouseId { get; set; } // The physical location shipping the goods

        public ConvertOrderToInvoiceCommand(int salesOrderId, int warehouseId)
        {
            SalesOrderId = salesOrderId;
            WarehouseId = warehouseId;
        }
    }
}
