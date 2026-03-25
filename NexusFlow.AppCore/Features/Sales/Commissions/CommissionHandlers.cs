using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Commissions
{
    // --- QUERIES ---
    public class GetCommissionRulesQuery : IRequest<Result<List<CommissionRuleDto>>> { }

    public class GetCommissionRulesHandler : IRequestHandler<GetCommissionRulesQuery, Result<List<CommissionRuleDto>>>
    {
        private readonly IErpDbContext _context;
        public GetCommissionRulesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<CommissionRuleDto>>> Handle(GetCommissionRulesQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.CommissionRules
                .Include(c => c.Category)
                .Include(c => c.Employee)
                .AsNoTracking()
                .Select(c => new CommissionRuleDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    RuleType = c.RuleType,
                    CategoryId = c.CategoryId,
                    CategoryName = c.Category != null ? c.Category.Name : null,
                    EmployeeId = c.EmployeeId,
                    EmployeeName = c.Employee != null ? $"[{c.Employee.EmployeeCode}] {c.Employee.FirstName} {c.Employee.LastName}" : "All Sales Reps",
                    ValidFrom = c.ValidFrom,
                    ValidTo = c.ValidTo,
                    CommissionPercentage = c.CommissionPercentage,
                    IsActive = c.IsActive
                })
                .OrderByDescending(c => c.IsActive).ThenBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return Result<List<CommissionRuleDto>>.Success(data);
        }
    }

    // --- COMMANDS ---
    public class SaveCommissionRuleCommand : IRequest<Result<int>>
    {
        public CommissionRuleDto Rule { get; set; }
    }

    public class SaveCommissionRuleHandler : IRequestHandler<SaveCommissionRuleCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public SaveCommissionRuleHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(SaveCommissionRuleCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Rule;

            // 1. Clean the payload based on RuleType
            if (dto.RuleType == CommissionRuleType.GlobalFlatRate)
                dto.CategoryId = null;

            // =======================================================================
            // 2. ENTERPRISE GUARD: OVERLAPPING SCOPE PREVENTION
            // =======================================================================
            if (dto.IsActive)
            {
                bool overlapExists = await _context.CommissionRules
                    .AnyAsync(r => r.Id != dto.Id
                                && r.IsActive
                                && r.RuleType == dto.RuleType
                                && r.CategoryId == dto.CategoryId
                                && r.EmployeeId == dto.EmployeeId
                                // Timeframe intersection logic:
                                && (!r.ValidFrom.HasValue || !dto.ValidTo.HasValue || r.ValidFrom <= dto.ValidTo)
                                && (!r.ValidTo.HasValue || !dto.ValidFrom.HasValue || r.ValidTo >= dto.ValidFrom),
                        cancellationToken);

                if (overlapExists)
                {
                    return Result<int>.Failure("Cannot save: Another active rule with this exact scope and an overlapping timeframe already exists. Please disable or adjust the dates of the conflicting rule first.");
                }
            }

            // 3. Save Entity
            CommissionRule entity;
            if (dto.Id == 0)
            {
                entity = new CommissionRule();
                _context.CommissionRules.Add(entity);
            }
            else
            {
                entity = await _context.CommissionRules.FindAsync(new object[] { dto.Id }, cancellationToken);
                if (entity == null) return Result<int>.Failure("Rule not found.");
            }

            entity.Name = dto.Name;
            entity.RuleType = dto.RuleType;
            entity.CategoryId = dto.CategoryId;
            entity.EmployeeId = dto.EmployeeId;
            entity.ValidFrom = dto.ValidFrom;
            entity.ValidTo = dto.ValidTo;
            entity.CommissionPercentage = dto.CommissionPercentage;
            entity.IsActive = dto.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(entity.Id, "Commission Rule saved successfully.");
        }
    }
}
