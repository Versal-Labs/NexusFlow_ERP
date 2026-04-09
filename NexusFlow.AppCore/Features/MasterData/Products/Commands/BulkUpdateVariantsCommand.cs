using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
{
    public class VariantBulkEditDto
    {
        public int VariantId { get; set; }
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
    }

    public class BulkUpdateVariantsCommand : IRequest<Result<int>>
    {
        public List<VariantBulkEditDto> Variants { get; set; } = new();
    }

    public class BulkUpdateVariantsHandler : IRequestHandler<BulkUpdateVariantsCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public BulkUpdateVariantsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(BulkUpdateVariantsCommand request, CancellationToken cancellationToken)
        {
            if (!request.Variants.Any()) return Result<int>.Failure("No modifications detected.");

            var variantIds = request.Variants.Select(v => v.VariantId).ToList();

            // Fetch variants with tracking enabled for the update
            var variantsToUpdate = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.Id))
                .ToListAsync(cancellationToken);

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                int updateCount = 0;
                foreach (var dto in request.Variants)
                {
                    var variant = variantsToUpdate.FirstOrDefault(v => v.Id == dto.VariantId);
                    if (variant != null)
                    {
                        variant.Size = string.IsNullOrWhiteSpace(dto.Size) ? "N/A" : dto.Size.Trim();
                        variant.Color = string.IsNullOrWhiteSpace(dto.Color) ? "N/A" : dto.Color.Trim();
                        variant.CostPrice = dto.CostPrice;

                        // Only update selling price if it's a Finished Good or Service
                        if (variant.Product.Type != Domain.Enums.ProductType.RawMaterial)
                        {
                            variant.SellingPrice = dto.SellingPrice;
                        }

                        // Gracefully build the name based on available attributes
                        string attrSuffix = (variant.Size == "N/A" && variant.Color == "N/A")
                            ? ""
                            : $" ({variant.Size.Replace("N/A", "")}/{variant.Color.Replace("N/A", "")})".Replace("(/)", "").Replace("(//)", "");

                        variant.Name = $"{variant.Product.Name}{attrSuffix}";
                        updateCount++;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(updateCount, $"Successfully updated {updateCount} variants.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Bulk update failed: {ex.Message}");
            }
        }
    }
}
