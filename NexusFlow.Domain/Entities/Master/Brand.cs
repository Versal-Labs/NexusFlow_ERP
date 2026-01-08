using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("Brands", Schema = "Master")]
    public class Brand : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
