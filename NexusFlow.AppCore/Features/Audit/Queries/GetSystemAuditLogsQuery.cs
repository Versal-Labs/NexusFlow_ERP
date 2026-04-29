using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Audit.Queries
{
    public class SystemAuditLogDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Details { get; set; } = string.Empty;
        public string? IPAddress { get; set; }
    }

    public class GetSystemAuditLogsQuery : IRequest<Result<IEnumerable<SystemAuditLogDto>>>
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Action { get; set; }
        public string? UserId { get; set; }
        public string? SearchTerm { get; set; }
    }

    public class GetSystemAuditLogsHandler : IRequestHandler<GetSystemAuditLogsQuery, Result<IEnumerable<SystemAuditLogDto>>>
    {
        private readonly string _connectionString;

        public GetSystemAuditLogsHandler(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Database connection string is missing.");
        }

        public async Task<Result<IEnumerable<SystemAuditLogDto>>> Handle(GetSystemAuditLogsQuery request, CancellationToken cancellationToken)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = new StringBuilder(@"
                SELECT Id, Action, EntityName, UserId, Timestamp, Details, IPAddress
                FROM [System].[AuditLogs]
                WHERE 1=1 ");

            var parameters = new DynamicParameters();

            if (request.StartDate.HasValue)
            {
                sql.Append(" AND Timestamp >= @StartDate ");
                parameters.Add("StartDate", request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                sql.Append(" AND Timestamp <= @EndDate ");
                // Add 1 day to include the entire end date
                parameters.Add("EndDate", request.EndDate.Value.AddDays(1));
            }

            if (!string.IsNullOrWhiteSpace(request.Action))
            {
                sql.Append(" AND Action = @Action ");
                parameters.Add("Action", request.Action);
            }

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                sql.Append(" AND UserId = @UserId ");
                parameters.Add("UserId", request.UserId);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                sql.Append(" AND (Details LIKE @Search OR EntityName LIKE @Search) ");
                parameters.Add("Search", $"%{request.SearchTerm}%");
            }

            sql.Append(" ORDER BY Timestamp DESC "); // Newest first

            var logs = await connection.QueryAsync<SystemAuditLogDto>(sql.ToString(), parameters);

            return Result<IEnumerable<SystemAuditLogDto>>.Success(logs);
        }
    }
}
