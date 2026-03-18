using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class LegacyProductCsvRecord
    {
        [Name("type")] public string? Type { get; set; } // Parent Category
        [Name("type1")] public string? Type1 { get; set; } // Sub Category
        [Name("itemname")] public string? ItemName { get; set; }
        [Name("itemcode")] public string? ItemCode { get; set; } // SKU
        [Name("brand")] public string? Brand { get; set; }
        [Name("color")] public string? Color { get; set; }
        [Name("size")] public string? Size { get; set; }
        [Name("msize")] public string? MSize { get; set; }

        [Name("avar")] public decimal AverageCost { get; set; }
        [Name("selprice")] public decimal SellingPrice { get; set; }
        [Name("minsellpri")] public decimal MinSellingPrice { get; set; }
    }
}
