using AutoMapper;
using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _config;

        public GetChartOfAccountsHandler(IConfiguration config) => _config = config;

        public async Task<Result<List<AccountDto>>> Handle(GetChartOfAccountsQuery request, CancellationToken cancellationToken)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            // Dapper Query prioritizing Active accounts but mapping hierarchy
            var sql = @"
    SELECT 
        Id, 
        Code, 
        Name, 
        CASE Type
            WHEN 1 THEN 'Asset'
            WHEN 2 THEN 'Liability'
            WHEN 3 THEN 'Equity'
            WHEN 4 THEN 'Revenue'
            WHEN 5 THEN 'Expense'
            ELSE 'Unknown'
        END AS Type, 
        IsTransactionAccount, 
        ParentAccountId, 
        Balance, 
        IsActive, 
        IsSystemAccount, 
        RequiresReconciliation
    FROM Finance.Accounts
    ORDER BY Code ASC";

            var allAccounts = (await connection.QueryAsync<AccountDto>(sql)).ToList();

            var lookup = allAccounts.ToDictionary(x => x.Id);
            var rootNodes = new List<AccountDto>();

            foreach (var dto in allAccounts)
            {
                if (dto.ParentAccountId.HasValue && lookup.TryGetValue(dto.ParentAccountId.Value, out var parent))
                {
                    parent.Children.Add(dto);
                }
                else
                {
                    rootNodes.Add(dto);
                }
            }

            return Result<List<AccountDto>>.Success(rootNodes);
        }
    }
}
