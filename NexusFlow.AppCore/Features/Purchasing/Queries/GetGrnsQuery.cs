using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Queries
{
    public class GrnDto
    {
        public int Id { get; set; }
        public string GrnNumber { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }

    public class GetGrnsQuery : IRequest<Result<List<GrnDto>>> { }

    public class GetGrnsHandler : IRequestHandler<GetGrnsQuery, Result<List<GrnDto>>>
    {
        private readonly string _conn;
        public GetGrnsHandler(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<List<GrnDto>>> Handle(GetGrnsQuery request, CancellationToken ct)
        {
            using var connection = new SqlConnection(_conn);
            var sql = @"
                SELECT g.Id, g.GrnNumber, g.ReceiptDate, s.Name AS SupplierName, 
                       w.Name AS WarehouseName, g.ReferenceNo, g.TotalValue
                FROM Purchasing.GoodsReceipts g
                INNER JOIN Purchasing.Suppliers s ON g.SupplierId = s.Id
                INNER JOIN MasterData.Warehouses w ON g.WarehouseId = w.Id
                ORDER BY g.ReceiptDate DESC, g.Id DESC";

            var result = await connection.QueryAsync<GrnDto>(sql);
            return Result<List<GrnDto>>.Success(result.ToList());
        }
    }
}
