using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Boms.Queries
{
    public record GetBomsQuery : IRequest<Result<IEnumerable<BomListDto>>>;
    public record GetBomByIdQuery(int Id) : IRequest<Result<BomDto>>;

    public class GetBomsHandler :
    IRequestHandler<GetBomsQuery, Result<IEnumerable<BomListDto>>>,
    IRequestHandler<GetBomByIdQuery, Result<BomDto>>
    {
        private readonly string _connectionString;

        public GetBomsHandler(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<Result<IEnumerable<BomListDto>>> Handle(GetBomsQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var sql = @"
            SELECT 
                b.Id,
                b.Name,
                p.Name + ' - ' + pv.SKU AS ProductVariantName,
                b.IsActive,
                (SELECT COUNT(*) FROM Master.BomComponents WHERE BillOfMaterialId = b.Id) AS ComponentCount
            FROM Master.BillOfMaterials b
            INNER JOIN Master.ProductVariants pv ON b.ProductVariantId = pv.Id
            INNER JOIN Master.Products p ON pv.ProductId = p.Id";

            var data = await db.QueryAsync<BomListDto>(sql);
            return Result<IEnumerable<BomListDto>>.Success(data);
        }

        public async Task<Result<BomDto>> Handle(GetBomByIdQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // QueryMultiple for high-performance Master-Detail hydration in a single round-trip
            var sql = @"
            SELECT b.Id, b.Name, b.ProductVariantId, b.IsActive, p.Name + ' - ' + pv.SKU AS ProductVariantName
            FROM Master.BillOfMaterials b
            INNER JOIN Master.ProductVariants pv ON b.ProductVariantId = pv.Id
            INNER JOIN Master.Products p ON pv.ProductId = p.Id
            WHERE b.Id = @Id;

            SELECT c.Id, c.MaterialVariantId, c.Quantity, p.Name + ' - ' + pv.SKU AS MaterialVariantName
            FROM Master.BomComponents c
            INNER JOIN Master.ProductVariants pv ON c.MaterialVariantId = pv.Id
            INNER JOIN Master.Products p ON pv.ProductId = p.Id
            WHERE c.BillOfMaterialId = @Id;";

            using var multi = await db.QueryMultipleAsync(sql, new { request.Id });
            var bom = await multi.ReadFirstOrDefaultAsync<BomDto>();

            if (bom == null) return Result<BomDto>.Failure("BOM not found.");

            bom.Components = (await multi.ReadAsync<BomComponentDto>()).ToList();

            return Result<BomDto>.Success(bom);
        }
    }
}
