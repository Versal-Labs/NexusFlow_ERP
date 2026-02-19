using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public class GetAccountsQuery : IRequest<Result<List<AccountDto>>>
    {
        // Optional: We could add a filter here like 'AccountType' if we wanted server-side filtering
    }

    public class GetAccountsHandler : IRequestHandler<GetAccountsQuery, Result<List<AccountDto>>>
    {
        private readonly IErpDbContext _context;

        public GetAccountsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<AccountDto>>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
        {
            // Fast "Read-Only" projection. We flatten the Enum to a String for easier JS filtering.
            var accounts = await _context.Accounts
                .AsNoTracking()
                .OrderBy(a => a.Code)
                .Select(a => new AccountDto
                {
                    Id = a.Id,
                    Code = a.Code,
                    Name = a.Name,
                    Type = a.Type.ToString(), // Crucial: Converts Enum (e.g. 4) to "Revenue"
                    IsTransactionAccount = true // Assuming we only pick leaf nodes or transaction accounts
                })
                .ToListAsync(cancellationToken);

            return Result<List<AccountDto>>.Success(accounts);
        }
    }
}
