using AutoMapper;
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
    public class GetChartOfAccountsHandler : IRequestHandler<GetChartOfAccountsQuery, Result<List<AccountDto>>>
    {
        private readonly IErpDbContext _context;
        private readonly IMapper _mapper;

        public GetChartOfAccountsHandler(IErpDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Result<List<AccountDto>>> Handle(GetChartOfAccountsQuery request, CancellationToken cancellationToken)
        {
            // 1. Fetch ALL accounts flat from DB
            var allAccounts = await _context.Accounts
                .OrderBy(a => a.Code)
                .ToListAsync(cancellationToken);

            // 2. Map to DTOs
            var dtos = _mapper.Map<List<AccountDto>>(allAccounts);

            // 3. Build the Tree Structure (In-Memory)
            var lookup = dtos.ToDictionary(x => x.Id);
            var rootNodes = new List<AccountDto>();

            foreach (var dto in dtos)
            {
                if (dto.ParentAccountId.HasValue && lookup.TryGetValue(dto.ParentAccountId.Value, out var parent))
                {
                    parent.Children.Add(dto);
                }
                else
                {
                    // If no parent, it's a Root Node (e.g., "Assets")
                    rootNodes.Add(dto);
                }
            }

            return Result<List<AccountDto>>.Success(rootNodes);
        }
    }
}
