using MediatR;
using NexusFlow.AppCore.DTOs.Inventory;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class TransferStockCommand : IRequest<Result>
    {
        public int SourceWarehouseId { get; set; }
        public int TargetWarehouseId { get; set; } // e.g., Factory
        public string ReferenceDoc { get; set; } = string.Empty; // e.g., "TRF-2024-001"

        // List of Items to Move
        public List<TransferItemDto> Items { get; set; } = new();
    }
}
