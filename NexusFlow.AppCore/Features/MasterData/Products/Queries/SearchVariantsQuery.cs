using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.DTOs.Master;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Queries
{
    public record SearchVariantsQuery(
        string? Query,
        int? ProductType = null,
        bool StockedOnly = false,
        int? WarehouseId = null,
        bool ExcludeWarehouseActivity = false) : IRequest<IEnumerable<VariantSearchResultDto>>;

    public class SearchVariantsHandler : IRequestHandler<SearchVariantsQuery, IEnumerable<VariantSearchResultDto>>
    {
        private readonly string _connectionString;

        public SearchVariantsHandler(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<IEnumerable<VariantSearchResultDto>> Handle(SearchVariantsQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // Enforcing the ProductType filter directly at the database level for maximum performance
            var sql = @"
            SELECT TOP 50
                pv.Id,
                pv.SKU,
                p.Name + COALESCE(' - ' + pv.Size, '') + COALESCE(' - ' + pv.Color, '') AS Name,
                COALESCE(NULLIF(pv.CostPrice, 0), pv.MovingAverageCost) AS CostPrice,
                pv.SellingPrice,
                COALESCE(u.Symbol, '') AS UomSymbol
            FROM Master.ProductVariants pv
            INNER JOIN Master.Products p ON pv.ProductId = p.Id
            LEFT JOIN Master.UnitOfMeasures u ON p.UnitOfMeasureId = u.Id
            WHERE pv.IsActive = 1
              AND (@ProductType IS NULL OR p.Type = @ProductType)
              AND (@StockedOnly = 0 OR p.Type <> @ServiceProductType)
              AND (
                    @ExcludeWarehouseActivity = 0
                    OR @WarehouseId IS NULL
                    OR NOT EXISTS (
                        SELECT 1
                        FROM Inventory.StockTransactions st
                        WHERE st.ProductVariantId = pv.Id
                          AND st.WarehouseId = @WarehouseId
                    )
                  )
              AND (@Query IS NULL OR p.Name LIKE '%' + @Query + '%' OR pv.SKU LIKE '%' + @Query + '%')
            ORDER BY p.Name, pv.SKU";

            return await db.QueryAsync<VariantSearchResultDto>(sql, new
            {
                Query = request.Query,
                ProductType = request.ProductType,
                request.StockedOnly,
                request.WarehouseId,
                request.ExcludeWarehouseActivity,
                ServiceProductType = (int)NexusFlow.Domain.Enums.ProductType.Service
            });
        }
    }
}
