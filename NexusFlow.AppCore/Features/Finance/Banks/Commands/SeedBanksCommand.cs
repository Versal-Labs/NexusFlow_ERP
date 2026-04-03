using MediatR;
using NexusFlow.AppCore.Interfaces;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusFlow.AppCore.Features.Finance.Banks.Commands
{
    // ==========================================
    // 1. TEMPORARY JSON DTOs (Matches sri_lanka_banks.json)
    // ==========================================
    public class BankRootJson
    {
        [JsonPropertyName("banks")]
        public List<BankJson> Banks { get; set; } = new();
    }

    public class BankJson
    {
        [JsonPropertyName("bank_name")] public string BankName { get; set; } = string.Empty;
        [JsonPropertyName("bank_code")] public string BankCode { get; set; } = string.Empty;
        [JsonPropertyName("swift_code")] public string? SwiftCode { get; set; }
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("branches")] public List<BranchJson> Branches { get; set; } = new();
    }

    public class BranchJson
    {
        [JsonPropertyName("branch_code")] public string BranchCode { get; set; } = string.Empty;
        [JsonPropertyName("branch_name")] public string BranchName { get; set; } = string.Empty;
    }

    // ==========================================
    // 2. THE COMMAND
    // ==========================================
    public class SeedBanksCommand : IRequest<Result<string>>
    {
        // Provide the absolute or relative path to where you saved the JSON file
        public string JsonFilePath { get; set; } = string.Empty;
    }

    // ==========================================
    // 3. THE HANDLER
    // ==========================================
    public class SeedBanksHandler : IRequestHandler<SeedBanksCommand, Result<string>>
    {
        private readonly IErpDbContext _context;

        public SeedBanksHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<string>> Handle(SeedBanksCommand request, CancellationToken cancellationToken)
        {
            // ENTERPRISE GUARD: Prevent duplicate seeding!
            if (await _context.Banks.AnyAsync(cancellationToken))
            {
                return Result<string>.Failure("Aborted: The Banks table is already populated. Seeding is not required.");
            }

            if (!File.Exists(request.JsonFilePath))
            {
                return Result<string>.Failure($"File not found at path: {request.JsonFilePath}");
            }

            try
            {
                // 1. Read and Deserialize JSON
                string jsonString = await File.ReadAllTextAsync(request.JsonFilePath, cancellationToken);
                var bankData = JsonSerializer.Deserialize<BankRootJson>(jsonString);

                if (bankData == null || !bankData.Banks.Any())
                {
                    return Result<string>.Failure("The JSON file was empty or improperly formatted.");
                }

                // 2. Map to Domain Entities
                var newBanks = new List<Bank>();

                foreach (var jsonBank in bankData.Banks)
                {
                    var entityBank = new Bank
                    {
                        Name = jsonBank.BankName,
                        BankCode = jsonBank.BankCode,
                        SwiftCode = jsonBank.SwiftCode,
                        Type = jsonBank.Type,
                        IsActive = true
                    };

                    foreach (var jsonBranch in jsonBank.Branches)
                    {
                        entityBank.Branches.Add(new BankBranch
                        {
                            BranchCode = jsonBranch.BranchCode,
                            BranchName = jsonBranch.BranchName,
                            IsActive = true
                        });
                    }

                    newBanks.Add(entityBank);
                }

                // 3. Bulk Insert
                _context.Banks.AddRange(newBanks);
                await _context.SaveChangesAsync(cancellationToken);

                return Result<string>.Success($"Successfully seeded {newBanks.Count} Banks and their branches into the database.");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Seeding failed: {ex.Message}");
            }
        }
    }
}
