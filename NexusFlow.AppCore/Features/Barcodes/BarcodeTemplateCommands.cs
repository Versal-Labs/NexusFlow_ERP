using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Barcodes
{
    public class SaveBarcodeTemplateCommand : IRequest<Result<int>>
    {
        public BarcodeTemplateDto Template { get; set; } = new();
    }

    public class SaveBarcodeTemplateHandler : IRequestHandler<SaveBarcodeTemplateCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public SaveBarcodeTemplateHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(SaveBarcodeTemplateCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Template;
            var validationErrors = BarcodeTemplateValidator.Validate(dto);
            if (validationErrors.Count > 0)
                return Result<int>.Failure(validationErrors.ToArray());

            dto.Name = dto.Name.Trim();
            bool duplicateName = await _context.BarcodeTemplates
                .AnyAsync(x => x.Name == dto.Name && x.Id != dto.Id, cancellationToken);
            if (duplicateName)
                return Result<int>.Failure($"A barcode template named '{dto.Name}' already exists.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                if (dto.IsDefault)
                {
                    var existingDefaults = await _context.BarcodeTemplates
                        .Where(x => x.IsDefault && x.Id != dto.Id)
                        .ToListAsync(cancellationToken);
                    foreach (var existingDefault in existingDefaults)
                        existingDefault.IsDefault = false;

                    if (existingDefaults.Count > 0)
                        await _context.SaveChangesAsync(cancellationToken);
                }

                BarcodeTemplate entity;
                if (dto.Id == 0)
                {
                    entity = new BarcodeTemplate();
                    _context.BarcodeTemplates.Add(entity);
                }
                else
                {
                    entity = await _context.BarcodeTemplates
                        .FirstOrDefaultAsync(x => x.Id == dto.Id, cancellationToken)
                        ?? throw new InvalidOperationException("Barcode template not found.");
                }

                Map(dto, entity);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(entity.Id, "Barcode template saved successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure(ex.Message);
            }
        }

        private static void Map(BarcodeTemplateDto source, BarcodeTemplate target)
        {
            target.Name = source.Name;
            target.PageWidthMM = source.PageWidthMM;
            target.PageHeightMM = source.PageHeightMM;
            target.StickerWidthMM = source.StickerWidthMM;
            target.StickerHeightMM = source.StickerHeightMM;
            target.StickersPerRow = source.StickersPerRow;
            target.RowsPerPage = source.RowsPerPage;
            target.MarginTopMM = source.MarginTopMM;
            target.MarginLeftMM = source.MarginLeftMM;
            target.HorizontalGapMM = source.HorizontalGapMM;
            target.VerticalGapMM = source.VerticalGapMM;
            target.Symbology = source.Symbology;
            target.PrintCompanyName = source.PrintCompanyName;
            target.PrintProductName = source.PrintProductName;
            target.PrintPrice = source.PrintPrice;
            target.PrintSizeColor = source.PrintSizeColor;
            target.IsDefault = source.IsDefault;
        }
    }

    public record DeleteBarcodeTemplateCommand(int Id) : IRequest<Result>;

    public class DeleteBarcodeTemplateHandler : IRequestHandler<DeleteBarcodeTemplateCommand, Result>
    {
        private readonly IErpDbContext _context;

        public DeleteBarcodeTemplateHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(DeleteBarcodeTemplateCommand request, CancellationToken cancellationToken)
        {
            var template = await _context.BarcodeTemplates
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (template == null)
                return Result.Failure("Barcode template not found.");

            _context.BarcodeTemplates.Remove(template);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success("Barcode template deleted successfully.");
        }
    }
}
