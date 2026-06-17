using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure.Installation;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services.Storage
{
    public class GlobalStorageCoordinator : IGlobalStorageCoordinator
    {
        private readonly AzureBlobStorageProvider _primaryProvider;
        private readonly LocalDiskStorageProvider _fallbackProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GlobalStorageCoordinator> _logger;
        private readonly InstallationRuntimeOptions _runtimeOptions;

        public GlobalStorageCoordinator(
            AzureBlobStorageProvider primaryProvider,
            LocalDiskStorageProvider fallbackProvider,
            IServiceProvider serviceProvider,
            ILogger<GlobalStorageCoordinator> logger,
            InstallationRuntimeOptions runtimeOptions)
        {
            _primaryProvider = primaryProvider;
            _fallbackProvider = fallbackProvider;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _runtimeOptions = runtimeOptions;
        }

        public async Task<string> SaveFileSecurelyAsync(Stream fileStream, string fileName, string moduleFolder, string contentType, CancellationToken cancellationToken = default)
        {
            // Ensure stream is at the beginning
            if (fileStream.CanSeek) fileStream.Position = 0;

            if (_runtimeOptions.StorageMode == StorageMode.Local)
            {
                return await _fallbackProvider.UploadAsync(fileStream, fileName, moduleFolder, contentType, cancellationToken);
            }

            try
            {
                return await _primaryProvider.UploadAsync(fileStream, fileName, moduleFolder, contentType, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_runtimeOptions.StorageMode == StorageMode.AzureBlob)
                {
                    _logger.LogError(ex, "Azure Blob Storage failed in AzureBlob storage mode.");
                    throw;
                }

                _logger.LogError(ex, "Azure Blob Storage failed. Falling back to local storage.");

                // Audit the failure asynchronously without blocking the main thread
                _ = AuditStorageFailureAsync(ex.Message, moduleFolder);

                // Reset stream position for the fallback attempt
                if (fileStream.CanSeek) fileStream.Position = 0;

                return await _fallbackProvider.UploadAsync(fileStream, fileName, moduleFolder, contentType, cancellationToken);
            }
        }

        public async Task<(Stream Stream, string ContentType)> RetrieveFileAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            if (_primaryProvider.CanHandle(fileReference))
                return await _primaryProvider.DownloadAsync(fileReference, cancellationToken);

            if (_fallbackProvider.CanHandle(fileReference))
                return await _fallbackProvider.DownloadAsync(fileReference, cancellationToken);

            throw new InvalidOperationException("Unrecognized file storage reference.");
        }

        public async Task DeleteFileAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            if (_primaryProvider.CanHandle(fileReference))
            {
                await _primaryProvider.DeleteAsync(fileReference, cancellationToken);
                return;
            }

            if (_fallbackProvider.CanHandle(fileReference))
            {
                await _fallbackProvider.DeleteAsync(fileReference, cancellationToken);
                return;
            }

            throw new InvalidOperationException("Unrecognized file storage reference.");
        }

        private async Task AuditStorageFailureAsync(string error, string module)
        {
            // Create a new scope so we don't interfere with the DbContext of the ongoing HTTP Request
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IErpDbContext>();

            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "STORAGE_FAILURE_FALLBACK",
                EntityName = module,
                Details = $"Fallback to Local Disk triggered. Error: {error}",
                Timestamp = DateTime.UtcNow,
                UserId = "SYSTEM" // Or capture current user context
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
