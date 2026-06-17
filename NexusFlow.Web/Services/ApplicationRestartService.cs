using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Web.Services
{
    public sealed class ApplicationRestartService : IApplicationRestartService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<ApplicationRestartService> _logger;

        public ApplicationRestartService(
            IHostApplicationLifetime lifetime,
            ILogger<ApplicationRestartService> logger)
        {
            _lifetime = lifetime;
            _logger = logger;
        }

        public Task RequestRestartAsync(CancellationToken cancellationToken = default)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
                    _logger.LogWarning("Application restart requested from the SuperAdmin Secret Vault.");
                    _lifetime.StopApplication();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to request application restart.");
                }
            }, CancellationToken.None);

            return Task.CompletedTask;
        }
    }
}
