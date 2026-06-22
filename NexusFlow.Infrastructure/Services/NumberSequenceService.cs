using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Exceptions;

namespace NexusFlow.Infrastructure.Services
{
    public class NumberSequenceService : INumberSequenceService
    {
        private readonly IErpDbContext _context;
        private readonly IConfiguration _configuration;

        public NumberSequenceService(IErpDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<string> GenerateNextNumberAsync(string moduleName, CancellationToken ct)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return await GenerateAtomicallyAsync(connectionString, moduleName, ct);
            }

            // Used only by non-SQL test providers. Production always takes the atomic SQL path.
            var sequence = await _context.NumberSequences.FirstOrDefaultAsync(x => x.Module == moduleName, ct)
                ?? throw new MissingNumberSequenceException(moduleName);

            var number = sequence.NextNumber++;
            sequence.LastUsed = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return Format(sequence.Prefix, sequence.Delimiter, number, sequence.Suffix);
        }

        private static async Task<string> GenerateAtomicallyAsync(
            string connectionString,
            string moduleName,
            CancellationToken cancellationToken)
        {
            const string sql = """
                UPDATE [Config].[NumberSequences] WITH (UPDLOCK, ROWLOCK)
                SET [NextNumber] = [NextNumber] + 1,
                    [LastUsed] = SYSUTCDATETIME()
                OUTPUT deleted.[NextNumber], inserted.[Prefix], inserted.[Delimiter], inserted.[Suffix]
                WHERE [Module] = @module;
                """;

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@module", moduleName);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new MissingNumberSequenceException(moduleName);
            }

            return Format(
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.GetInt32(0),
                reader.IsDBNull(3) ? null : reader.GetString(3));
        }

        private static string Format(string prefix, string? delimiter, int number, string? suffix) =>
            $"{prefix}{delimiter}{number}{suffix}";
    }
}
