using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services.Storage
{
    public class LocalDiskStorageProvider : IFileStorageProvider
    {
        private readonly string _basePath;
        private const string _providerPrefix = "local://";

        public LocalDiskStorageProvider(string basePath)
        {
            _basePath = basePath;
        }

        public bool CanHandle(string fileReference) => fileReference.StartsWith(_providerPrefix);

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string containerFolder, string contentType, CancellationToken cancellationToken = default)
        {
            string targetFolder = Path.Combine(_basePath, containerFolder);
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            string uniqueFileName = $"{Guid.NewGuid()}-{fileName}";
            string filePath = Path.Combine(targetFolder, uniqueFileName);

            using var targetStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(targetStream, cancellationToken);

            return $"{_providerPrefix}{containerFolder}/{uniqueFileName}";
        }

        public async Task<(Stream Stream, string ContentType)> DownloadAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            string relativePath = fileReference.Replace(_providerPrefix, "").Replace("/", "\\");
            string filePath = Path.Combine(_basePath, relativePath);

            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found on local fallback disk.");

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Content type mapping logic omitted for brevity (use FileExtensionContentTypeProvider)
            return (stream, "application/octet-stream");
        }

        public async Task DeleteAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            string relativePath = fileReference.Replace(_providerPrefix, "").Replace("/", "\\");
            string filePath = Path.Combine(_basePath, relativePath);
            if (File.Exists(filePath)) File.Delete(filePath);
            await Task.CompletedTask;
        }
    }
}
