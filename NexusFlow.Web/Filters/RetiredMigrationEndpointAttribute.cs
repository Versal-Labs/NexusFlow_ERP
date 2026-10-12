using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Web.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RetiredMigrationEndpointAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<IErpDbContext>();
        if (HttpMethods.IsPost(context.HttpContext.Request.Method) && await db.MigrationProjects.AnyAsync(context.HttpContext.RequestAborted))
        { context.Result = new ObjectResult(new { message = "Opening imports have moved to Administration → Migration Centre (/Migration). Existing migration batches remain available for read-only audit." }) { StatusCode = StatusCodes.Status410Gone }; return; }
        await next();
    }
}
