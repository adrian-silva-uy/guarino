namespace GuarinoApp.Application.Common.Interfaces
    {
    public interface IDashboardDataService
        {
        Task<Dictionary<string, int>> GetStockByVarietyAsync(DateTime? referenceDate = null);
        Task<List<MonthlyVolcado>> GetMonthlyVolcadosAsync(DateTime from, DateTime to);
        Task<List<MonthlyVolcado>> GetMonthlyVolcadosAsync(DateTime? referenceDate = null);
        Task<int> GetTotalStockAsync(DateTime? referenceDate = null);
        Task<int> GetPalletsWithoutClientAsync(DateTime? referenceDate = null);
        Task<decimal> GetProjectedGrowthAsync(DateTime from, DateTime to);
        Task<decimal> GetProjectedGrowthAsync(DateTime? referenceDate = null);
        Task<List<VolcadoDetalle>> GetVolcadosDetailAsync(DateTime from, DateTime to, int top = 10);
        Task<DateTime?> GetLastStockDateAsync();
        Task<DateTime?> GetLastStockDateAsync(DateTime referenceDate);
        }

    public class MonthlyVolcado
        {
        public string? Month { get; set; }
        public int Kgs { get; set; }
        }

    public class VolcadoDetalle
        {
        public string? Productor { get; set; }
        public string? Variedad { get; set; }
        public decimal KgsVolcados { get; set; }
        public int Bins { get; set; }
        public string? Zafra { get; set; }
        }
    }
