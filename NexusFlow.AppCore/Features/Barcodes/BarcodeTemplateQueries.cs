using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.AppCore.Features.Barcodes
{
    public record GetBarcodeTemplatesQuery : IRequest<List<BarcodeTemplateDto>>;

    public class GetBarcodeTemplatesHandler : IRequestHandler<GetBarcodeTemplatesQuery, List<BarcodeTemplateDto>>
    {
        private readonly IErpDbContext _context;

        public GetBarcodeTemplatesHandler(IErpDbContext context)
        {
            _context = context;
        }

        public Task<List<BarcodeTemplateDto>> Handle(GetBarcodeTemplatesQuery request, CancellationToken cancellationToken)
        {
            return _context.BarcodeTemplates
                .AsNoTracking()
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .Select(x => new BarcodeTemplateDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    PageWidthMM = x.PageWidthMM,
                    PageHeightMM = x.PageHeightMM,
                    StickerWidthMM = x.StickerWidthMM,
                    StickerHeightMM = x.StickerHeightMM,
                    StickersPerRow = x.StickersPerRow,
                    RowsPerPage = x.RowsPerPage,
                    MarginTopMM = x.MarginTopMM,
                    MarginLeftMM = x.MarginLeftMM,
                    HorizontalGapMM = x.HorizontalGapMM,
                    VerticalGapMM = x.VerticalGapMM,
                    Symbology = x.Symbology,
                    PrintCompanyName = x.PrintCompanyName,
                    PrintProductName = x.PrintProductName,
                    PrintPrice = x.PrintPrice,
                    PrintSizeColor = x.PrintSizeColor,
                    IsDefault = x.IsDefault
                })
                .ToListAsync(cancellationToken);
        }
    }

    public record SearchBarcodeVariantsQuery(string? Query) : IRequest<List<BarcodeVariantSearchDto>>;

    public class SearchBarcodeVariantsHandler : IRequestHandler<SearchBarcodeVariantsQuery, List<BarcodeVariantSearchDto>>
    {
        private readonly IErpDbContext _context;

        public SearchBarcodeVariantsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public Task<List<BarcodeVariantSearchDto>> Handle(SearchBarcodeVariantsQuery request, CancellationToken cancellationToken)
        {
            string? search = string.IsNullOrWhiteSpace(request.Query) ? null : request.Query.Trim();

            return _context.ProductVariants
                .AsNoTracking()
                .Where(x => x.IsActive &&
                    (search == null || x.Name.Contains(search) || x.SKU.Contains(search) || (x.Barcode != null && x.Barcode.Contains(search))))
                .OrderBy(x => x.Name)
                .ThenBy(x => x.SKU)
                .Take(50)
                .Select(x => new BarcodeVariantSearchDto
                {
                    Id = x.Id,
                    SKU = x.SKU,
                    Name = x.Name
                })
                .ToListAsync(cancellationToken);
        }
    }
}
