using MediatR;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class RunProductionCommand : IRequest<Result<int>>
    {
        // What did we make?
        public int FinishedGoodVariantId { get; set; }

        // How many?
        public decimal QtyProduced { get; set; } // e.g., 100 Jeans

        // Where did the raw materials come from?
        public int FactoryWarehouseId { get; set; }

        // Where do we put the finished jeans?
        public int TargetWarehouseId { get; set; }

        // Subcontracting Cost (Sewing Charge) - e.g., $500 total
        public decimal TotalServiceCost { get; set; }

        public string ReferenceDoc { get; set; } = string.Empty; // e.g., "PROD-001"
    }
}
