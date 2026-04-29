using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Dashboard.Queries;
using NexusFlow.AppCore.Features.Inventory.Queries;
using NexusFlow.Web.Models;
using NexusFlow.Web.Models.Dashbaord;
using System.Diagnostics;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class HomeController : Controller
    {
        private readonly IMediator _mediator;

        public HomeController(IMediator mediator)
        {
            _mediator = mediator;
        }
        public async Task<IActionResult> Index()
        {
            // Execute the high-performance dashboard aggregation query
            var result = await _mediator.Send(new GetDashboardDataQuery());

            // If it succeeds, pass the DTO directly to the View. If not, pass an empty one.
            var model = result.Succeeded ? result.Data : new DashboardMetricsDto();

            return View(model);
        }

        public IActionResult Privacy()
        {
            var vm = new UserName
            {
                Name = "Sakeel",
                Age = new UserAge 
                { 
                    Age = 30,
                    Gender = "Male"
                },
                Address = "Adresss"
            };
            return View(vm);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
