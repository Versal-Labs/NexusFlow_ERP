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
    public class GetUnpaidInvoicesHandler : IRequestHandler<GetUnpaidInvoicesQuery, Result<UnpaidInvoicesResponseDto>>
    {
        private readonly string _conn;
        public GetUnpaidInvoicesHandler(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;

        public async Task<Result<UnpaidInvoicesResponseDto>> Handle(GetUnpaidInvoicesQuery request, CancellationToken ct)
        {
            using var connection = new SqlConnection(_conn);

            // Dapper QueryMultiple allows multiple SELECT statements separated by semicolons
            var sql = @"
            -- 1. Get Unpaid Invoices (FIFO)
            SELECT 
                Id, InvoiceNumber, InvoiceDate, DueDate, 
                GrandTotal, AmountPaid, (GrandTotal - AmountPaid) AS Balance
            FROM Sales.SalesInvoices
            WHERE CustomerId = @CustomerId 
               AND IsPosted = 1 
               AND GrandTotal > AmountPaid
            ORDER BY DueDate ASC;

            -- 2. Calculate Unapplied Credit (Total Receipts minus Total Allocations)
            SELECT 
                ISNULL((SELECT SUM(Amount) FROM Finance.PaymentTransactions WHERE CustomerId = @CustomerId AND IsVoided = 0 AND Type = 1), 0) -
                ISNULL((SELECT SUM(pa.AmountAllocated) FROM Finance.PaymentAllocations pa 
                        INNER JOIN Finance.PaymentTransactions pt ON pa.PaymentTransactionId = pt.Id 
                        WHERE pt.CustomerId = @CustomerId AND pt.IsVoided = 0 AND pt.Type = 1), 0) AS UnappliedCredit;
        ";

            // Execute both queries in one database hit
            using var multi = await connection.QueryMultipleAsync(sql, new { request.CustomerId });

            var response = new UnpaidInvoicesResponseDto();

            // Read the first SELECT (Invoices)
            response.Invoices = (await multi.ReadAsync<UnpaidInvoiceDto>()).ToList();

            // Read the second SELECT (Unapplied Credit)
            response.UnappliedCredit = await multi.ReadSingleAsync<decimal>();

            return Result<UnpaidInvoicesResponseDto>.Success(response);
        }
    }
}
