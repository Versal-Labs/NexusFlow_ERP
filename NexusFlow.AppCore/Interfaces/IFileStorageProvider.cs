using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IFileStorageProvider
    {
        // Used to determine if a stored path belongs to this provider when downloading
        bool CanHandle(string fileReference);

        Task<string> UploadAsync(Stream fileStream, string fileName, string containerFolder, string contentType, CancellationToken cancellationToken = default);

        Task<(Stream Stream, string ContentType)> DownloadAsync(string fileReference, CancellationToken cancellationToken = default);

        Task DeleteAsync(string fileReference, CancellationToken cancellationToken = default);
    }

    // The global interface injected into Command Handlers
    public interface IGlobalStorageCoordinator
    {
        Task<string> SaveFileSecurelyAsync(Stream fileStream, string fileName, string moduleFolder, string contentType, CancellationToken cancellationToken = default);
        Task<(Stream Stream, string ContentType)> RetrieveFileAsync(string fileReference, CancellationToken cancellationToken = default);
    }
}
