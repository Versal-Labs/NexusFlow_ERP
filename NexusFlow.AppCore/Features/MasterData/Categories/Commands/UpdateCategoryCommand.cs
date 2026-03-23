using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Categories.Commands
{
    public class UpdateCategoryCommand : IRequest<Result<int>>
    {
        public CategoryDto Category { get; set; }
    }

    public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public UpdateCategoryHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Category;

            // 1. Fetch entity
            var entity = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == dto.Id, cancellationToken);

            if (entity == null)
                return Result<int>.Failure($"Category with ID {dto.Id} not found.");

            // ==========================================
            // ENTERPRISE GUARDS & VALIDATIONS
            // ==========================================

            // Guard 1: Direct Cyclic Dependency Check
            if (dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value == dto.Id)
            {
                return Result<int>.Failure("A category cannot be its own parent. This creates an infinite loop.");
            }

            // Guard 2: Deep Cyclic Dependency Check (Prevent setting parent to own child)
            if (dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value != entity.ParentCategoryId)
            {
                bool isChild = await IsDescendantAsync(dto.Id, dto.ParentCategoryId.Value, cancellationToken);
                if (isChild)
                {
                    return Result<int>.Failure("Cannot set the parent to one of its own sub-categories. This creates a cyclic graph.");
                }
            }

            // Guard 3: Unique Code Constraint
            var incomingCode = dto.Code?.Trim().ToUpper();
            if (!string.Equals(entity.Code, incomingCode, StringComparison.OrdinalIgnoreCase))
            {
                bool codeExists = await _context.Categories
                    .AnyAsync(c => c.Code == incomingCode, cancellationToken);

                if (codeExists)
                {
                    return Result<int>.Failure($"The Category Code '{incomingCode}' is already in use by another category.");
                }
            }

            // ==========================================
            // STATE MUTATION
            // ==========================================
            entity.Name = dto.Name?.Trim();
            entity.Code = incomingCode;
            entity.ParentCategoryId = dto.ParentCategoryId;

            // Enterprise Posting Groups
            entity.SalesAccountId = dto.SalesAccountId;
            entity.InventoryAccountId = dto.InventoryAccountId;
            entity.CogsAccountId = dto.CogsAccountId;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(entity.Id, "Category updated successfully.");
        }

        /// <summary>
        /// Recursively checks if the proposed parent is actually a descendant of the current category.
        /// </summary>
        private async Task<bool> IsDescendantAsync(int currentCategoryId, int proposedParentId, CancellationToken cancellationToken)
        {
            // Traverse UP the tree from the proposed parent. 
            // If we ever hit the currentCategoryId, it means the proposed parent is a child of the current category.
            int? currentCheckId = proposedParentId;

            while (currentCheckId.HasValue)
            {
                if (currentCheckId.Value == currentCategoryId) return true;

                var parentNode = await _context.Categories
                    .AsNoTracking()
                    .Where(c => c.Id == currentCheckId.Value)
                    .Select(c => c.ParentCategoryId)
                    .FirstOrDefaultAsync(cancellationToken);

                currentCheckId = parentNode;
            }

            return false;
        }
    }
}
