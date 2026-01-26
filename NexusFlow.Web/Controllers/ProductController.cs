using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.Web.Models.Products;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class ProductController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetAllProducts()
        {
            // Mock Data - In reality, fetch from SQL/Entity Framework
            var products = new List<ProductViewModel>
        {
            new ProductViewModel { Id=1, Code="P-101", Name="Stainless Bolt", Category="Hardware", Price=10.50m, IsActive=true },
            new ProductViewModel { Id=2, Code="P-102", Name="Power Drill", Category="Tools", Price=150.00m, IsActive=true },
            new ProductViewModel { Id=3, Code="P-103", Name="Safety Goggles", Category="PPE", Price=25.00m, IsActive=false },
        };

            return Json(new { data = products });
        }

        // API Endpoint to Save
        [HttpPost]
        public IActionResult Save([FromBody] ProductViewModel model)
        {
            if (ModelState.IsValid)
            {
                // TODO: Save to Database logic here
                return Json(new { success = true, message = "Product saved successfully!" });
            }
            return Json(new { success = false, message = "Validation failed" });
        }
    }
}
