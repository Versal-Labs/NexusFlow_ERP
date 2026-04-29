using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.MasterData.Products.Queries;
using NexusFlow.AppCore.Features.Sales.Customers.Commands;
using NexusFlow.AppCore.Features.Sales.Customers.Queries;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [HybridAuthorize]
    public class CustomerController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IErpDbContext _context;

        public CustomerController(IErpDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _mediator.Send(new GetCustomersQuery());

            // Standard Result<T> wrapper response
            return Ok(result);
        }

        // [ POST ] /api/customer - Creates a new customer
        // ==========================================
        // LOCATION MASTER ENDPOINT
        // ==========================================
        [HttpGet("/api/locations/provinces")]
        public async Task<IActionResult> GetProvinces()
        {
            // By using .Select(), we break the circular reference and only send 
            // the exact hierarchy the dropdowns need, reducing payload size by 90%!
            var provinces = await _context.Provinces
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    Districts = p.Districts.Select(d => new
                    {
                        d.Id,
                        d.Name,
                        Cities = d.Cities.Select(c => new
                        {
                            c.Id,
                            c.Name
                        }).ToList()
                    }).ToList()
                })
                .ToListAsync();

            return Ok(provinces);
        }

        // ==========================================
        // BANK MASTER ENDPOINT
        // ==========================================
        [HttpGet("/api/banks")]
        public async Task<IActionResult> GetBanks()
        {
            var banks = await _context.Banks
                .AsNoTracking()
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.BankCode,
                    b.SwiftCode,
                    Branches = b.Branches.Select(br => new
                    {
                        br.Id,
                        br.BranchCode,
                        br.BranchName
                    }).ToList()
                })
                .ToListAsync();

            return Ok(banks);
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveCustomer([FromBody] SaveCustomerCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
