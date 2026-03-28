using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Queries
{
    public class GetSalesOrdersQuery : IRequest<Result<List<SalesOrderDto>>> { }

    public class GetSalesOrdersHandler : IRequestHandler<GetSalesOrdersQuery, Result<List<SalesOrderDto>>>
    {
        private readonly IErpDbContext _context;

        public GetSalesOrdersHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<SalesOrderDto>>> Handle(GetSalesOrdersQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.SalesRep)
                .AsNoTracking()
                .Select(o => new SalesOrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.Name,
                    SalesRepId = o.SalesRepId,
                    SalesRepName = o.SalesRep != null ? $"[{o.SalesRep.EmployeeCode}] {o.SalesRep.FirstName}" : "Internal",
                    TotalAmount = o.TotalAmount,
                    StatusText = o.Status.ToString(),
                    IsDraft = o.Status == Domain.Enums.SalesOrderStatus.Draft
                })
                .OrderByDescending(o => o.Id)
                .ToListAsync(cancellationToken);

            return Result<List<SalesOrderDto>>.Success(data);
        }
    }
}
