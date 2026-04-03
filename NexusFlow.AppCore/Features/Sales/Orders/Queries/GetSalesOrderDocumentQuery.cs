using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Queries
{
    public record GetSalesOrderDocumentQuery(int OrderId) : IRequest<Result<SalesOrderDocumentDto>>;

    public class SalesOrderDocumentDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? SalesRepId { get; set; }
        public string SalesRepName { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<SalesOrderDocumentLineDto> Items { get; set; } = new();
    }

    public class SalesOrderDocumentLineDto
    {
        public int ProductVariantId { get; set; }
        public string ProductDescription { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class GetSalesOrderDocumentHandler : IRequestHandler<GetSalesOrderDocumentQuery, Result<SalesOrderDocumentDto>>
    {
        private readonly string _conn;
        public GetSalesOrderDocumentHandler(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<SalesOrderDocumentDto>> Handle(GetSalesOrderDocumentQuery request, CancellationToken ct)
        {
            using IDbConnection db = new SqlConnection(_conn);

            // QueryMultiple for Master-Detail hydration
            var sql = @"
            SELECT 
                o.Id, o.OrderNumber, o.OrderDate, o.Status, o.CustomerId, c.Name AS CustomerName,
                o.SalesRepId, e.FirstName + ' ' + e.LastName AS SalesRepName, o.Notes, o.TotalAmount,
                CASE o.Status 
                    WHEN 1 THEN 'Draft' 
                    WHEN 2 THEN 'Submitted' 
                    WHEN 3 THEN 'Converted' 
                    WHEN 4 THEN 'Cancelled' 
                    ELSE 'Unknown' 
                END AS StatusText
            FROM Sales.SalesOrders o
            INNER JOIN Sales.Customers c ON o.CustomerId = c.Id
            LEFT JOIN HR.Employees e ON o.SalesRepId = e.Id
            WHERE o.Id = @OrderId;

            SELECT 
                i.ProductVariantId, p.Name + ' - ' + pv.SKU AS ProductDescription,
                i.Quantity, i.UnitPrice, i.Discount, i.LineTotal
            FROM Sales.SalesOrderItems i
            INNER JOIN Master.ProductVariants pv ON i.ProductVariantId = pv.Id
            INNER JOIN Master.Products p ON pv.ProductId = p.Id
            WHERE i.SalesOrderId = @OrderId;";

            using var multi = await db.QueryMultipleAsync(sql, new { request.OrderId });
            var doc = await multi.ReadFirstOrDefaultAsync<SalesOrderDocumentDto>();

            if (doc == null) return Result<SalesOrderDocumentDto>.Failure("Document not found.");

            doc.Items = (await multi.ReadAsync<SalesOrderDocumentLineDto>()).ToList();
            return Result<SalesOrderDocumentDto>.Success(doc);
        }
    }
}
