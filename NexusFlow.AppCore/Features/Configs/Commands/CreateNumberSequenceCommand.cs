using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Configs.Commands
{
    public class CreateNumberSequenceCommand : IRequest<Result<int>>
    {
        // The unique key used by code (e.g. "SalesInvoice", "PurchaseOrder")
        public string Module { get; set; } = string.Empty;

        // Visual formatting
        public string Prefix { get; set; } = string.Empty;
        public int NextNumber { get; set; } = 1;
        public string Delimiter { get; set; } = "-";
        public string Suffix { get; set; } = string.Empty;
    }

    // 2. The Logic (Handler)
    public class CreateNumberSequenceHandler : IRequestHandler<CreateNumberSequenceCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public CreateNumberSequenceHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(CreateNumberSequenceCommand request, CancellationToken cancellationToken)
        {
            // A. Validation: Ensure Module Name is Unique
            // The system relies on 'Module' string to fetch the sequence (e.g. GetNext("Invoice"))
            // We cannot allow duplicates here.
            var exists = await _context.NumberSequences
                .AnyAsync(x => x.Module == request.Module, cancellationToken);

            if (exists)
            {
                return Result<int>.Failure($"A sequence for module '{request.Module}' already exists.");
            }

            // B. Validation: Basic Sanity Checks
            if (string.IsNullOrWhiteSpace(request.Module))
                return Result<int>.Failure("Module name is required.");

            if (request.NextNumber < 1)
                return Result<int>.Failure("Starting number must be 1 or greater.");

            // C. Create Entity
            var entity = new NumberSequence
            {
                Module = request.Module.Trim(),
                // Standardize formatting: "inv" -> "INV"
                Prefix = request.Prefix?.Trim().ToUpper() ?? "",
                NextNumber = request.NextNumber,
                Delimiter = request.Delimiter ?? "-",
                Suffix = request.Suffix?.Trim() ?? ""
            };

            // D. Persist
            _context.NumberSequences.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(entity.Id, "Number sequence created successfully.");
        }
    }
}
