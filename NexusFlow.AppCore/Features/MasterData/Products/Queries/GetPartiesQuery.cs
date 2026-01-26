using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Queries
{
    public class PartyDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // 1. Get Suppliers
    public class GetSuppliersQuery : IRequest<Result<List<PartyDto>>> { }

    public class GetSuppliersHandler : IRequestHandler<GetSuppliersQuery, Result<List<PartyDto>>>
    {
        private readonly IErpDbContext _context;
        public GetSuppliersHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<PartyDto>>> Handle(GetSuppliersQuery request, CancellationToken ct)
        {
            var data = await _context.Suppliers.AsNoTracking()
                .Select(s => new PartyDto { Id = s.Id, Name = s.Name })
                .ToListAsync(ct);
            return Result<List<PartyDto>>.Success(data);
        }
    }

    // 2. Get Customers
    public class GetCustomersQuery : IRequest<Result<List<PartyDto>>> { }

    public class GetCustomersHandler : IRequestHandler<GetCustomersQuery, Result<List<PartyDto>>>
    {
        private readonly IErpDbContext _context;
        public GetCustomersHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<PartyDto>>> Handle(GetCustomersQuery request, CancellationToken ct)
        {
            var data = await _context.Customers.AsNoTracking()
                .Select(c => new PartyDto { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);
            return Result<List<PartyDto>>.Success(data);
        }
    }
}
