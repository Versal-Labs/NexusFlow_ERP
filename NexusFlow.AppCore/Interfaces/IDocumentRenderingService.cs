using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.Domain.Enums;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IDocumentRenderingService
    {
        /// <summary>
        /// Renders a document into PDF bytes based on its type and data.
        /// </summary>
        Task<byte[]> RenderDocumentToPdfAsync(DocumentType documentType, PrintDocumentDto data, CancellationToken cancellationToken = default);
    }
}
