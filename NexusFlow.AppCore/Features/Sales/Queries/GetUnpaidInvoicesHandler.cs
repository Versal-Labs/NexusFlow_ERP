using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Queries
{
    public class GetUnpaidInvoicesHandler : IRequestHandler<GetUnpaidInvoicesQuery, Result<List<UnpaidInvoiceDto>>>
    {
        private readonly string _conn;
        public GetUnpaidInvoicesHandler(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<List<UnpaidInvoiceDto>>> Handle(GetUnpaidInvoicesQuery request, CancellationToken ct)
        {
            using var connection = new SqlConnection(_conn);
            var sql = @"
                SELECT 
                    Id, InvoiceNumber, InvoiceDate, DueDate, 
                    GrandTotal, AmountPaid, (GrandTotal - AmountPaid) AS Balance
                FROM Sales.SalesInvoices
                WHERE CustomerId = @CustomerId 
                  AND IsPosted = 1 
                  AND GrandTotal > AmountPaid
                ORDER BY DueDate ASC"; // FIFO Allocation (Oldest first)

            var result = await connection.QueryAsync<UnpaidInvoiceDto>(sql, new { request.CustomerId });
            return Result<List<UnpaidInvoiceDto>>.Success(result.ToList());
        }
    }
}
