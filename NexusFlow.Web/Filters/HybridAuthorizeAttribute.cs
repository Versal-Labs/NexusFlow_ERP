using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NexusFlow.Web.Filters
{
    // Apply this to your Hybrid API Controllers
    public class HybridAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // 1. If user is not authenticated at all, let the standard [Authorize] handle it
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                return;
            }

            // 2. Check how they authenticated
            // If they are using a Bearer Token (Mobile), skip CSRF check
            if (context.HttpContext.User.Identity.AuthenticationType == "AuthenticationTypes.Federation" ||
                context.HttpContext.Request.Headers.ContainsKey("Authorization"))
            {
                return;
            }

            // 3. If they are using Cookies (ASP.NET Internal), VALIDATE ANTIFORGERY
            // We manually invoke the Antiforgery service
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new BadRequestObjectResult("Anti-Forgery token validation failed.");
            }
        }
    }
}
