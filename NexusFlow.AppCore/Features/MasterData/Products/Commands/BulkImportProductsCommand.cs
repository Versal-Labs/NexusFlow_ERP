using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
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
        private readonly IErpDbContext _context;

        public BulkImportProductsHandler(IErpDbContext context, IMediator mediator)
        {
            _mediator = mediator;
            _context = context;
        }

        public async Task<Result<int>> Handle(BulkImportProductsCommand request, CancellationToken cancellationToken)
        {
            int successCount = 0;

            // ATOMIC TRANSACTION: All or Nothing
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var productCmd in request.Products)
                {
                    // Send to the existing CreateProductHandler to enforce all business rules
                    var result = await _mediator.Send(productCmd, cancellationToken);

                    if (!result.Succeeded)
                    {
                        // Fail Fast: Rollback everything and tell the user exactly which row broke
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<int>.Failure($"Import aborted. Row {successCount + 1} ('{productCmd.Product.Name}') failed: {string.Join(", ", result.Message)}");
                    }

                    successCount++;
                }

                // If we get here, all rows passed validation
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(successCount, $"Successfully imported {successCount} products.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Fatal error during bulk import: {ex.Message}");
            }
        }
    }
}
