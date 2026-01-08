using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("Warehouses", Schema = "Master")]
    public class Warehouse : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Main Store", "ABC Garments (Factory)"
        public string Location { get; set; } = string.Empty;

        // If TRUE, this is a 3rd party factory. 
        // Inventory here is typically considered "WIP" or "Raw Material at Vendor".
        public bool IsSubcontractor { get; set; }
    }
}
