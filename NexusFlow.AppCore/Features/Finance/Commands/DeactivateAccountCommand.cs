using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Commands
{
    public record DeactivateAccountCommand(int Id) : IRequest<Result<int>>;

    public class DeactivateAccountHandler : IRequestHandler<DeactivateAccountCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public DeactivateAccountHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(DeactivateAccountCommand request, CancellationToken cancellationToken)
        {
            var account = await _context.Accounts
                .Include(a => a.ChildAccounts)
                .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

            if (account == null) return Result<int>.Failure("Account not found.");

            if (account.IsSystemAccount)
                return Result<int>.Failure("System Control Accounts cannot be deactivated.");

            if (account.Balance != 0)
                return Result<int>.Failure($"Cannot deactivate account. Balance must be LKR 0.00 (Current Balance: {account.Balance}).");

            if (account.ChildAccounts.Any(c => c.IsActive))
                return Result<int>.Failure("Cannot deactivate account with active child accounts.");

            account.Deactivate();
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(account.Id, "Account deactivated successfully.");
        }
    }
}
