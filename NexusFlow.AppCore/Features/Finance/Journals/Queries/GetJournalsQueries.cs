using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Journals.Queries
{
    public class JournalEntryDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<JournalLineDto> Lines { get; set; } = new();
    }

    public class JournalLineDto
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    // --- QUERY 1: GET ALL (For the Grid, optionally filtered) ---
    public class GetJournalEntriesQuery : IRequest<Result<List<JournalEntryDto>>>
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Module { get; set; }
    }

    public class GetJournalEntriesHandler : IRequestHandler<GetJournalEntriesQuery, Result<List<JournalEntryDto>>>
    {
        private readonly IErpDbContext _context;
        public GetJournalEntriesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<JournalEntryDto>>> Handle(GetJournalEntriesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.JournalEntries.AsNoTracking().AsQueryable();

            if (request.StartDate.HasValue) query = query.Where(j => j.Date.Date >= request.StartDate.Value.Date);
            if (request.EndDate.HasValue) query = query.Where(j => j.Date.Date <= request.EndDate.Value.Date);
            if (!string.IsNullOrWhiteSpace(request.Module)) query = query.Where(j => j.Module == request.Module);

            var data = await query
                .OrderByDescending(j => j.Date).ThenByDescending(j => j.Id)
                .Select(j => new JournalEntryDto
                {
                    Id = j.Id,
                    Date = j.Date,
                    ReferenceNo = j.ReferenceNo,
                    Description = j.Description,
                    Module = j.Module,
                    TotalAmount = j.TotalAmount
                })
                .Take(500) // Enterprise Guard: Prevent massive payload crashes on initial load
                .ToListAsync(cancellationToken);

            return Result<List<JournalEntryDto>>.Success(data);
        }
    }

    // --- QUERY 2: GET BY ID (For the Drill-Down Modal) ---
    public class GetJournalEntryByIdQuery : IRequest<Result<JournalEntryDto>>
    {
        public int Id { get; set; }
    }

    public class GetJournalEntryByIdHandler : IRequestHandler<GetJournalEntryByIdQuery, Result<JournalEntryDto>>
    {
        private readonly IErpDbContext _context;
        public GetJournalEntryByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<JournalEntryDto>> Handle(GetJournalEntryByIdQuery request, CancellationToken cancellationToken)
        {
            var journal = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account) // Must include Account to get Name/Code
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);

            if (journal == null) return Result<JournalEntryDto>.Failure("Journal Entry not found.");

            var dto = new JournalEntryDto
            {
                Id = journal.Id,
                Date = journal.Date,
                ReferenceNo = journal.ReferenceNo,
                Description = journal.Description,
                Module = journal.Module,
                TotalAmount = journal.TotalAmount,
                Lines = journal.Lines.Select(l => new JournalLineDto
                {
                    AccountId = l.AccountId,
                    AccountCode = l.Account?.Code ?? "UNKNOWN",
                    AccountName = l.Account?.Name ?? "UNKNOWN",
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Description = l.Description
                }).ToList()
            };

            return Result<JournalEntryDto>.Success(dto);
        }
    }
}
