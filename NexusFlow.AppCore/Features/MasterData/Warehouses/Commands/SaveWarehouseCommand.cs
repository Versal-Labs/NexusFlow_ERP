using MediatR;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Warehouses.Commands
{
    public class SaveWarehouseCommand : IRequest<Result<int>>
    {
        public WarehouseDto Warehouse { get; set; } = null!;
    }
}
