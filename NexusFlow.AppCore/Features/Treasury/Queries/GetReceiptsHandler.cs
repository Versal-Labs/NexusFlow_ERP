using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Queries
{
    public class GetReceiptsHandler : IRequestHandler<GetReceiptsQuery, Result<List<ReceiptDto>>>
    {
        private readonly string _connString;
        public GetReceiptsHandler(IConfiguration config) => _connString = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<List<ReceiptDto>>> Handle(GetReceiptsQuery request, CancellationToken ct)
        {
            using var connection = new SqlConnection(_connString);
            var sql = @"
                SELECT 
                    p.Id, p.ReferenceNo, p.Date, c.Name as CustomerName, 
                    p.Amount, p.RelatedDocumentNo,
                    CASE p.Method WHEN 1 THEN 'Cash' WHEN 2 THEN 'Bank Transfer' ELSE 'Cheque' END as Method
                FROM Finance.PaymentTransactions p
                INNER JOIN Sales.Customers c ON p.CustomerId = c.Id
                WHERE p.Type = 1 -- 1 = CustomerReceipt
                ORDER BY p.Date DESC";

            var result = await connection.QueryAsync<ReceiptDto>(sql);
            return Result<List<ReceiptDto>>.Success(result.ToList());
        }
    }
}
