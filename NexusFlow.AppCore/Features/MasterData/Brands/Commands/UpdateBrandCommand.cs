using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Brands.Commands
{
    public class UpdateBrandCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateBrandHandler : IRequestHandler<UpdateBrandCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public UpdateBrandHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
        {
            var brand = await _context.Brands.FindAsync(new object[] { request.Id }, cancellationToken);

            if (brand == null)
                return Result<int>.Failure($"Brand with Id {request.Id} not found.");

            brand.Name = request.Name;
            brand.Description = request.Description;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(brand.Id);
        }
    }
}
