using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Commands
{
    public class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public CreateAccountHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            // 1. Validation: Check if Code already exists
            bool codeExists = await _context.Accounts
                .AnyAsync(a => a.Code == request.Code, cancellationToken);

            if (codeExists)
            {
                return Result<int>.Failure($"Account Code '{request.Code}' already exists.");
            }

            // 2. Validation: Check Parent Existence (if not null)
            if (request.ParentAccountId.HasValue)
            {
                bool parentExists = await _context.Accounts
                    .AnyAsync(a => a.Id == request.ParentAccountId.Value, cancellationToken);

                if (!parentExists)
                {
                    return Result<int>.Failure($"Parent Account ID {request.ParentAccountId} not found.");
                }
            }

            // 3. Create Entity
            var account = new Account
            {
                Code = request.Code,
                Name = request.Name,
                Type = request.Type,
                ParentAccountId = request.ParentAccountId,
                IsTransactionAccount = request.IsTransactionAccount,
                // Audit fields are handled by DbContext if using AuditableEntity interceptors, 
                // otherwise might need manual setting or current user service.
            };

            // 4. Save
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(account.Id, "Account created successfully.");
        }
    }
}
