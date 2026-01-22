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
    public class CreateSystemConfigCommand : IRequest<Result<string>>
    {
        public string Key { get; set; }        // e.g. "Inventory.AllowNegative"
        public string Value { get; set; }      // e.g. "true"
        public string DataType { get; set; }   // e.g. "Boolean"
        public string Description { get; set; }
    }

    public class CreateSystemConfigHandler : IRequestHandler<CreateSystemConfigCommand, Result<string>>
    {
        private readonly IErpDbContext _context;

        public CreateSystemConfigHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<string>> Handle(CreateSystemConfigCommand request, CancellationToken cancellationToken)
        {
            // 1. Enforce Uniqueness
            var exists = await _context.SystemConfigs.AnyAsync(x => x.Key == request.Key, cancellationToken);
            if (exists) return Result<string>.Failure($"The key '{request.Key}' already exists.");

            // 2. Create Entity
            var entity = new SystemConfig
            {
                Key = request.Key,
                Value = request.Value,
                DataType = request.DataType,
                Description = request.Description
            };

            _context.SystemConfigs.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<string>.Success(entity.Key, "Configuration added successfully.");
        }
    }
}
