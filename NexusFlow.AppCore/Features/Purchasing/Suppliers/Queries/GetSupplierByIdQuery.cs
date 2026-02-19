using MediatR;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Suppliers.Queries
{
    public class GetSupplierByIdQuery : IRequest<Result<SupplierDto>>
    {
        public int Id { get; set; }
    }
}
