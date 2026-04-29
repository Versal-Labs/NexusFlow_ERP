using Microsoft.AspNetCore.Http;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace NexusFlow.Infrastructure.Identity
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Retrieves the User's ID from the JWT or Identity Cookie. 
        // Falls back to "SYSTEM" if this is a background worker without an HTTP Context.
        public string UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "SYSTEM";

        public string UserName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        public int? EmployeeId
        {
            get
            {
                var empClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("EmployeeId");
                return int.TryParse(empClaim, out int empId) ? empId : null;
            }
        }

        // TIER-1: The core permission checker used by MediatR Handlers
        public bool HasPermission(string permission)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated) return false;

            return user.Claims.Any(c =>
                c.Type == "Permission" &&
                (c.Value == permission || c.Value == Permissions.SuperAdmin));
        }
    }
}
