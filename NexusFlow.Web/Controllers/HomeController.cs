using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Features.Inventory.Queries;
using NexusFlow.Web.Models;
using NexusFlow.Web.Models.Dashbaord;
using System.Diagnostics;

namespace NexusFlow.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IMediator _mediator;

        public HomeController(IMediator mediator)
        {
            _mediator = mediator;
        }
        public async Task<IActionResult> Index()
        {
            // 1. Get Real Inventory Data
            var stockResult = await _mediator.Send(new GetStockLevelsQuery());
            var stockItems = stockResult.Succeeded ? stockResult.Data : new();

            // 2. Calculate KPIs
            var viewModel = new DashboardViewModel
            {
                // Real Data
                TotalInventoryValue = stockItems.Sum(x => x.TotalValue),
                LowStockItems = stockItems.Count(x => x.QuantityOnHand < 100), // Simple rule for now

                // Mock Data (We will connect these later)
                PendingOrders = 5,
                MonthlyRevenue = 45000.00m,

                // Mock Chart Data
                ChartLabels = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun" },
                SalesData = new List<decimal> { 12000, 19000, 15000, 22000, 24000, 28000 },
                PurchaseData = new List<decimal> { 10000, 15000, 12000, 18000, 20000, 22000 }
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
