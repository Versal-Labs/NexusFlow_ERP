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
    public record GetProductionReceiptsQuery : IRequest<Result<IEnumerable<ProductionReceiptListDto>>>;

    public record ProductionReceiptListDto
    {
        public string ReceiptNo { get; init; } = string.Empty;
        public DateTime Date { get; init; }
        public string FinishedGood { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal TotalValue { get; init; }
        public decimal UnitCost { get; init; }
    }

    public class GetProductionReceiptsHandler : IRequestHandler<GetProductionReceiptsQuery, Result<IEnumerable<ProductionReceiptListDto>>>
    {
        private readonly string _connectionString;

        public GetProductionReceiptsHandler(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Database connection string not configured.");
        }

        public async Task<Result<IEnumerable<ProductionReceiptListDto>>> Handle(GetProductionReceiptsQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // High-performance Read Model: Joining Transactions to Product Master
            var sql = @"
            SELECT 
                st.ReferenceDocNo AS ReceiptNo,
                st.Date,
                p.Name + ' - ' + pv.SKU AS FinishedGood,
                st.Qty AS Quantity,
                st.TotalValue,
                st.UnitCost
            FROM Inventory.StockTransactions st
            INNER JOIN Master.ProductVariants pv ON st.ProductVariantId = pv.Id
            INNER JOIN Master.Products p ON pv.ProductId = p.Id
            WHERE st.Type = @TransactionType
            ORDER BY st.Date DESC, st.Id DESC";

            var data = await db.QueryAsync<ProductionReceiptListDto>(sql, new
            {
                TransactionType = (int)StockTransactionType.ProductionIn
            });

            return Result<IEnumerable<ProductionReceiptListDto>>.Success(data);
        }
    }
}
