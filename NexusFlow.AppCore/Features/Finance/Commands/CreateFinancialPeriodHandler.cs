using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Commands
{
    public class CreateFinancialPeriodHandler : IRequestHandler<CreateFinancialPeriodCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public CreateFinancialPeriodHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(CreateFinancialPeriodCommand request, CancellationToken cancellationToken)
        {
            if (request.EndDate.Date < request.StartDate.Date)
                return Result<int>.Failure("Financial period end date cannot be before its start date.");

            // Period ranges may never overlap; otherwise posting-date resolution becomes ambiguous.
            var exists = await _context.FinancialPeriods.AnyAsync(p =>
                request.StartDate.Date <= p.EndDate.Date && request.EndDate.Date >= p.StartDate.Date,
                cancellationToken);

            if (exists)
            {
                return Result<int>.Failure("The financial period overlaps an existing period.");
            }

            var period = new FinancialPeriod
            {
                Year = request.Year,
                Month = request.Month,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsClosed = false
            };

            _context.FinancialPeriods.Add(period);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(period.Id, "Financial Period created successfully.");
        }
    }
}
