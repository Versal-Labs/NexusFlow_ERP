using MediatR;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Warehouses.Queries
{
    public class GetWarehousesQuery : IRequest<Result<List<WarehouseDto>>> { }

    public class GetWarehouseByIdQuery : IRequest<Result<WarehouseDto>>
    {
        public int Id { get; set; }
    }
}
