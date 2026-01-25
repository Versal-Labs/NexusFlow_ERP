using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class BrandDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } // Derived logic if needed, usually just existence
    }
}
