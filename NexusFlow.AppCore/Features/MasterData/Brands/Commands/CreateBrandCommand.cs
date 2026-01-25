using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Brands.Commands
{
    public class CreateBrandCommand : IRequest<Result<int>>
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CreateBrandHandler : IRequestHandler<CreateBrandCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public CreateBrandHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
            // 1. Validation (Business Logic)
            // Ideally use FluentValidation, but simple checks here are acceptable for now
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result<int>.Failure("Brand Name is required.");

            // 2. Entity Creation
            var brand = new Brand
            {
                Name = request.Name,
                Description = request.Description
            };

            // 3. Persistence
            _context.Brands.Add(brand);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(brand.Id);
        }
    }
}
