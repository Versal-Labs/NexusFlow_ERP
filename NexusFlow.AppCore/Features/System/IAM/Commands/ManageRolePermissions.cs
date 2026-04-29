using MediatR;
using Microsoft.AspNetCore.Identity;
using NexusFlow.AppCore.Constants;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace NexusFlow.AppCore.Features.System.IAM.Commands
{
    // --- 1. DTOs ---
    public class PermissionDto
    {
        public string Module { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty;
    }

    public class RolePermissionsDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<string> AssignedPermissions { get; set; } = new();
    }

    // --- 2. GET ALL AVAILABLE PERMISSIONS FROM CONSTANTS ---
    public class GetAllPermissionsQuery : IRequest<Result<List<PermissionDto>>> { }

    public class GetAllPermissionsHandler : IRequestHandler<GetAllPermissionsQuery, Result<List<PermissionDto>>>
    {
        public Task<Result<List<PermissionDto>>> Handle(GetAllPermissionsQuery request, CancellationToken cancellationToken)
        {
            var permissions = new List<PermissionDto>();
            var modules = typeof(Permissions).GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

            foreach (var module in modules)
            {
                var fields = module.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                foreach (var fi in fields)
                {
                    var propertyValue = fi.GetValue(null);
                    if (propertyValue != null)
                    {
                        permissions.Add(new PermissionDto { Module = module.Name, Permission = propertyValue.ToString() });
                    }
                }
            }
            return Task.FromResult(Result<List<PermissionDto>>.Success(permissions));
        }
    }

    // --- 3. GET A SPECIFIC ROLE'S PERMISSIONS ---
    public class GetRolePermissionsQuery : IRequest<Result<RolePermissionsDto>> { public string RoleId { get; set; } = string.Empty; }

    public class GetRolePermissionsHandler : IRequestHandler<GetRolePermissionsQuery, Result<RolePermissionsDto>>
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        public GetRolePermissionsHandler(RoleManager<IdentityRole> roleManager) => _roleManager = roleManager;

        public async Task<Result<RolePermissionsDto>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return Result<RolePermissionsDto>.Failure("Role not found.");

            var claims = await _roleManager.GetClaimsAsync(role);
            var assigned = claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList();

            return Result<RolePermissionsDto>.Success(new RolePermissionsDto { RoleId = role.Id, RoleName = role.Name, AssignedPermissions = assigned });
        }
    }

    // --- 4. SAVE ROLE PERMISSIONS ---
    public class UpdateRolePermissionsCommand : IRequest<Result<int>>
    {
        public string RoleId { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    public class UpdateRolePermissionsHandler : IRequestHandler<UpdateRolePermissionsCommand, Result<int>>
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        public UpdateRolePermissionsHandler(RoleManager<IdentityRole> roleManager) => _roleManager = roleManager;

        public async Task<Result<int>> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return Result<int>.Failure("Role not found.");

            // 1. Get existing claims
            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var existingPermissions = existingClaims.Where(c => c.Type == "Permission").ToList();

            // 2. Remove old permissions
            foreach (var claim in existingPermissions)
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }

            // 3. Add new permissions
            foreach (var perm in request.Permissions)
            {
                await _roleManager.AddClaimAsync(role, new Claim("Permission", perm));
            }

            return Result<int>.Success(1, "Permissions updated successfully.");
        }
    }
}
