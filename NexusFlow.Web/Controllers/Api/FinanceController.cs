using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Finance.Banks.Commands;
using NexusFlow.AppCore.Features.Finance.Banks.Queries;
using NexusFlow.AppCore.Features.Finance.Commands;
using NexusFlow.AppCore.Features.Finance.Journals.Queries;
using NexusFlow.AppCore.Features.Finance.Queries;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class FinanceController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IErpDbContext _context; // For direct DB access in certain endpoints

        public FinanceController(IMediator mediator, IErpDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts()
        {
            var result = await _mediator.Send(new GetAccountsQuery());
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet("chart-of-accounts")]
        public async Task<IActionResult> GetChartOfAccounts()
        {
            var result = await _mediator.Send(new GetChartOfAccountsQuery());
            if (result.Succeeded) return Ok(result.Data); // Unwrapped for UI Tree
            return BadRequest(result.Message); // Fixed from result.Errors to standard Messages
        }

        [HttpPost("account")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet("trial-balance")]
        public async Task<IActionResult> GetTrialBalance([FromQuery] DateTime? date)
        {
            var query = new GetTrialBalanceQuery { AsOfDate = date ?? DateTime.UtcNow };
            var result = await _mediator.Send(query);

            // ARCHITECT FIX: Unwrapping the array so JS .forEach() works perfectly
            if (result.Succeeded) return Ok(result.Data);
            return BadRequest(result.Message);
        }

        [HttpGet("balance-sheet")]
        public async Task<IActionResult> GetBalanceSheet([FromQuery] DateTime? date)
        {
            var query = new GetBalanceSheetQuery { AsOfDate = date ?? DateTime.UtcNow };
            var result = await _mediator.Send(query);

            // ARCHITECT FIX: Pre-emptively unwrapping this for when we build the Balance Sheet UI
            if (result.Succeeded) return Ok(result.Data);
            return BadRequest(result.Message);
        }

        [HttpGet("periods")]
        public async Task<IActionResult> GetFinancialPeriods()
        {
            var result = await _mediator.Send(new GetFinancialPeriodsQuery());
            return Ok(result);
        }

        [HttpPost("periods")]
        public async Task<IActionResult> CreateFinancialPeriod([FromBody] CreateFinancialPeriodCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet("journals")]
        public async Task<IActionResult> GetJournals([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? module)
        {
            var query = new GetJournalEntriesQuery { StartDate = startDate, EndDate = endDate, Module = module };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("journals/{id}")]
        public async Task<IActionResult> GetJournalById(int id)
        {
            var result = await _mediator.Send(new GetJournalEntryByIdQuery { Id = id });
            if (result.Succeeded) return Ok(result.Data); // Unwrap for JS
            return NotFound(result.Message);
        }

        // In Controllers/Api/FinanceController.cs
        [HttpGet("banks")]
        public async Task<IActionResult> GetBanks()
        {
            var banks = await _context.Banks
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.BankCode })
                .ToListAsync();
            return Ok(banks);
        }

        [HttpGet("banks/{bankId}/branches")]
        public async Task<IActionResult> GetBranches(int bankId)
        {
            var branches = await _context.BankBranches
                .Where(b => b.BankId == bankId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new { b.Id, b.BranchName, b.BranchCode })
                .ToListAsync();
            return Ok(branches);
        }

        [HttpPost("seed-banks")]
        [AllowAnonymous] // Temporarily allow anonymous just to hit it from Postman/Browser without token issues, remove after!
        public async Task<IActionResult> SeedSriLankaBanks([FromServices] IWebHostEnvironment env)
        {
            // Assuming you placed the json file inside the 'wwwroot/data' folder of your web project
            string filePath = Path.Combine(env.WebRootPath, "data", "sri_lanka_banks.json");

            var command = new SeedBanksCommand { JsonFilePath = filePath };
            var result = await _mediator.Send(command);

            if (result.Succeeded)
                return Ok(result);

            return BadRequest(result);
        }

        // ==========================================
        // BANK RECONCILIATION ENDPOINTS
        // ==========================================

        [HttpGet("banking/beginning-balance")]
        public async Task<IActionResult> GetBeginningBalance([FromQuery] int bankAccountId)
        {
            var result = await _mediator.Send(new GetBeginningBalanceQuery(bankAccountId));
            return Ok(result); // Returns the Result<decimal> wrapper
        }

        [HttpGet("banking/uncleared")]
        public async Task<IActionResult> GetUnclearedTransactions([FromQuery] int bankAccountId, [FromQuery] DateTime statementDate)
        {
            var result = await _mediator.Send(new GetUnclearedTransactionsQuery(bankAccountId, statementDate));
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("banking/adjustment")]
        public async Task<IActionResult> PostBankAdjustment([FromBody] PostBankAdjustmentCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("banking/finalize")]
        public async Task<IActionResult> FinalizeReconciliation([FromBody] FinalizeReconciliationCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }
    }
}
