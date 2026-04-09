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
            if (await _context.Accounts.AnyAsync(a => a.Code == request.Code, cancellationToken))
                return Result<int>.Failure($"Account Code '{request.Code}' already exists.");

            if (request.ParentAccountId.HasValue)
            {
                var parent = await _context.Accounts.FindAsync(new object[] { request.ParentAccountId.Value }, cancellationToken);
                if (parent == null) return Result<int>.Failure("Parent Account not found.");

                // ARCHITECTURAL RULE: Child MUST inherit Parent's Account Type
                if (parent.Type != request.Type)
                    return Result<int>.Failure($"Hierarchy Violation: Account Type ({request.Type}) must match Parent Account Type ({parent.Type}).");
            }

            var account = new Account
            {
                Code = request.Code,
                Name = request.Name,
                Type = request.Type,
                ParentAccountId = request.ParentAccountId,
                IsTransactionAccount = request.IsTransactionAccount,
                RequiresReconciliation = request.RequiresReconciliation,
                IsActive = true,
                IsSystemAccount = false // Can only be set via DB seed/migrations
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(account.Id, "Account created successfully.");
        }
    }
}
