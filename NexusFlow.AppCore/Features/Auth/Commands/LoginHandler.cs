using MediatR;
using Microsoft.AspNetCore.Identity;
using NexusFlow.AppCore.DTOs.Auth;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Auth.Commands
{
    public class LoginHandler : IRequestHandler<LoginCommand, Result<LoginResponseDto>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ITokenService _tokenService;

        public LoginHandler(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
        }

        public async Task<Result<LoginResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            // 1. Find User
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Result<LoginResponseDto>.Failure("Invalid email or password.");

            // 2. Check Password (CHANGE THIS LINE)
            // We use UserManager directly. This works perfectly in Class Libraries.
            bool isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);

            if (!isPasswordValid)
                return Result<LoginResponseDto>.Failure("Invalid email or password.");

            if (!user.IsActive)
                return Result<LoginResponseDto>.Failure("Account is deactivated.");

            // 3. Generate Token
            var roles = await _userManager.GetRolesAsync(user);

            // Handle case where user has no role assigned yet
            var primaryRole = roles.FirstOrDefault() ?? "User";

            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var roleName in roles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null) continue;

                foreach (var claim in await _roleManager.GetClaimsAsync(role))
                {
                    if (claim.Type == "Permission")
                    {
                        permissions.Add(claim.Value);
                    }
                }
            }

            var token = _tokenService.GenerateToken(user.Id, user.Email!, roles, permissions.ToList());

            return Result<LoginResponseDto>.Success(new LoginResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                Role = primaryRole
            }, "Login Successful");
        }
    }
}
