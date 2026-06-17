using System.Xml.Linq;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class AzureBlobXmlRepository : IXmlRepository
    {
        private readonly BlobContainerClient _containerClient;
        private readonly BlobClient _blobClient;
        private readonly object _sync = new();

        public AzureBlobXmlRepository(string? connectionString, string containerName, string blobName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Azure Blob Storage connection is required for Data Protection key persistence.");
            }

            _containerClient = new BlobContainerClient(connectionString, containerName);
            _blobClient = _containerClient.GetBlobClient(blobName);
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            lock (_sync)
            {
                _containerClient.CreateIfNotExists();
                if (!_blobClient.Exists())
                {
                    return Array.Empty<XElement>();
                }

                var content = _blobClient.DownloadContent().Value.Content.ToString();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Array.Empty<XElement>();
                }

                var document = XDocument.Parse(content);
                return document.Root?.Elements().Select(element => new XElement(element)).ToArray()
                    ?? Array.Empty<XElement>();
            }
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            lock (_sync)
            {
                _containerClient.CreateIfNotExists();
                var document = LoadDocument();
                var stored = new XElement(element);
                stored.SetAttributeValue("friendlyName", friendlyName);
                document.Root!.Add(stored);
                _blobClient.Upload(BinaryData.FromString(document.ToString(SaveOptions.DisableFormatting)), overwrite: true);
            }
        }

        private XDocument LoadDocument()
        {
            if (!_blobClient.Exists())
            {
                return new XDocument(new XElement("repository"));
            }

            var content = _blobClient.DownloadContent().Value.Content.ToString();
            return string.IsNullOrWhiteSpace(content)
                ? new XDocument(new XElement("repository"))
                : XDocument.Parse(content);
        }
    }
}
