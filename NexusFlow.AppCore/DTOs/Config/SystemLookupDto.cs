using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Config
{
    public class SystemLookupDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
