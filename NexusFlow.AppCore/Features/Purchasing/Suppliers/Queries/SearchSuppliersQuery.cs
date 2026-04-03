using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Suppliers.Queries
{
    public record SearchSuppliersQuery(string? Query) : IRequest<IEnumerable<SupplierSearchDto>>;

    public class SupplierSearchDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SearchSuppliersHandler : IRequestHandler<SearchSuppliersQuery, IEnumerable<SupplierSearchDto>>
    {
        private readonly string _connectionString;

        public SearchSuppliersHandler(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<IEnumerable<SupplierSearchDto>> Handle(SearchSuppliersQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var sql = @"
            SELECT TOP 50 Id, Name 
            FROM Purchasing.Suppliers 
            WHERE IsActive = 1 AND (@Query IS NULL OR Name LIKE '%' + @Query + '%')
            ORDER BY Name";

            return await db.QueryAsync<SupplierSearchDto>(sql, new { Query = request.Query });
        }
    }
}
