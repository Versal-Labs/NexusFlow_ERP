using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.System.IAM.Queries
{
    public class UserGridDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class GetUsersQuery : IRequest<Result<List<UserGridDto>>> { }

    public class GetUsersHandler : IRequestHandler<GetUsersQuery, Result<List<UserGridDto>>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public GetUsersHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        public async Task<Result<List<UserGridDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            var users = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
            var userDtos = new List<UserGridDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserGridDto
                {
                    Id = user.Id,
                    FullName = user.FullName, // Mapped directly to the new property
                    Email = user.Email ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? "",
                    IsActive = user.IsActive
                });
            }
            return Result<List<UserGridDto>>.Success(userDtos.OrderBy(u => u.FullName).ToList());
        }
    }

    // QUERY TO FETCH AVAILABLE ROLES FOR THE DROPDOWN
    public class RoleDto { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; }
    public class GetRolesQuery : IRequest<Result<List<RoleDto>>> { }

    public class GetRolesHandler : IRequestHandler<GetRolesQuery, Result<List<RoleDto>>>
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        public GetRolesHandler(RoleManager<IdentityRole> roleManager) => _roleManager = roleManager;

        public async Task<Result<List<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
        {
            var roles = await _roleManager.Roles.Select(r => new RoleDto { Id = r.Id, Name = r.Name! }).ToListAsync(cancellationToken);
            return Result<List<RoleDto>>.Success(roles);
        }
    }
}
