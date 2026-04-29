using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public class ExportMetadata
    {
        public string CompanyName { get; set; } = "NexusFlow ERP";
        public string ReportTitle { get; set; } = string.Empty;
        public Dictionary<string, string> AppliedFilters { get; set; } = new();
    }

    public interface IExportService
    {
        byte[] ExportToExcel<T>(IEnumerable<T> data, ExportMetadata metadata, string sheetName = "Report");
        byte[] ExportToPdf<T>(IEnumerable<T> data, ExportMetadata metadata);
    }
}
