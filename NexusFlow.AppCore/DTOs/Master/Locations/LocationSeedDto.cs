using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace NexusFlow.AppCore.DTOs.Master.Locations
{
    public class LocationSeedRoot
    {
        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("provinces")]
        public List<ProvinceSeedDto> Provinces { get; set; } = new();
    }

    public class ProvinceSeedDto
    {
        [JsonPropertyName("province")]
        public string Province { get; set; } = string.Empty;

        [JsonPropertyName("districts")]
        public List<DistrictSeedDto> Districts { get; set; } = new();
    }

    public class DistrictSeedDto
    {
        [JsonPropertyName("district")]
        public string District { get; set; } = string.Empty;

        [JsonPropertyName("cities")]
        public List<string> Cities { get; set; } = new();
    }
}
