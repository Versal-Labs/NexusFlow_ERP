namespace NexusFlow.Web.Models.Dashbaord
{
    public class DashboardViewModel
    {
        // KPI Metrics
        public decimal TotalInventoryValue { get; set; }
        public int LowStockItems { get; set; }
        public int PendingOrders { get; set; }
        public decimal MonthlyRevenue { get; set; }

        // Chart Data (Simplified for UI)
        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> SalesData { get; set; } = new();
        public List<decimal> PurchaseData { get; set; } = new();
    }
}
