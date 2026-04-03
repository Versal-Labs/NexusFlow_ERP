using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    public record GetMaterialIssuesQuery : IRequest<Result<IEnumerable<MaterialIssueListDto>>>;

    public class MaterialIssueListDto
    {
        public string IssueNo { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
    }

    public class GetMaterialIssuesHandler : IRequestHandler<GetMaterialIssuesQuery, Result<IEnumerable<MaterialIssueListDto>>>
    {
        private readonly string _connectionString;

        public GetMaterialIssuesHandler(IConfiguration config) => _connectionString = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<IEnumerable<MaterialIssueListDto>>> Handle(GetMaterialIssuesQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // Grouping the RM items back into a single 'Document' Header
            var sql = @"
            SELECT 
                st.ReferenceDocNo AS IssueNo,
                MAX(st.Date) AS IssueDate,
                w.Name AS WarehouseName,
                SUM(st.TotalValue) AS TotalCost
            FROM Inventory.StockTransactions st
            LEFT JOIN Master.Warehouses w ON st.WarehouseId = w.Id
            WHERE st.Type = @TransactionType
            GROUP BY st.ReferenceDocNo, w.Name
            ORDER BY MAX(st.Date) DESC";

            var data = await db.QueryAsync<MaterialIssueListDto>(sql, new { TransactionType = (int)StockTransactionType.TransferOut });
            return Result<IEnumerable<MaterialIssueListDto>>.Success(data);
        }
    }
}
