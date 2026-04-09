using MediatR;
using NexusFlow.AppCore.Interfaces;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Commands
{
    public class UpdateAccountCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? ParentAccountId { get; set; }
        public bool RequiresReconciliation { get; set; }
    }

    public class UpdateAccountHandler : IRequestHandler<UpdateAccountCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public UpdateAccountHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
        {
            var account = await _context.Accounts.FindAsync(new object[] { request.Id }, cancellationToken);
            if (account == null) return Result<int>.Failure("Account not found.");

            if (account.IsSystemAccount && account.Code != request.Code)
                return Result<int>.Failure("System Account codes cannot be modified.");

            // Prevent circular dependency
            if (request.ParentAccountId == request.Id)
                return Result<int>.Failure("Account cannot be its own parent.");

            // Check if new code exists elsewhere
            if (account.Code != request.Code && await _context.Accounts.AnyAsync(a => a.Code == request.Code && a.Id != request.Id, cancellationToken))
                return Result<int>.Failure($"Account Code '{request.Code}' is already in use.");

            account.Code = request.Code;
            account.Name = request.Name;
            account.ParentAccountId = request.ParentAccountId;
            account.RequiresReconciliation = request.RequiresReconciliation;

            _context.Accounts.Update(account);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(account.Id, "Account updated successfully.");
        }
    }
}
