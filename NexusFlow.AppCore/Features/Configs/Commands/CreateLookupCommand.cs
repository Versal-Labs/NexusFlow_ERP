using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Configs.Commands
{
    public class CreateLookupCommand : IRequest<Result<int>>
    {
        public string Type { get; set; }
        public string Code { get; set; }
        public string Value { get; set; }
        public int SortOrder { get; set; }
    }

    public class CreateLookupHandler : IRequestHandler<CreateLookupCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public CreateLookupHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(CreateLookupCommand request, CancellationToken cancellationToken)
        {
            // Enforce Uniqueness
            bool exists = await _context.SystemLookups.AnyAsync(x => x.Type == request.Type && x.Code == request.Code, cancellationToken);
            if (exists) return Result<int>.Failure($"Code '{request.Code}' already exists for {request.Type}.");

            var entity = new SystemLookup
            {
                Type = request.Type,
                Code = request.Code.ToUpper(), // Standardize codes
                Value = request.Value,
                SortOrder = request.SortOrder,
                IsActive = true
            };

            _context.SystemLookups.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(entity.Id);
        }
    }
}
