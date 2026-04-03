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
    public record SearchVariantsQuery(string? Query, int? ProductType) : IRequest<IEnumerable<VariantSearchResultDto>>;

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
                p.Name + COALESCE(' - ' + pv.Size, '') + COALESCE(' - ' + pv.Color, '') AS Name
            FROM Master.ProductVariants pv
            INNER JOIN Master.Products p ON pv.ProductId = p.Id
            WHERE pv.IsActive = 1
              AND (@ProductType IS NULL OR p.Type = @ProductType)
              AND (@Query IS NULL OR p.Name LIKE '%' + @Query + '%' OR pv.SKU LIKE '%' + @Query + '%')
            ORDER BY p.Name, pv.SKU";

            return await db.QueryAsync<VariantSearchResultDto>(sql, new
            {
                Query = request.Query,
                ProductType = request.ProductType
            });
        }
    }
}
