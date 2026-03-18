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
    public class SupplierBillDto
    {
        public int Id { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public string SupplierInvoiceNo { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public DateTime DueDate { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public bool IsPosted { get; set; }
    }

    public class GetSupplierBillsQuery : IRequest<Result<List<SupplierBillDto>>> { }

    public class GetSupplierBillsHandler : IRequestHandler<GetSupplierBillsQuery, Result<List<SupplierBillDto>>>
    {
        private readonly string _conn;
        public GetSupplierBillsHandler(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<List<SupplierBillDto>>> Handle(GetSupplierBillsQuery request, CancellationToken ct)
        {
            using var connection = new SqlConnection(_conn);
            var sql = @"
                SELECT 
                    b.Id, b.BillNumber, b.SupplierInvoiceNo, b.BillDate, b.DueDate,
                    s.Name AS SupplierName, b.GrandTotal, b.AmountPaid,
                    CASE b.PaymentStatus WHEN 0 THEN 'Unpaid' WHEN 1 THEN 'Partial' ELSE 'Paid' END AS PaymentStatus,
                    b.IsPosted
                FROM Purchasing.SupplierBills b
                INNER JOIN Purchasing.Suppliers s ON b.SupplierId = s.Id
                ORDER BY b.BillDate DESC, b.Id DESC";

            var result = await connection.QueryAsync<SupplierBillDto>(sql);
            return Result<List<SupplierBillDto>>.Success(result.ToList());
        }
    }
}
