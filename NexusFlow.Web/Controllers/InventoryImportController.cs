using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NexusFlow.Web.Controllers
{
    public class InventoryImportController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file)
        {
            // MOCK: Simulate file upload
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // Return detected headers and sample rows used for Mapping Step
            var mockHeaders = new List<string> { "Style Name", "Color Variant", "Size", "Barcode", "Qty", "Cost", "Price" };
            
            return Json(new { success = true, headers = mockHeaders, fileName = file.FileName, jobId = Guid.NewGuid() });
        }

        [HttpPost]
        public IActionResult SaveMapping([FromBody] MappingDto mapping)
        {
            // MOCK: Simulate saving mapping and starting validation
            return Json(new { success = true, jobId = mapping.JobId });
        }

        [HttpGet]
        public IActionResult GetStagingData(Guid jobId)
        {
            // MOCK: Generate 50 items. 10 with errors.
            var items = new List<StagingItemViewModel>();
            var random = new Random();

            for (int i = 1; i <= 50; i++)
            {
                bool isError = i % 5 == 0; // Every 5th item has an error
                
                items.Add(new StagingItemViewModel
                {
                    Id = i,
                    ProductName = isError ? "" : $"Oxford Shirt - Batch {i}",
                    StyleCode = $"OX-{1000 + i}",
                    Color = random.Next(0, 2) == 0 ? "Blue" : "White",
                    Size = random.Next(0, 2) == 0 ? "L" : "M",
                    SKU = isError ? "" : $"OX-{1000 + i}-L",
                    Quantity = isError ? "-5" : random.Next(10, 100).ToString(),
                    CostPrice = "15.00",
                    SellingPrice = "35.00",
                    IsValid = !isError,
                    ErrorMessage = isError ? (string.IsNullOrEmpty(isError ? "" : "SKU") ? "Missing Product Name" : "Invalid Quantity") : null
                });
            }

            return Json(new { data = items });
        }
        
        [HttpPost]
        public IActionResult Commit(Guid jobId)
        {
            // MOCK: Start async commit
            return Json(new { success = true });
        }
    }
}
