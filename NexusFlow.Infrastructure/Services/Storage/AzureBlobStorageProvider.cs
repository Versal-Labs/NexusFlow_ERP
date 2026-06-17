using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services.Storage
{
    public class AzureBlobStorageProvider : IFileStorageProvider
    {
        private readonly BlobServiceClient? _blobServiceClient;
        private readonly string? _tenantContainerName;
        private const string _providerPrefix = "azure://";

        public AzureBlobStorageProvider(string? connectionString, string? tenantContainerName = null)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                _blobServiceClient = new BlobServiceClient(connectionString);
            }

            _tenantContainerName = string.IsNullOrWhiteSpace(tenantContainerName)
                ? null
                : tenantContainerName.Trim().ToLowerInvariant();
        }

        public bool CanHandle(string fileReference) => fileReference.StartsWith(_providerPrefix);

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string containerFolder, string contentType, CancellationToken cancellationToken = default)
        {
            if (_blobServiceClient == null)
                throw new InvalidOperationException("Azure Blob Storage is not configured.");

            var containerName = _tenantContainerName ?? containerFolder.ToLowerInvariant();
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            string uniqueFileName = $"{Guid.NewGuid()}-{fileName}";
            var blobName = _tenantContainerName == null
                ? uniqueFileName
                : $"{NormalizeBlobFolder(containerFolder)}/{uniqueFileName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } };
            await blobClient.UploadAsync(fileStream, options, cancellationToken);

            // Store an internal URI to obscure the actual Azure URL from the database
            return $"{_providerPrefix}{containerName}/{blobName}";
        }

        public async Task<(Stream Stream, string ContentType)> DownloadAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            if (_blobServiceClient == null)
                throw new InvalidOperationException("Azure Blob Storage is not configured.");

            var parts = fileReference.Replace(_providerPrefix, "").Split('/');
            var containerClient = _blobServiceClient.GetBlobContainerClient(parts[0]);
            var blobName = string.Join('/', parts.Skip(1));
            var blobClient = containerClient.GetBlobClient(blobName);

            var downloadInfo = await blobClient.DownloadAsync(cancellationToken);
            return (downloadInfo.Value.Content, downloadInfo.Value.Details.ContentType);
        }

        public async Task DeleteAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            if (_blobServiceClient == null)
                throw new InvalidOperationException("Azure Blob Storage is not configured.");

            var parts = fileReference.Replace(_providerPrefix, "").Split('/');
            var containerClient = _blobServiceClient.GetBlobContainerClient(parts[0]);
            var blobName = string.Join('/', parts.Skip(1));
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        private static string NormalizeBlobFolder(string value)
        {
            var normalized = value.Replace('\\', '/').Trim('/');
            return string.IsNullOrWhiteSpace(normalized) ? "files" : normalized;
        }
    }
}
