using MediatR;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
{
    public class UpdateProductCommand : IRequest<Result<int>>
    {
        // We reuse the ProductDto, but ensure ID is populated
        public ProductDto Product { get; set; }
    }

    public class DeleteProductCommand : IRequest<Result<bool>>
    {
        public int Id { get; set; }
    }
}
