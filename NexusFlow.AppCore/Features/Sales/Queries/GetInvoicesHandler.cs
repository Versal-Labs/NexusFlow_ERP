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
    public class GetInvoicesHandler : IRequestHandler<GetInvoicesQuery, Result<List<InvoiceDto>>>
    {
        private readonly string _connectionString;

        public GetInvoicesHandler(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<Result<List<InvoiceDto>>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
        SELECT 
            i.Id, 
            i.InvoiceNumber, 
            i.CustomerPoNumber,
            i.InvoiceDate, 
            i.DueDate, 
            c.Name AS CustomerName, 
            i.GrandTotal, 
            i.AmountPaid,    
            i.IsPosted,
            i.PaymentStatus
        FROM Sales.SalesInvoices i
        INNER JOIN Sales.Customers c ON i.CustomerId = c.Id
        ORDER BY i.InvoiceDate DESC, i.Id DESC";

            var result = await connection.QueryAsync<InvoiceDto>(sql);
            return Result<List<InvoiceDto>>.Success(result.ToList());
        }
    }
}
