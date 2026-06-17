using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.System.DocumentTemplates
{
    public class GetDocumentTemplatesQuery : IRequest<Result<List<DocumentTemplateDto>>>
    {
        public DocumentType? DocumentType { get; set; }
    }

    public class GetDocumentTemplatesHandler : IRequestHandler<GetDocumentTemplatesQuery, Result<List<DocumentTemplateDto>>>
    {
        private readonly IErpDbContext _context;

        public GetDocumentTemplatesHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<DocumentTemplateDto>>> Handle(GetDocumentTemplatesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.DocumentTemplates.AsNoTracking();
            if (request.DocumentType.HasValue)
                query = query.Where(x => x.DocumentType == request.DocumentType.Value);

            var templates = await query
                .OrderBy(x => x.DocumentType)
                .ThenBy(x => x.TaxProfile)
                .ThenByDescending(x => x.IsDefault)
                .ThenBy(x => x.TemplateName)
                .Select(x => new DocumentTemplateDto
                {
                    Id = x.Id,
                    DocumentType = x.DocumentType,
                    TemplateName = x.TemplateName,
                    TaxProfile = x.TaxProfile,
                    BlobUrl = x.BlobUrl,
                    IsDefault = x.IsDefault,
                    IsActive = x.IsActive
                })
                .ToListAsync(cancellationToken);

            return Result<List<DocumentTemplateDto>>.Success(templates);
        }
    }
}
