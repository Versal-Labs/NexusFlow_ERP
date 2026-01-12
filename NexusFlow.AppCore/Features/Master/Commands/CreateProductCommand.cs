using MediatR;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Master.Commands
{
    public class CreateProductCommand : IRequest<Result<int>>
    {
        public ProductDto Product { get; set; }
    }
}
