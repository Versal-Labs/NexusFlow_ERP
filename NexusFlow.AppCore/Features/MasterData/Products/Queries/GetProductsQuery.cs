using MediatR;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Queries
{
    public class GetProductsQuery : IRequest<Result<List<ProductDto>>>
    {
        // Optional: Add filtering here later (e.g. public string SearchTerm { get; set; })
    }

    public class GetProductByIdQuery : IRequest<Result<ProductDto>>
    {
        public int Id { get; set; }
    }
}
