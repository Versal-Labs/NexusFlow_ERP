using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    public class NameUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // 1. Try the standard Identity Cookie Claim
            var id = connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // 2. If missing, try the standard JWT Claim
            if (string.IsNullOrEmpty(id))
            {
                id = connection.User?.FindFirst("sub")?.Value;
            }

            return id;
        }
    }
}
