using NexusFlow.AppCore.Features.System.Secrets;

namespace NexusFlow.AppCore.Interfaces
{
    public interface ISecretValidationService
    {
        Task<SecretValidationResultDto> ValidateAsync(
            SecretDefinition definition,
            string value,
            CancellationToken cancellationToken = default);
    }
}
