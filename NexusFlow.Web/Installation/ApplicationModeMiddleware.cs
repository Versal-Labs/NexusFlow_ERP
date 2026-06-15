using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Web.Installation
{
    public sealed class ApplicationModeMiddleware
    {
        private readonly RequestDelegate _next;

        public ApplicationModeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IInstallationStateStore stateStore)
        {
            var mode = stateStore.Get().Mode;
            if (mode == ApplicationMode.Installed || IsAlwaysAllowed(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (mode == ApplicationMode.UpgradeRequired && IsUpgradeAllowed(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "NexusFlow is not ready",
                    status = StatusCodes.Status503ServiceUnavailable,
                    mode = mode.ToString()
                });
                return;
            }

            var destination = mode == ApplicationMode.UpgradeRequired
                ? "/maintenance/upgrade"
                : "/install";
            context.Response.Redirect(destination);
        }

        private static bool IsAlwaysAllowed(PathString path) =>
            path.StartsWithSegments("/install") ||
            path.StartsWithSegments("/maintenance") ||
            path.StartsWithSegments("/health") ||
            path.StartsWithSegments("/css") ||
            path.StartsWithSegments("/js") ||
            path.StartsWithSegments("/Assets") ||
            path.StartsWithSegments("/favicon.ico");

        private static bool IsUpgradeAllowed(PathString path) =>
            path.StartsWithSegments("/Account") ||
            path.StartsWithSegments("/maintenance") ||
            IsAlwaysAllowed(path);
    }
}
