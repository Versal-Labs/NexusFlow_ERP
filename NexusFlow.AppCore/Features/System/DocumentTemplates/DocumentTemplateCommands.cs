using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.System.DocumentTemplates
{
    public class SaveDocumentTemplateCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public DocumentType DocumentType { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public TaxProfile TaxProfile { get; set; } = TaxProfile.All;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public Stream? TemplateStream { get; set; }
        public string? TemplateFileName { get; set; }
        public string? TemplateContentType { get; set; }
    }

    public class SaveDocumentTemplateHandler : IRequestHandler<SaveDocumentTemplateCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IGlobalStorageCoordinator _storageCoordinator;

        public SaveDocumentTemplateHandler(IErpDbContext context, IGlobalStorageCoordinator storageCoordinator)
        {
            _context = context;
            _storageCoordinator = storageCoordinator;
        }

        public async Task<Result<int>> Handle(SaveDocumentTemplateCommand request, CancellationToken cancellationToken)
        {
            var errors = Validate(request);
            if (errors.Count > 0)
                return Result<int>.Failure(errors.ToArray());

            request.TemplateName = request.TemplateName.Trim();

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            string? oldBlobUrl = null;
            try
            {
                DocumentTemplate template;
                if (request.Id == 0)
                {
                    if (request.TemplateStream == null)
                        return Result<int>.Failure("Upload a .docx file for the new document template.");

                    template = new DocumentTemplate();
                    _context.DocumentTemplates.Add(template);
                }
                else
                {
                    template = await _context.DocumentTemplates
                        .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                        ?? throw new InvalidOperationException("Document template not found.");
                }

                bool duplicateName = await _context.DocumentTemplates.AnyAsync(x =>
                    x.Id != request.Id &&
                    x.DocumentType == request.DocumentType &&
                    x.TaxProfile == request.TaxProfile &&
                    x.TemplateName == request.TemplateName,
                    cancellationToken);
                if (duplicateName)
                    return Result<int>.Failure("A document template with this name already exists for the selected document type and tax profile.");

                if (request.IsDefault)
                {
                    var existingDefaults = await _context.DocumentTemplates
                        .Where(x => x.Id != request.Id &&
                                    x.DocumentType == request.DocumentType &&
                                    x.TaxProfile == request.TaxProfile &&
                                    x.IsDefault)
                        .ToListAsync(cancellationToken);
                    foreach (var existingDefault in existingDefaults)
                        existingDefault.IsDefault = false;
                }

                template.DocumentType = request.DocumentType;
                template.TemplateName = request.TemplateName;
                template.TaxProfile = request.TaxProfile;
                template.IsActive = request.IsActive;
                template.IsDefault = request.IsDefault;

                if (request.TemplateStream != null && !string.IsNullOrWhiteSpace(request.TemplateFileName))
                {
                    oldBlobUrl = template.BlobUrl;
                    template.BlobUrl = await _storageCoordinator.SaveFileSecurelyAsync(
                        request.TemplateStream,
                        request.TemplateFileName,
                        "templates",
                        request.TemplateContentType ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(oldBlobUrl) && oldBlobUrl != template.BlobUrl)
                    await TryDeleteBlobAsync(oldBlobUrl, cancellationToken);

                return Result<int>.Success(template.Id, "Document template saved successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure(ex.Message);
            }
        }

        private static List<string> Validate(SaveDocumentTemplateCommand request)
        {
            var errors = new List<string>();

            if (!Enum.IsDefined(request.DocumentType))
                errors.Add("Select a valid document type.");
            if (!Enum.IsDefined(request.TaxProfile))
                errors.Add("Select a valid tax profile.");
            if (string.IsNullOrWhiteSpace(request.TemplateName))
                errors.Add("Template name is required.");
            else if (request.TemplateName.Trim().Length > 150)
                errors.Add("Template name cannot exceed 150 characters.");
            if (!request.IsActive && request.IsDefault)
                errors.Add("An inactive document template cannot be the default template.");

            if (request.TemplateStream != null)
            {
                if (string.IsNullOrWhiteSpace(request.TemplateFileName) ||
                    !request.TemplateFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    errors.Add("Only .docx document templates are supported.");
            }

            return errors;
        }

        private async Task TryDeleteBlobAsync(string blobUrl, CancellationToken cancellationToken)
        {
            try
            {
                await _storageCoordinator.DeleteFileAsync(blobUrl, cancellationToken);
            }
            catch
            {
                // Old template files are non-critical after the database points at the replacement.
            }
        }
    }

    public record DeleteDocumentTemplateCommand(int Id) : IRequest<Result>;

    public class DeleteDocumentTemplateHandler : IRequestHandler<DeleteDocumentTemplateCommand, Result>
    {
        private readonly IErpDbContext _context;
        private readonly IGlobalStorageCoordinator _storageCoordinator;

        public DeleteDocumentTemplateHandler(IErpDbContext context, IGlobalStorageCoordinator storageCoordinator)
        {
            _context = context;
            _storageCoordinator = storageCoordinator;
        }

        public async Task<Result> Handle(DeleteDocumentTemplateCommand request, CancellationToken cancellationToken)
        {
            var template = await _context.DocumentTemplates.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (template == null)
                return Result.Failure("Document template not found.");

            var blobUrl = template.BlobUrl;
            _context.DocumentTemplates.Remove(template);
            await _context.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(blobUrl))
            {
                try
                {
                    await _storageCoordinator.DeleteFileAsync(blobUrl, cancellationToken);
                }
                catch
                {
                    // Temporal history keeps the record; missing blob cleanup should not block the UI.
                }
            }

            return Result.Success("Document template deleted successfully.");
        }
    }

    public record SetDocumentTemplateDefaultCommand(int Id) : IRequest<Result>;

    public class SetDocumentTemplateDefaultHandler : IRequestHandler<SetDocumentTemplateDefaultCommand, Result>
    {
        private readonly IErpDbContext _context;

        public SetDocumentTemplateDefaultHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(SetDocumentTemplateDefaultCommand request, CancellationToken cancellationToken)
        {
            var template = await _context.DocumentTemplates.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (template == null)
                return Result.Failure("Document template not found.");
            if (!template.IsActive)
                return Result.Failure("Activate this document template before making it default.");

            var existingDefaults = await _context.DocumentTemplates
                .Where(x => x.Id != template.Id &&
                            x.DocumentType == template.DocumentType &&
                            x.TaxProfile == template.TaxProfile &&
                            x.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var existingDefault in existingDefaults)
                existingDefault.IsDefault = false;
            template.IsDefault = true;

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success("Default document template updated successfully.");
        }
    }

    public record SetDocumentTemplateActiveCommand(int Id, bool IsActive) : IRequest<Result>;

    public class SetDocumentTemplateActiveHandler : IRequestHandler<SetDocumentTemplateActiveCommand, Result>
    {
        private readonly IErpDbContext _context;

        public SetDocumentTemplateActiveHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(SetDocumentTemplateActiveCommand request, CancellationToken cancellationToken)
        {
            var template = await _context.DocumentTemplates.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (template == null)
                return Result.Failure("Document template not found.");

            template.IsActive = request.IsActive;
            if (!request.IsActive)
                template.IsDefault = false;

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(request.IsActive ? "Document template activated." : "Document template deactivated.");
        }
    }
}
