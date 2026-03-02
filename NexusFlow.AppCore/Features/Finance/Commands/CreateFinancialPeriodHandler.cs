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
            // Validation: Check if period already exists
            var exists = await _context.FinancialPeriods
                .AnyAsync(p => p.Year == request.Year && p.Month == request.Month, cancellationToken);

            if (exists)
            {
                return Result<int>.Failure("Financial Period for this Year and Month already exists.");
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
