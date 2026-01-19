using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Config
{
    public class SystemConfigDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DataType { get; set; } = "String"; // String, Boolean, Decimal, Integer
        public string Description { get; set; } = string.Empty;
    }

    public class NumberSequenceDto
    {
        public int Id { get; set; }
        public string Module { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public int NextNumber { get; set; }
        public string Delimiter { get; set; } = "-";
        public string Suffix { get; set; } = string.Empty;

        // Computed property for UI convenience
        public string Preview => $"{Prefix}{Delimiter}{NextNumber}{Suffix}";
    }
}
