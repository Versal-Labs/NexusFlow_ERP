using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("UnitOfMeasures", Schema = "Master")]
    public class UnitOfMeasure : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Piece"
        public string Symbol { get; set; } = string.Empty; // e.g., "pcs"
    }
}
