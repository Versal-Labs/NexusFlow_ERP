using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Warehouses.Queries
{
    public class GetWarehousesHandler :
        IRequestHandler<GetWarehousesQuery, Result<List<WarehouseDto>>>,
        IRequestHandler<GetWarehouseByIdQuery, Result<WarehouseDto>>
    {
        private readonly string _connString;

        public GetWarehousesHandler(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<Result<List<WarehouseDto>>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
        {
            using var connection = new SqlConnection(_connString);
            var sql = @"
                SELECT 
                    w.Id, w.Code, w.Name, w.Location, w.ManagerName, w.Type, 
                    w.LinkedSupplierId, s.Name AS LinkedSupplierName, 
                    w.OverrideInventoryAccountId, w.IsActive
                FROM Master.Warehouses w
                LEFT JOIN Purchasing.Suppliers s ON w.LinkedSupplierId = s.Id
                ORDER BY w.Name ASC";

            var result = await connection.QueryAsync<WarehouseDto>(sql);
            return Result<List<WarehouseDto>>.Success(result.ToList());
        }

        public async Task<Result<WarehouseDto>> Handle(GetWarehouseByIdQuery request, CancellationToken cancellationToken)
        {
            using var connection = new SqlConnection(_connString);
            var sql = @"
                SELECT 
                    w.Id, w.Code, w.Name, w.Location, w.ManagerName, w.Type, 
                    w.LinkedSupplierId, s.Name AS LinkedSupplierName, 
                    w.OverrideInventoryAccountId, w.IsActive
                FROM Master.Warehouses w
                LEFT JOIN Purchasing.Suppliers s ON w.LinkedSupplierId = s.Id
                WHERE w.Id = @Id";

            var result = await connection.QueryFirstOrDefaultAsync<WarehouseDto>(sql, new { request.Id });
            if (result == null) return Result<WarehouseDto>.Failure("Warehouse not found.");

            return Result<WarehouseDto>.Success(result);
        }
    }
}
