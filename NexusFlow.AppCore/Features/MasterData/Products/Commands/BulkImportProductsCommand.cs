using MediatR;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
{
    public class BulkImportProductsCommand : IRequest<Result<int>>
    {
        public List<CreateProductCommand> Products { get; set; } = new();
    }

    public class BulkImportProductsHandler : IRequestHandler<BulkImportProductsCommand, Result<int>>
    {
        private readonly IMediator _mediator;

        public BulkImportProductsHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result<int>> Handle(BulkImportProductsCommand request, CancellationToken cancellationToken)
        {
            int successCount = 0;

            // In a real high-performance scenario, we would use bulk insert libraries (e.g. EFCore.BulkExtensions).
            // For now, looping the mediator ensures all business rules/validation in CreateProductHandler are respected.
            foreach (var productCmd in request.Products)
            {
                var result = await _mediator.Send(productCmd, cancellationToken);
                if (result.Succeeded) successCount++;
            }

            return Result<int>.Success(successCount, $"Successfully imported {successCount} products.");
        }
    }
}
