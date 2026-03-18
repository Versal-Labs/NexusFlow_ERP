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
    public class UnbilledGrnDto
    {
        public int Id { get; set; }
        public string GrnNumber { get; set; } = string.Empty;
        public DateTime ReceivedDate { get; set; }
        public string SupplierInvoiceNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        // We also pull the lines so the UI can auto-populate the bill grid
        public List<UnbilledGrnLineDto> Lines { get; set; } = new();
    }

    public class UnbilledGrnLineDto
    {
        public int ProductVariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class GetUnbilledGrnsQuery : IRequest<Result<List<UnbilledGrnDto>>>
    {
        public int SupplierId { get; set; }
    }

    public class GetUnbilledGrnsHandler : IRequestHandler<GetUnbilledGrnsQuery, Result<List<UnbilledGrnDto>>>
    {
        private readonly string _conn;
        public GetUnbilledGrnsHandler(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<List<UnbilledGrnDto>>> Handle(GetUnbilledGrnsQuery request, CancellationToken ct)
        {
            using var connection = new SqlConnection(_conn);

            // Fetch the Headers
            var sqlHeader = @"
                SELECT 
                    g.Id, 
                    g.GrnNumber, 
                    g.ReceivedDate, 
                    g.SupplierInvoiceNo, 
                    g.TotalAmount 
                FROM Purchasing.GRNs g
                INNER JOIN Purchasing.PurchaseOrders po ON g.PurchaseOrderId = po.Id
                WHERE po.SupplierId = @SupplierId AND g.IsBilled = 0";

            var grns = (await connection.QueryAsync<UnbilledGrnDto>(sqlHeader, new { request.SupplierId })).ToList();

            if (!grns.Any()) return Result<List<UnbilledGrnDto>>.Success(grns);

            // Fetch the Lines to prevent UI round-trips
            var grnIds = grns.Select(g => g.Id).ToArray();
            var sqlLines = @"
                SELECT 
                    i.GRNId AS Id, -- Mapped to temporary ID for grouping
                    i.ProductVariantId, p.Name AS ProductName, pv.Sku,
                    i.QuantityReceived, i.UnitCost
                FROM Purchasing.GRNItems i
                INNER JOIN Master.ProductVariants pv ON i.ProductVariantId = pv.Id
                INNER JOIN Master.Products p ON pv.ProductId = p.Id
                WHERE i.GRNId IN @GrnIds";

            var lines = await connection.QueryAsync(sqlLines, new { GrnIds = grnIds });

            // Map lines to headers
            foreach (var grn in grns)
            {
                grn.Lines = lines.Where(l => l.Id == grn.Id).Select(l => new UnbilledGrnLineDto
                {
                    ProductVariantId = l.ProductVariantId,
                    ProductName = l.ProductName,
                    Sku = l.Sku,
                    QuantityReceived = l.QuantityReceived,
                    UnitCost = l.UnitCost
                }).ToList();
            }

            return Result<List<UnbilledGrnDto>>.Success(grns);
        }
    }
}
