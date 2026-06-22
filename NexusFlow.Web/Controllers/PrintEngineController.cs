using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.AppCore.Features.Print;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Domain.Enums;
using NexusFlow.Web.Filters;
using System.Security.Cryptography;
using System.Text.Json;

namespace NexusFlow.Web.Controllers
{
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [HybridAuthorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PrintEngineController : ControllerBase
    {
        private readonly IDocumentRenderingService _renderingService;
        private readonly IMediator _mediator;
        private readonly IErpDbContext _context;
        private readonly IGlobalStorageCoordinator _storage;
        private readonly ICurrentUserService _currentUser;

        public PrintEngineController(
            IDocumentRenderingService renderingService,
            IMediator mediator,
            IErpDbContext context,
            IGlobalStorageCoordinator storage,
            ICurrentUserService currentUser)
        {
            _renderingService = renderingService;
            _mediator = mediator;
            _context = context;
            _storage = storage;
            _currentUser = currentUser;
        }

        [HttpGet("Initialize/{documentType}/{documentId}")]
        public async Task<IActionResult> Initialize(string documentType, string documentId)
        {
            var load = await LoadAuthorizedDocumentAsync(documentType, documentId);
            return load.Error ?? Ok(load.Document);
        }

        [HttpPost("Render")]
        public async Task<IActionResult> Render([FromBody] PrintRenderRequestDto request)
        {
            var load = await LoadAuthorizedDocumentAsync(request.DocumentType, request.DocumentId);
            if (load.Error != null) return load.Error;

            // Accounting values are always reloaded; only presentation text may be overridden.
            ApplyWhitelistedOverrides(load.Document!, request.Overrides, out _);
            var pdf = await _renderingService.RenderDocumentToPdfAsync(load.Type, load.Document!);
            return File(pdf, "application/pdf");
        }

        [HttpPost("Finalize")]
        public async Task<IActionResult> Finalize([FromBody] PrintRenderRequestDto request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.OutputAction, "Print", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.OutputAction, "Download", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Output action must be Print or Download.");

            var load = await LoadAuthorizedDocumentAsync(request.DocumentType, request.DocumentId);
            if (load.Error != null) return load.Error;

            ApplyWhitelistedOverrides(load.Document!, request.Overrides, out var differences);
            var pdf = await _renderingService.RenderDocumentToPdfAsync(load.Type, load.Document!, cancellationToken);
            var fileName = $"{load.Document!.DocumentType}-{SafeFilePart(load.Document.DocumentNumber)}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            string? blobUrl = null;

            try
            {
                await using var stream = new MemoryStream(pdf, writable: false);
                blobUrl = await _storage.SaveFileSecurelyAsync(stream, fileName, "GeneratedDocuments", "application/pdf", cancellationToken);

                var audit = new GeneratedDocument
                {
                    DocumentType = load.Document.DocumentType,
                    DocumentId = load.Document.DocumentId,
                    DocumentNumber = load.Document.DocumentNumber,
                    OutputAction = request.OutputAction,
                    BlobUrl = blobUrl,
                    Sha256Hash = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(),
                    OverrideDifferencesJson = JsonSerializer.Serialize(differences),
                    GeneratedAtUtc = DateTime.UtcNow,
                    GeneratedByUserId = _currentUser.UserId ?? "SYSTEM"
                };
                _context.GeneratedDocuments.Add(audit);
                await _context.SaveChangesAsync(cancellationToken);

                Response.Headers.Append("X-Generated-Document-Id", audit.Id.ToString());
                return File(pdf, "application/pdf", fileName);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(blobUrl))
                {
                    try { await _storage.DeleteFileAsync(blobUrl, cancellationToken); } catch { }
                }
                throw;
            }
        }

        [HttpGet("History/{documentType}/{documentId}")]
        public async Task<IActionResult> History(string documentType, string documentId, CancellationToken cancellationToken)
        {
            var load = await LoadAuthorizedDocumentAsync(documentType, documentId);
            if (load.Error != null) return load.Error;

            var rows = await _context.GeneratedDocuments
                .AsNoTracking()
                .Where(x => x.DocumentType == load.Document!.DocumentType && x.DocumentId == load.Document.DocumentId)
                .OrderByDescending(x => x.GeneratedAtUtc)
                .Take(25)
                .Select(x => new GeneratedDocumentHistoryDto
                {
                    Id = x.Id,
                    OutputAction = x.OutputAction,
                    Sha256Hash = x.Sha256Hash,
                    GeneratedAtUtc = x.GeneratedAtUtc,
                    GeneratedByUserId = x.GeneratedByUserId,
                    HasOverrides = x.OverrideDifferencesJson != "{}"
                })
                .ToListAsync(cancellationToken);
            return Ok(rows);
        }

        [HttpGet("Generated/{id:int}")]
        public async Task<IActionResult> Generated(int id, CancellationToken cancellationToken)
        {
            var record = await _context.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (record == null) return NotFound("Generated document not found.");

            var load = await LoadAuthorizedDocumentAsync(record.DocumentType, record.DocumentId);
            if (load.Error != null) return load.Error;

            var (stream, contentType) = await _storage.RetrieveFileAsync(record.BlobUrl, cancellationToken);
            return File(stream, contentType, $"{record.DocumentType}-{SafeFilePart(record.DocumentNumber)}.pdf");
        }

        [HttpGet("File/{documentType}/{documentId}")]
        public async Task<IActionResult> DownloadFile(string documentType, string documentId, CancellationToken cancellationToken)
        {
            return await Finalize(new PrintRenderRequestDto
            {
                DocumentType = documentType,
                DocumentId = documentId,
                OutputAction = "Download"
            }, cancellationToken);
        }

        private async Task<(DocumentType Type, PrintDocumentDto? Document, IActionResult? Error)> LoadAuthorizedDocumentAsync(string documentType, string documentId)
        {
            if (!Enum.TryParse<DocumentType>(documentType, true, out var type))
                return (default, null, BadRequest("Invalid document type."));
            if (!CanView(type))
                return (type, null, Forbid());

            var result = await _mediator.Send(new GetPrintDocumentQuery(type, documentId));
            if (!result.Succeeded)
                return (type, null, BadRequest(string.Join(", ", result.Errors ?? new[] { result.Message })));
            return (type, result.Data, null);
        }

        private bool CanView(DocumentType type)
        {
            if (_currentUser.HasPermission(Permissions.SuperAdmin)) return true;
            var permission = type switch
            {
                DocumentType.SalesOrder or DocumentType.SalesQuotation => Permissions.Sales.ViewOrders,
                DocumentType.SalesInvoice => Permissions.Sales.ViewInvoices,
                DocumentType.CreditNote => Permissions.Sales.ViewCreditNotes,
                DocumentType.PurchaseOrder => Permissions.Purchasing.ViewPOs,
                DocumentType.GRN => Permissions.Purchasing.ViewGRNs,
                DocumentType.SupplierBill => Permissions.Purchasing.ViewBills,
                DocumentType.DebitNote => Permissions.Purchasing.ViewDebitNotes,
                DocumentType.CustomerReceipt => Permissions.Treasury.ViewReceipts,
                DocumentType.SupplierPaymentRemittance => Permissions.Treasury.ViewPayments,
                DocumentType.StockTransferDeliveryNote => Permissions.Inventory.ViewStock,
                DocumentType.Payslip => Permissions.HR.ViewEmployees,
                DocumentType.ProductionOrder or DocumentType.MaterialRequirementIssueSheet or DocumentType.MaterialReturn or DocumentType.ProductionReceipt or DocumentType.ProductionClosureReconciliation => Permissions.Inventory.RunProduction,
                _ => string.Empty
            };
            return !string.IsNullOrWhiteSpace(permission) && _currentUser.HasPermission(permission);
        }

        private static void ApplyWhitelistedOverrides(PrintDocumentDto document, PrintOverridesDto overrides, out Dictionary<string, object> differences)
        {
            var collectedDifferences = new Dictionary<string, object>();
            Apply(nameof(document.CustomerOrSupplierName), document.CustomerOrSupplierName, overrides.CustomerOrSupplierName, value => document.CustomerOrSupplierName = value);
            Apply(nameof(document.BillingAddress), document.BillingAddress, overrides.BillingAddress, value => document.BillingAddress = value);
            Apply(nameof(document.ShippingAddress), document.ShippingAddress, overrides.ShippingAddress, value => document.ShippingAddress = value);
            Apply(nameof(document.Notes), document.Notes, overrides.Notes, value => document.Notes = value);
            differences = collectedDifferences;

            void Apply(string field, string original, string? replacement, Action<string> setter)
            {
                if (replacement == null || replacement == original) return;
                collectedDifferences[field] = new { Original = original, Override = replacement };
                setter(replacement);
            }
        }

        private static string SafeFilePart(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    }
}
