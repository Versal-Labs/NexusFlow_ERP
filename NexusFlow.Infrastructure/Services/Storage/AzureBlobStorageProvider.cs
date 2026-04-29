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
        private readonly BlobServiceClient _blobServiceClient;
        private const string _providerPrefix = "azure://";

        public AzureBlobStorageProvider(string connectionString)
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        public bool CanHandle(string fileReference) => fileReference.StartsWith(_providerPrefix);

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string containerFolder, string contentType, CancellationToken cancellationToken = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerFolder.ToLower());
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            string uniqueFileName = $"{Guid.NewGuid()}-{fileName}";
            var blobClient = containerClient.GetBlobClient(uniqueFileName);

            var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } };
            await blobClient.UploadAsync(fileStream, options, cancellationToken);

            // Store an internal URI to obscure the actual Azure URL from the database
            return $"{_providerPrefix}{containerFolder}/{uniqueFileName}";
        }

        public async Task<(Stream Stream, string ContentType)> DownloadAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            var parts = fileReference.Replace(_providerPrefix, "").Split('/');
            var containerClient = _blobServiceClient.GetBlobContainerClient(parts[0]);
            var blobClient = containerClient.GetBlobClient(parts[1]);

            var downloadInfo = await blobClient.DownloadAsync(cancellationToken);
            return (downloadInfo.Value.Content, downloadInfo.Value.Details.ContentType);
        }

        public async Task DeleteAsync(string fileReference, CancellationToken cancellationToken = default)
        {
            var parts = fileReference.Replace(_providerPrefix, "").Split('/');
            var containerClient = _blobServiceClient.GetBlobContainerClient(parts[0]);
            var blobClient = containerClient.GetBlobClient(parts[1]);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }
}
