using MediatR;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Commands
{
    public class CreateGrnCommand : IRequest<Result<int>>
    {
        public int PurchaseOrderId { get; set; }
        public int WarehouseId { get; set; }
        public DateTime DateReceived { get; set; }
        public string SupplierInvoiceNo { get; set; } = string.Empty; // Optional

        // We allow partial receiving (e.g., Ordered 1000, Received 400)
        public List<GrnItemDto> Items { get; set; } = new();
    }
}
