using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;

namespace NexusFlow.Infrastructure.Identity
{
    public sealed class CurrentUserPasswordValidator : ICurrentUserPasswordValidator
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;

        public CurrentUserPasswordValidator(
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager)
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public async Task<bool> ValidateCurrentPasswordAsync(
            string password,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            return await _userManager.CheckPasswordAsync(user, password);
        }
    }
}
