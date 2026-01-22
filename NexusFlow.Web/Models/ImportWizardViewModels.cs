using System;
using System.Collections.Generic;

namespace NexusFlow.Web.Models
{
    public class ImportJobViewModel
    {
        public Guid JobId { get; set; }
        public string Status { get; set; } // Pending, Validating, Ready, Completed
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int ErrorRows { get; set; }
    }

    public class StagingItemViewModel
    {
        public int Id { get; set; } // Mock ID
        public string ProductName { get; set; }
        public string StyleCode { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public string SKU { get; set; }
        public string Quantity { get; set; }
        public string CostPrice { get; set; }
        public string SellingPrice { get; set; }
        
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } // "SKU Missing", "Negative Price"
    }

    public class MappingDto
    {
        public Guid JobId { get; set; }
        public Dictionary<string, string> ColumnMapping { get; set; }
    }
}
