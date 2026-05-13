using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Payroll.Commands;
using NexusFlow.AppCore.Features.Payroll.Queries;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Jobs.Interfaces;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class PayrollController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IExportService _exportService;
        private readonly IGenerateDraftPayrollJob _payrollGenerator;
        public PayrollController(IMediator mediator, IExportService exportService, IGenerateDraftPayrollJob payrollGenerator)
        {
            _mediator = mediator;
            _exportService = exportService;
            _payrollGenerator = payrollGenerator;
        }

        [HttpPost("{payrollPeriodId}/post")]
        public async Task<IActionResult> PostPayroll(int payrollPeriodId)
        {
            var result = await _mediator.Send(new PostPayrollCommand { PayrollPeriodId = payrollPeriodId });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{payrollPeriodId}/export-bank-file")]
        // [Authorize(Policy = Permissions.HR.ManagePayroll)]
        public async Task<IActionResult> ExportBankFile(int payrollPeriodId)
        {
            var result = await _mediator.Send(new GetBankTransferFileQuery { PayrollPeriodId = payrollPeriodId });
            if (!result.Succeeded) return BadRequest(result.Message);

            // Return as a standard CSV
            return File(result.Data, "text/csv", $"BankTransfer_{DateTime.Now:yyyyMMdd}.csv");
        }

        [HttpGet("{payrollPeriodId}/export-epf-return")]
        // [Authorize(Policy = Permissions.HR.ManagePayroll)]
        public async Task<IActionResult> ExportEpfReturn(int payrollPeriodId)
        {
            var result = await _mediator.Send(new GetEpfReturnFileQuery { PayrollPeriodId = payrollPeriodId });
            if (!result.Succeeded) return BadRequest(result.Message);

            return File(result.Data, "text/csv", $"EPF_C_Form_{DateTime.Now:yyyyMM}.csv");
        }

        [HttpGet("slip/{slipId}/pdf")]
        public async Task<IActionResult> DownloadPayslipPdf(int slipId)
        {
            var result = await _mediator.Send(new GetEmployeePayslipQuery { PayrollSlipId = slipId });
            if (!result.Succeeded) return BadRequest(result.Message);

            // This calls the beautiful new layout!
            var pdfBytes = _exportService.GeneratePayslipPdf(result.Data);

            return File(pdfBytes, "application/pdf", $"Payslip_{result.Data.EmployeeCode}_{result.Data.MonthYear}.pdf");
        }

        [HttpGet("period")]
        // [Authorize(Policy = Permissions.HR.ViewPayroll)]
        public async Task<IActionResult> GetPeriod([FromQuery] string monthYear)
        {
            if (string.IsNullOrWhiteSpace(monthYear))
                return BadRequest("MonthYear is required.");

            var result = await _mediator.Send(new GetPayrollPeriodQuery { MonthYear = monthYear });

            // We return OK whether it found data or not, the Result.Data handles the payload
            return Ok(result);
        }

        [HttpPost("generate")]
        // [Authorize(Policy = Permissions.HR.ManagePayroll)]
        public async Task<IActionResult> GenerateDraft([FromQuery] int year, [FromQuery] int month)
        {
            // You can inject the Job directly or use MediatR.
            // Assuming you injected: private readonly GenerateDraftPayrollJob _payrollGenerator;
            try
            {
                // Note: For best practice, this should be dispatched via MediatR to keep controllers thin, 
                // but calling the job directly here works perfectly for triggering Hangfire manually.
                await _payrollGenerator.ExecuteAsync(year, month);
                return Ok(new { message = "Draft Payroll Generated Successfully!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
