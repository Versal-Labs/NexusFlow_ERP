using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("Provinces", Schema = "Master")]
    public class Province : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public ICollection<District> Districts { get; set; } = new List<District>();
    }

    [Table("Districts", Schema = "Master")]
    public class District : BaseEntity
    {
        public int ProvinceId { get; set; }
        public Province Province { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public ICollection<City> Cities { get; set; } = new List<City>();
    }

    [Table("Cities", Schema = "Master")]
    public class City : BaseEntity
    {
        public int DistrictId { get; set; }
        public District District { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
    }
}
