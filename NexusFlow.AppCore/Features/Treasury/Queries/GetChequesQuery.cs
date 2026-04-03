using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Queries
{
    public class ChequeGridDto
    {
        public int Id { get; set; }
        public string ChequeNumber { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public DateTime ChequeDate { get; set; }
        public decimal Amount { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    public class GetChequesQuery : IRequest<Result<List<ChequeGridDto>>>
    {
        public int? CustomerId { get; set; }
        public int? BankId { get; set; }
        public int? BankBranchId { get; set; }
        public ChequeStatus? Status { get; set; }
    }

    public class GetChequesHandler : IRequestHandler<GetChequesQuery, Result<List<ChequeGridDto>>>
    {
        private readonly IErpDbContext _context;
        public GetChequesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<ChequeGridDto>>> Handle(GetChequesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.ChequeRegisters
                .Include(c => c.Customer)
                .Include(c => c.BankBranch)
                .Include(c => c.BankBranch.Bank)
                .AsNoTracking()
                .AsQueryable();

            if (request.CustomerId.HasValue) query = query.Where(c => c.CustomerId == request.CustomerId);
            if (request.BankBranchId.HasValue) query = query.Where(c => c.BankBranchId == request.BankBranchId);
            if (request.Status.HasValue) query = query.Where(c => c.Status == request.Status);

            var data = await query
                .OrderBy(c => c.ChequeDate) // Show oldest (most due) cheques first
                .Select(c => new ChequeGridDto
                {
                    Id = c.Id,
                    ChequeNumber = c.ChequeNumber,
                    BankName = c.BankBranch.Bank.Name,
                    BranchName = c.BankBranch.BranchName,
                    ChequeDate = c.ChequeDate,
                    Amount = c.Amount,
                    CustomerName = c.Customer.Name,
                    StatusId = (int)c.Status,
                    StatusText = c.Status.ToString()
                })
                .ToListAsync(cancellationToken);

            return Result<List<ChequeGridDto>>.Success(data);
        }
    }
}
