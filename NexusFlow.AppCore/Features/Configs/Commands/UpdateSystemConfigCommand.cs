using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

using NexusFlow.AppCore.Constants;

namespace NexusFlow.AppCore.Features.Configs.Commands
{
    public class UpdateSystemConfigCommand : IRequest<Result<bool>>
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    // Handler
    public class UpdateSystemConfigHandler : IRequestHandler<UpdateSystemConfigCommand, Result<bool>>
    {
        private readonly IErpDbContext _context;

        public UpdateSystemConfigHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<bool>> Handle(UpdateSystemConfigCommand request, CancellationToken cancellationToken)
        {
            // 1. Find the setting
            var config = await _context.SystemConfigs
                .FirstOrDefaultAsync(c => c.Key == request.Key, cancellationToken);

            if (config == null)
            {
                return Result<bool>.Failure($"Configuration key '{request.Key}' not found.");
            }

            // 2. Validate Data Type (Production Safety)
            if (!ValidateDataType(config.DataType, request.Value))
            {
                return Result<bool>.Failure($"Invalid format. Key '{config.Key}' expects a {config.DataType}.");
            }

            if (AccountMappingKeys.Required.Contains(config.Key, StringComparer.OrdinalIgnoreCase))
            {
                var validAccount = await _context.Accounts.AnyAsync(
                    x => x.Code == request.Value.Trim() && x.IsActive && x.IsTransactionAccount,
                    cancellationToken);
                if (!validAccount)
                    return Result<bool>.Failure("Required account mappings must resolve to an active transaction account.");
            }

            if (ConfigurationKeys.Required.Contains(config.Key, StringComparer.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(request.Value))
                return Result<bool>.Failure("Required configuration values cannot be empty.");

            // 3. Update & Save
            config.Value = request.Value;

            // Note: AuditableEntity will automatically set LastModifiedBy/At via your DbContext Interceptor
            await _context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true, "Configuration updated successfully.");
        }

        // Helper: Prevent crashing the system with bad config values
        private bool ValidateDataType(string type, string val)
        {
            switch (type)
            {
                case "Boolean": return bool.TryParse(val, out _);
                case "Decimal": return decimal.TryParse(val, out _);
                case "Integer": return int.TryParse(val, out _);
                default: return true; // String accepts anything
            }
        }
    }
}
