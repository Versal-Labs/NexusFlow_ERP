using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("Categories", Schema = "Master")]
    public class Category : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // e.g., "MENS-SHIRT"
    }
}
