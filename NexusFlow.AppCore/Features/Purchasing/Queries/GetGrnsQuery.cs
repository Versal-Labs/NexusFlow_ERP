using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Queries
{
    public class GrnDto
    {
        public int Id { get; set; }
        public string GrnNumber { get; set; } = string.Empty;
        public string PoNumber { get; set; } = string.Empty;
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
            using IDbConnection connection = new SqlConnection(_conn);

            // TIER-1 FIX: Added po.PoNumber to the projection
            var sql = @"
        SELECT 
            g.Id, 
            g.GrnNumber, 
            po.PoNumber,
            g.ReceivedDate AS ReceiptDate, 
            s.Name AS SupplierName,
            w.Name AS WarehouseName, 
            g.SupplierInvoiceNo AS ReferenceNo, 
            g.TotalAmount AS TotalValue
        FROM Purchasing.GRNs g
        INNER JOIN Purchasing.PurchaseOrders po ON g.PurchaseOrderId = po.Id
        INNER JOIN Purchasing.Suppliers s ON po.SupplierId = s.Id
        INNER JOIN Master.Warehouses w ON g.WarehouseId = w.Id
        ORDER BY g.ReceivedDate DESC, g.Id DESC";

            var result = await connection.QueryAsync<GrnDto>(sql);
            return Result<List<GrnDto>>.Success(result.ToList());
        }
    }
}
