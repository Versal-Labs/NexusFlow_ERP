using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.System.Secrets
{
    public sealed class GetSecretSettingsStatusQuery : IRequest<Result<SecretSettingsStatusDto>>
    {
    }

    public sealed class GetSecretSettingsStatusHandler
        : IRequestHandler<GetSecretSettingsStatusQuery, Result<SecretSettingsStatusDto>>
    {
        private readonly IInstallationSecretStore _secretStore;
        private readonly IInstallationSecretStoreDiagnostics _diagnostics;
        private readonly IInstallationRuntimeContext _runtimeContext;
        private readonly IErpDbContext _context;

        public GetSecretSettingsStatusHandler(
            IInstallationSecretStore secretStore,
            IInstallationSecretStoreDiagnostics diagnostics,
            IInstallationRuntimeContext runtimeContext,
            IErpDbContext context)
        {
            _secretStore = secretStore;
            _diagnostics = diagnostics;
            _runtimeContext = runtimeContext;
            _context = context;
        }

        public async Task<Result<SecretSettingsStatusDto>> Handle(
            GetSecretSettingsStatusQuery request,
            CancellationToken cancellationToken)
        {
            var restartAt = _secretStore.Get(InstallationSecretKeys.RestartRequiredAtUtc);
            var restartReason = _secretStore.Get(InstallationSecretKeys.RestartRequiredReason);
            var auditLookup = await _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.EntityName.StartsWith(SecretAudit.EntityPrefix))
                .GroupBy(x => x.EntityName)
                .Select(x => new { EntityName = x.Key, LastAuditAtUtc = x.Max(a => a.Timestamp) })
                .ToDictionaryAsync(x => x.EntityName, x => x.LastAuditAtUtc, cancellationToken);

            var dto = new SecretSettingsStatusDto
            {
                InstanceId = _runtimeContext.InstanceId,
                DeploymentProfile = _runtimeContext.DeploymentProfile,
                StorageMode = _runtimeContext.StorageMode,
                AzureBlobContainer = _runtimeContext.AzureBlobStorageContainer,
                RestartRequired = !string.IsNullOrWhiteSpace(restartAt),
                RestartRequiredAtUtc = restartAt,
                RestartRequiredReason = restartReason
            };

            foreach (var definition in SecretRegistry.All)
            {
                var diagnostic = _diagnostics.Inspect(definition.Key);
                auditLookup.TryGetValue(SecretAudit.EntityName(definition), out var lastAudit);

                dto.Items.Add(new SecretSettingStatusItemDto
                {
                    Key = definition.Key,
                    DisplayName = definition.DisplayName,
                    Category = definition.Category,
                    Description = definition.Description,
                    Kind = definition.Kind.ToString(),
                    InputMode = definition.InputMode,
                    Configured = diagnostic.HasValue,
                    Source = diagnostic.Source,
                    Fingerprint = diagnostic.Fingerprint,
                    HasStoredValue = diagnostic.HasStoredValue,
                    HasPlatformValue = diagnostic.HasPlatformValue,
                    PlatformOverrideActive = diagnostic.HasPlatformValue && diagnostic.HasStoredValue,
                    CanRemove = definition.CanRemove,
                    CanRotate = definition.CanRotate,
                    RequiresRestart = definition.RequiresRestart,
                    LastAuditAtUtc = lastAudit == default ? null : lastAudit.ToString("O"),
                    Warning = diagnostic.HasPlatformValue && diagnostic.HasStoredValue
                        ? "A platform/environment value is overriding the saved secret. Remove the platform value before restart if you want the saved value to take effect."
                        : null
                });
            }

            return Result<SecretSettingsStatusDto>.Success(dto);
        }
    }

    public sealed class TestSecretSettingCommand : IRequest<Result<SecretValidationResultDto>>
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public sealed class TestSecretSettingHandler
        : IRequestHandler<TestSecretSettingCommand, Result<SecretValidationResultDto>>
    {
        private readonly ISecretValidationService _validationService;

        public TestSecretSettingHandler(ISecretValidationService validationService)
        {
            _validationService = validationService;
        }

        public async Task<Result<SecretValidationResultDto>> Handle(
            TestSecretSettingCommand request,
            CancellationToken cancellationToken)
        {
            var definition = SecretRegistry.Find(request.Key);
            if (definition == null)
            {
                return Result<SecretValidationResultDto>.Failure("Unknown secret key.");
            }

            var validation = await _validationService.ValidateAsync(definition, request.Value, cancellationToken);
            return validation.IsValid
                ? Result<SecretValidationResultDto>.Success(validation, validation.Message)
                : Result<SecretValidationResultDto>.Failure(validation.Message);
        }
    }

    public sealed class SaveSecretSettingCommand : IRequest<Result<SecretMutationResultDto>>
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public sealed class SaveSecretSettingHandler
        : IRequestHandler<SaveSecretSettingCommand, Result<SecretMutationResultDto>>
    {
        private readonly IInstallationSecretStore _secretStore;
        private readonly ISecretValidationService _validationService;
        private readonly ICurrentUserPasswordValidator _passwordValidator;
        private readonly IErpDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public SaveSecretSettingHandler(
            IInstallationSecretStore secretStore,
            ISecretValidationService validationService,
            ICurrentUserPasswordValidator passwordValidator,
            IErpDbContext context,
            ICurrentUserService currentUser)
        {
            _secretStore = secretStore;
            _validationService = validationService;
            _passwordValidator = passwordValidator;
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<Result<SecretMutationResultDto>> Handle(
            SaveSecretSettingCommand request,
            CancellationToken cancellationToken)
        {
            var definition = SecretRegistry.Find(request.Key);
            if (definition == null)
            {
                return Result<SecretMutationResultDto>.Failure("Unknown secret key.");
            }

            if (!await _passwordValidator.ValidateCurrentPasswordAsync(request.CurrentPassword, cancellationToken))
            {
                await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_SAVE_DENIED", "Password confirmation failed.", cancellationToken);
                return Result<SecretMutationResultDto>.Failure("Current password confirmation failed.");
            }

            var value = (request.Value ?? string.Empty).Trim();
            if (definition.Kind == SecretSettingKind.Hangfire && string.IsNullOrWhiteSpace(value))
            {
                await _secretStore.RemoveAsync(definition.Key, cancellationToken);
                await MarkRestartRequiredAsync(definition, cancellationToken);
                await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_REMOVED", "Optional Hangfire connection cleared; it will inherit the primary database connection after restart.", cancellationToken);
                return Result<SecretMutationResultDto>.Success(
                    RestartRequired($"{definition.DisplayName} cleared. Restart is required."));
            }

            var validation = await _validationService.ValidateAsync(definition, value, cancellationToken);
            if (!validation.IsValid)
            {
                await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_SAVE_REJECTED", validation.Message, cancellationToken);
                return Result<SecretMutationResultDto>.Failure(validation.Message);
            }

            await _secretStore.SetAsync(definition.Key, value, cancellationToken);
            await MarkRestartRequiredAsync(definition, cancellationToken);
            await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_SAVED", $"{definition.DisplayName} saved. Secret value was not logged.", cancellationToken);

            return Result<SecretMutationResultDto>.Success(
                RestartRequired($"{definition.DisplayName} saved. Restart is required."));
        }

        private async Task MarkRestartRequiredAsync(SecretDefinition definition, CancellationToken cancellationToken)
        {
            await _secretStore.SetAsync(InstallationSecretKeys.RestartRequiredAtUtc, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
            await _secretStore.SetAsync(InstallationSecretKeys.RestartRequiredReason, definition.DisplayName, cancellationToken);
        }

        private static SecretMutationResultDto RestartRequired(string message)
        {
            return new SecretMutationResultDto { RestartRequired = true, Message = message };
        }
    }

    public sealed class RemoveSecretSettingCommand : IRequest<Result<SecretMutationResultDto>>
    {
        public string Key { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public sealed class RemoveSecretSettingHandler
        : IRequestHandler<RemoveSecretSettingCommand, Result<SecretMutationResultDto>>
    {
        private readonly IInstallationSecretStore _secretStore;
        private readonly ICurrentUserPasswordValidator _passwordValidator;
        private readonly IErpDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public RemoveSecretSettingHandler(
            IInstallationSecretStore secretStore,
            ICurrentUserPasswordValidator passwordValidator,
            IErpDbContext context,
            ICurrentUserService currentUser)
        {
            _secretStore = secretStore;
            _passwordValidator = passwordValidator;
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<Result<SecretMutationResultDto>> Handle(
            RemoveSecretSettingCommand request,
            CancellationToken cancellationToken)
        {
            var definition = SecretRegistry.Find(request.Key);
            if (definition == null)
            {
                return Result<SecretMutationResultDto>.Failure("Unknown secret key.");
            }

            if (!definition.CanRemove)
            {
                return Result<SecretMutationResultDto>.Failure($"{definition.DisplayName} cannot be removed from the Secret Vault.");
            }

            if (!await _passwordValidator.ValidateCurrentPasswordAsync(request.CurrentPassword, cancellationToken))
            {
                await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_REMOVE_DENIED", "Password confirmation failed.", cancellationToken);
                return Result<SecretMutationResultDto>.Failure("Current password confirmation failed.");
            }

            await _secretStore.RemoveAsync(definition.Key, cancellationToken);
            await _secretStore.SetAsync(InstallationSecretKeys.RestartRequiredAtUtc, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
            await _secretStore.SetAsync(InstallationSecretKeys.RestartRequiredReason, $"{definition.DisplayName} removed", cancellationToken);
            await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_REMOVED", $"{definition.DisplayName} removed. Secret value was not logged.", cancellationToken);

            return Result<SecretMutationResultDto>.Success(new SecretMutationResultDto
            {
                RestartRequired = true,
                Message = $"{definition.DisplayName} removed. Restart is required."
            });
        }
    }

    public sealed class RotateJwtSecretCommand : IRequest<Result<SecretMutationResultDto>>
    {
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public sealed class RotateJwtSecretHandler
        : IRequestHandler<RotateJwtSecretCommand, Result<SecretMutationResultDto>>
    {
        private readonly IInstallationSecretStore _secretStore;
        private readonly ICurrentUserPasswordValidator _passwordValidator;
        private readonly IErpDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public RotateJwtSecretHandler(
            IInstallationSecretStore secretStore,
            ICurrentUserPasswordValidator passwordValidator,
            IErpDbContext context,
            ICurrentUserService currentUser)
        {
            _secretStore = secretStore;
            _passwordValidator = passwordValidator;
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<Result<SecretMutationResultDto>> Handle(
            RotateJwtSecretCommand request,
            CancellationToken cancellationToken)
        {
            var definition = SecretRegistry.Find(InstallationSecretKeys.JwtSecret)!;
            if (!await _passwordValidator.ValidateCurrentPasswordAsync(request.CurrentPassword, cancellationToken))
            {
                await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_ROTATE_DENIED", "Password confirmation failed.", cancellationToken);
                return Result<SecretMutationResultDto>.Failure("Current password confirmation failed.");
            }

            var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            await _secretStore.SetAsync(definition.Key, generated, cancellationToken);
            await _secretStore.SetAsync(InstallationSecretKeys.RestartRequiredAtUtc, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
            await _secretStore.SetAsync(InstallationSecretKeys.RestartRequiredReason, "JWT secret rotated", cancellationToken);
            await SecretAudit.WriteAsync(_context, _currentUser, definition, "SECRET_ROTATED", "JWT secret rotated. Secret value was not logged.", cancellationToken);

            return Result<SecretMutationResultDto>.Success(new SecretMutationResultDto
            {
                RestartRequired = true,
                Message = "JWT secret rotated. Existing API tokens will be invalid after restart."
            });
        }
    }

    public sealed class RequestApplicationRestartCommand : IRequest<Result<SecretMutationResultDto>>
    {
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public sealed class RequestApplicationRestartHandler
        : IRequestHandler<RequestApplicationRestartCommand, Result<SecretMutationResultDto>>
    {
        private readonly IInstallationSecretStore _secretStore;
        private readonly ICurrentUserPasswordValidator _passwordValidator;
        private readonly IApplicationRestartService _restartService;
        private readonly IErpDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public RequestApplicationRestartHandler(
            IInstallationSecretStore secretStore,
            ICurrentUserPasswordValidator passwordValidator,
            IApplicationRestartService restartService,
            IErpDbContext context,
            ICurrentUserService currentUser)
        {
            _secretStore = secretStore;
            _passwordValidator = passwordValidator;
            _restartService = restartService;
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<Result<SecretMutationResultDto>> Handle(
            RequestApplicationRestartCommand request,
            CancellationToken cancellationToken)
        {
            var definition = SecretRegistry.Find(InstallationSecretKeys.DefaultConnection)!;
            if (!await _passwordValidator.ValidateCurrentPasswordAsync(request.CurrentPassword, cancellationToken))
            {
                await SecretAudit.WriteAsync(_context, _currentUser, definition, "APP_RESTART_DENIED", "Password confirmation failed.", cancellationToken);
                return Result<SecretMutationResultDto>.Failure("Current password confirmation failed.");
            }

            await SecretAudit.WriteAsync(_context, _currentUser, definition, "APP_RESTART_REQUESTED", "Application restart requested from Secret Vault.", cancellationToken);
            await _restartService.RequestRestartAsync(cancellationToken);

            return Result<SecretMutationResultDto>.Success(new SecretMutationResultDto
            {
                RestartRequired = false,
                Message = "Application restart requested. The hosting platform should start NexusFlow again automatically."
            });
        }
    }

    internal static class SecretAudit
    {
        public const string EntityPrefix = "SecretVault:";

        public static string EntityName(SecretDefinition definition) => $"{EntityPrefix}{definition.Key}";

        public static async Task WriteAsync(
            IErpDbContext context,
            ICurrentUserService currentUser,
            SecretDefinition definition,
            string action,
            string detail,
            CancellationToken cancellationToken)
        {
            context.AuditLogs.Add(new AuditLog
            {
                Action = action,
                EntityName = EntityName(definition),
                UserId = currentUser.UserId,
                Timestamp = DateTime.UtcNow,
                Details = $"{definition.DisplayName}: {detail}"
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
