using Microsoft.Extensions.Logging;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Services
{
    public class SmsGatewayService : ISmsGatewayService
    {
        private readonly ILogger<SmsGatewayService> _logger;

        public SmsGatewayService(ILogger<SmsGatewayService> logger)
        {
            _logger = logger;
        }

        public Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            _logger.LogWarning("SMS delivery was requested, but no SMS provider is configured.");
            return Task.FromResult(false);
        }
    }
}
