using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Security
{
    // 1. The Requirement
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }
        public PermissionRequirement(string permission) => Permission = permission;
    }

    // 2. The Handler (Checks the JWT / Cookie Claims)
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User == null)
                return Task.CompletedTask;

            // Check if the user has the specific permission claim, OR if they are a SuperAdmin
            var hasPermission = context.User.Claims.Any(c =>
                c.Type == "Permission" &&
                (c.Value == requirement.Permission || c.Value == Permissions.SuperAdmin));

            if (hasPermission)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    // 3. The Dynamic Policy Provider
    public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // First check if it's a standard policy (like your HybridPolicy)
            var policy = await base.GetPolicyAsync(policyName);

            if (policy == null)
            {
                // If it starts with "Permissions.", build the policy dynamically!
                if (policyName.StartsWith("Permissions.", StringComparison.OrdinalIgnoreCase))
                {
                    var builder = new AuthorizationPolicyBuilder();
                    builder.AddRequirements(new PermissionRequirement(policyName));
                    return builder.Build();
                }
            }

            return policy;
        }
    }
}
