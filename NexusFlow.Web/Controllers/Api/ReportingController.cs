using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Configs.Queries;
using NexusFlow.AppCore.Features.Reporting.Commands;
using NexusFlow.AppCore.Features.Reporting.Queries;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class ReportingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IExportService _exportService;

        public ReportingController(IMediator mediator, IExportService exportService)
        {
            _mediator = mediator;
            _exportService = exportService;
        }

        // 1. Fetch JSON for the DataTable UI
        [HttpGet("sales-register")]
        public async Task<IActionResult> GetSalesRegister([FromQuery] GetSalesRegisterQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        private async Task<ExportMetadata> BuildMetadataAsync(string reportTitle, GetSalesRegisterQuery query)
        {
            string companyName = await _mediator.Send(new GetCompanyNameQuery());

            var metadata = new ExportMetadata
            {
                CompanyName = companyName,
                ReportTitle = reportTitle,
                AppliedFilters = new Dictionary<string, string>()
            };

            if (query.StartDate.HasValue) metadata.AppliedFilters.Add("From Date", query.StartDate.Value.ToString("yyyy-MM-dd"));
            if (query.EndDate.HasValue) metadata.AppliedFilters.Add("To Date", query.EndDate.Value.ToString("yyyy-MM-dd"));
            if (query.CustomerId.HasValue) metadata.AppliedFilters.Add("Customer ID Filter", query.CustomerId.Value.ToString());
            if (query.SalesRepId.HasValue) metadata.AppliedFilters.Add("Sales Rep ID Filter", query.SalesRepId.Value.ToString());

            return metadata;
        }

        [HttpGet("sales-register/export/excel")]
        public async Task<IActionResult> ExportSalesRegisterExcel([FromQuery] GetSalesRegisterQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = await BuildMetadataAsync("Master Sales Register", query);

            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "Sales Register");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Sales_Register_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("sales-register/export/pdf")]
        public async Task<IActionResult> ExportSalesRegisterPdf([FromQuery] GetSalesRegisterQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = await BuildMetadataAsync("Master Sales Register", query);

            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"Sales_Register_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // --- AR AGING ---

        [HttpGet("ar-aging")]
        public async Task<IActionResult> GetArAging([FromQuery] GetArAgingQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpPost("ar-aging/remind")]
        public async Task<IActionResult> SendReminder([FromBody] SendArReminderCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result.Message);
        }

        [HttpGet("ar-aging/export/excel")]
        public async Task<IActionResult> ExportArAgingExcel([FromQuery] GetArAgingQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "Accounts Receivable Aging Summary", CompanyName = "NexusFlow Enterprise" }; // You can wire up the CompanyName query here too
            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "AR Aging");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AR_Aging_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("ar-aging/export/pdf")]
        public async Task<IActionResult> ExportArAgingPdf([FromQuery] GetArAgingQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "Accounts Receivable Aging Summary", CompanyName = "NexusFlow Enterprise" };
            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"AR_Aging_{DateTime.Now:yyyyMMdd}.pdf");
        }

        //Customer
        [HttpGet("customer-statement")]
        public async Task<IActionResult> GetCustomerStatement([FromQuery] GetCustomerStatementQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpGet("customer-statement/export/excel")]
        public async Task<IActionResult> ExportStatementExcel([FromQuery] GetCustomerStatementQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "Customer Statement of Account", CompanyName = "NexusFlow Enterprise" };
            metadata.AppliedFilters.Add("Date Range", $"{query.StartDate:yyyy-MM-dd} to {query.EndDate:yyyy-MM-dd}");

            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "Statement");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Statement_{query.CustomerId}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("customer-statement/export/pdf")]
        public async Task<IActionResult> ExportStatementPdf([FromQuery] GetCustomerStatementQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "Customer Statement of Account", CompanyName = "NexusFlow Enterprise" };
            metadata.AppliedFilters.Add("Date Range", $"{query.StartDate:yyyy-MM-dd} to {query.EndDate:yyyy-MM-dd}");

            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"Statement_{query.CustomerId}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ==========================================
        // ACCOUNTS PAYABLE & SUPPLIER STATEMENTS
        // ==========================================

        [HttpGet("ap-aging")]
        public async Task<IActionResult> GetApAging([FromQuery] GetApAgingQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpGet("ap-aging/export/excel")]
        public async Task<IActionResult> ExportApAgingExcel([FromQuery] GetApAgingQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = await BuildMetadataAsync("Accounts Payable Aging Summary", new GetSalesRegisterQuery()); // Reusing metadata helper
            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "AP Aging");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AP_Aging_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("ap-aging/export/pdf")]
        public async Task<IActionResult> ExportApAgingPdf([FromQuery] GetApAgingQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = await BuildMetadataAsync("Accounts Payable Aging Summary", new GetSalesRegisterQuery());
            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"AP_Aging_{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet("supplier-statement")]
        public async Task<IActionResult> GetSupplierStatement([FromQuery] GetSupplierStatementQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpGet("supplier-statement/export/excel")]
        public async Task<IActionResult> ExportSupplierStatementExcel([FromQuery] GetSupplierStatementQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            // Uses the BuildMetadataAsync helper we created in Phase 1
            var metadata = new ExportMetadata { ReportTitle = "Supplier Statement of Account", CompanyName = "NexusFlow Enterprise" };
            metadata.AppliedFilters.Add("Date Range", $"{query.StartDate:yyyy-MM-dd} to {query.EndDate:yyyy-MM-dd}");

            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "AP Statement");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Supplier_Statement_{query.SupplierId}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("supplier-statement/export/pdf")]
        public async Task<IActionResult> ExportSupplierStatementPdf([FromQuery] GetSupplierStatementQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "Supplier Statement of Account", CompanyName = "NexusFlow Enterprise" };
            metadata.AppliedFilters.Add("Date Range", $"{query.StartDate:yyyy-MM-dd} to {query.EndDate:yyyy-MM-dd}");

            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"Supplier_Statement_{query.SupplierId}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ==========================================
        // INVENTORY ANALYTICS CUBE
        // ==========================================

        [HttpGet("inventory-analytics")]
        public async Task<IActionResult> GetInventoryAnalytics([FromQuery] GetInventoryAnalyticsQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpGet("inventory-analytics/export/excel")]
        public async Task<IActionResult> ExportInventoryAnalyticsExcel([FromQuery] GetInventoryAnalyticsQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "Inventory Analytics Cube", CompanyName = "NexusFlow Enterprise" };
            if (query.StartDate.HasValue) metadata.AppliedFilters.Add("From Date", query.StartDate.Value.ToString("yyyy-MM-dd"));
            if (query.EndDate.HasValue) metadata.AppliedFilters.Add("To Date", query.EndDate.Value.ToString("yyyy-MM-dd"));
            if (query.TransactionType.HasValue) metadata.AppliedFilters.Add("Transaction Type", query.TransactionType.Value.ToString());

            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "Inventory Analytics");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Inventory_Analytics_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("inventory-analytics/export/pdf")]
        public async Task<IActionResult> ExportInventoryAnalyticsPdf([FromQuery] GetInventoryAnalyticsQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "Inventory Analytics Cube", CompanyName = "NexusFlow Enterprise" };
            if (query.StartDate.HasValue) metadata.AppliedFilters.Add("From Date", query.StartDate.Value.ToString("yyyy-MM-dd"));
            if (query.EndDate.HasValue) metadata.AppliedFilters.Add("To Date", query.EndDate.Value.ToString("yyyy-MM-dd"));
            if (query.TransactionType.HasValue) metadata.AppliedFilters.Add("Transaction Type", query.TransactionType.Value.ToString());

            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"Inventory_Analytics_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ==========================================
        // TREASURY & CHEQUE VAULT ANALYTICS
        // ==========================================

        [HttpGet("cheque-vault")]
        public async Task<IActionResult> GetChequeVaultAnalytics([FromQuery] GetChequeVaultAnalyticsQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpGet("cheque-vault/export/excel")]
        public async Task<IActionResult> ExportChequeVaultExcel([FromQuery] GetChequeVaultAnalyticsQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = await BuildMetadataAsync("Treasury: Cheque Vault Analytics", new GetSalesRegisterQuery()); // We reuse the helper
            if (query.Status.HasValue) metadata.AppliedFilters.Add("Status", query.Status.Value.ToString());

            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "Cheque Vault");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ChequeVault_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("cheque-vault/export/pdf")]
        public async Task<IActionResult> ExportChequeVaultPdf([FromQuery] GetChequeVaultAnalyticsQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = await BuildMetadataAsync("Treasury: Cheque Vault Analytics", new GetSalesRegisterQuery());
            if (query.Status.HasValue) metadata.AppliedFilters.Add("Status", query.Status.Value.ToString());

            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"ChequeVault_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ==========================================
        // GENERAL LEDGER & EXPENSE AUDIT
        // ==========================================

        [HttpGet("general-ledger")]
        public async Task<IActionResult> GetGeneralLedger([FromQuery] GetGeneralLedgerQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpGet("general-ledger/export/excel")]
        public async Task<IActionResult> ExportGeneralLedgerExcel([FromQuery] GetGeneralLedgerQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "General Ledger & Expense Report", CompanyName = "NexusFlow Enterprise" };
            metadata.AppliedFilters.Add("Date Range", $"{query.StartDate:yyyy-MM-dd} to {query.EndDate:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(query.Module)) metadata.AppliedFilters.Add("Module Filter", query.Module);

            var fileBytes = _exportService.ExportToExcel(result.Data, metadata, "General Ledger");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"GeneralLedger_{query.AccountId}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("general-ledger/export/pdf")]
        public async Task<IActionResult> ExportGeneralLedgerPdf([FromQuery] GetGeneralLedgerQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || !result.Data.Any()) return BadRequest("No data to export.");

            var metadata = new ExportMetadata { ReportTitle = "General Ledger & Expense Report", CompanyName = "NexusFlow Enterprise" };
            metadata.AppliedFilters.Add("Date Range", $"{query.StartDate:yyyy-MM-dd} to {query.EndDate:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(query.Module)) metadata.AppliedFilters.Add("Module Filter", query.Module);

            var fileBytes = _exportService.ExportToPdf(result.Data, metadata);
            return File(fileBytes, "application/pdf", $"GeneralLedger_{query.AccountId}_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}
