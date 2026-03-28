using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Boms.Queries
{
    public class GetBomsQuery : IRequest<Result<List<BomDto>>>
    {
    }


    public class GetBomsHandler : IRequestHandler<GetBomsQuery, Result<List<BomDto>>>
    {
        private readonly string _connectionString;

        public GetBomsHandler(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<Result<List<BomDto>>> Handle(GetBomsQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // Raw SQL utilizing Dapper's multiple mapping for Master-Detail relationship
            var sql = @"
                SELECT 
                    b.Id, b.ProductVariantId, b.Name, b.IsActive,
                    c.Id, c.MaterialVariantId, c.Quantity
                FROM Master.BillOfMaterials b
                LEFT JOIN Master.BomComponents c ON b.Id = c.BillOfMaterialId
                -- Exclude Soft-Deleted records if AuditableEntity supports it
            ";

            var bomDictionary = new Dictionary<int, BomDto>();

            await db.QueryAsync<BomDto, BomComponentDto, BomDto>(
                sql,
                (bom, component) =>
                {
                    if (!bomDictionary.TryGetValue(bom.Id, out var currentBom))
                    {
                        currentBom = bom;
                        currentBom.Components = new List<BomComponentDto>();
                        bomDictionary.Add(currentBom.Id, currentBom);
                    }

                    if (component != null && component.Id > 0)
                    {
                        currentBom.Components.Add(component);
                    }

                    return currentBom;
                },
                splitOn: "Id"
            );

            return Result<List<BomDto>>.Success(bomDictionary.Values.ToList());
        }
    }
}
