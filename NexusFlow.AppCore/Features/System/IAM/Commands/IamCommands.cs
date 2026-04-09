using MediatR;
using Microsoft.AspNetCore.Identity;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.System.IAM.Commands
{
    public class CreateUserCommand : IRequest<Result<string>>
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<string>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public CreateUserHandler(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager; _roleManager = roleManager;
        }

        public async Task<Result<string>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null) return Result<string>.Failure("Email is already in use.");

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName, // Mapped to FullName
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return Result<string>.Failure($"Creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            // Assign Role
            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var roleExists = await _roleManager.RoleExistsAsync(request.Role);
                if (roleExists) await _userManager.AddToRoleAsync(user, request.Role);
            }

            return Result<string>.Success(user.Id, "User created successfully.");
        }
    }

    // ==========================================
    // 2. UPDATE USER COMMAND
    // ==========================================
    public class UpdateUserCommand : IRequest<Result<bool>>
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, Result<bool>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public UpdateUserHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        public async Task<Result<bool>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null) return Result<bool>.Failure("User not found.");

            user.FullName = request.FullName; // Mapped to FullName
            await _userManager.UpdateAsync(user);

            // Handle Role Change
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(request.Role) && !string.IsNullOrWhiteSpace(request.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, request.Role);
            }

            return Result<bool>.Success(true, "User updated successfully.");
        }
    }

    // ==========================================
    // 3. TOGGLE USER STATUS (Activate/Deactivate)
    // ==========================================
    public class ToggleUserStatusCommand : IRequest<Result<bool>>
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class ToggleUserStatusHandler : IRequestHandler<ToggleUserStatusCommand, Result<bool>>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ToggleUserStatusHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        public async Task<Result<bool>> Handle(ToggleUserStatusCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null) return Result<bool>.Failure("User not found.");

            // Prevent the Super Admin from deactivating themselves!
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SuperAdmin"))
                return Result<bool>.Failure("You cannot deactivate a Super Administrator.");

            user.IsActive = !user.IsActive; // Flip the status

            // If deactivated, lock them out immediately
            if (!user.IsActive)
            {
                user.LockoutEnd = DateTimeOffset.MaxValue;
            }
            else
            {
                user.LockoutEnd = null; // Unlock
            }

            await _userManager.UpdateAsync(user);
            return Result<bool>.Success(user.IsActive, $"User {(user.IsActive ? "Activated" : "Deactivated")} successfully.");
        }
    }

    // ==========================================
    // 4. CREATE ROLE COMMAND
    // ==========================================
    public class CreateRoleCommand : IRequest<Result<string>>
    {
        public string RoleName { get; set; } = string.Empty;
    }

    public class CreateRoleHandler : IRequestHandler<CreateRoleCommand, Result<string>>
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public CreateRoleHandler(RoleManager<IdentityRole> roleManager) => _roleManager = roleManager;

        public async Task<Result<string>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RoleName))
                return Result<string>.Failure("Role name is required.");

            string cleanRoleName = request.RoleName.Trim().Replace(" ", ""); // Standardize: "Junior Accountant"

            if (await _roleManager.RoleExistsAsync(cleanRoleName))
                return Result<string>.Failure($"The role '{cleanRoleName}' already exists.");

            var result = await _roleManager.CreateAsync(new IdentityRole(cleanRoleName));

            if (result.Succeeded)
                return Result<string>.Success(cleanRoleName, $"Role '{cleanRoleName}' created successfully.");

            return Result<string>.Failure(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    // ==========================================
    // UPDATE ROLE COMMAND
    // ==========================================
    public class UpdateRoleCommand : IRequest<Result<bool>>
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    public class UpdateRoleHandler : IRequestHandler<UpdateRoleCommand, Result<bool>>
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        public UpdateRoleHandler(RoleManager<IdentityRole> roleManager) => _roleManager = roleManager;

        public async Task<Result<bool>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return Result<bool>.Failure("Role not found.");

            string cleanRoleName = request.RoleName.Trim().Replace(" ", "");
            if (role.Name != cleanRoleName && await _roleManager.RoleExistsAsync(cleanRoleName))
                return Result<bool>.Failure($"Role '{cleanRoleName}' already exists.");

            role.Name = cleanRoleName;
            await _roleManager.UpdateAsync(role);

            return Result<bool>.Success(true, "Role updated successfully.");
        }
    }
}
