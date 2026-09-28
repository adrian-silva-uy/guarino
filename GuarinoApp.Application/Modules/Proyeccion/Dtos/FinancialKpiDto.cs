namespace GuarinoApp.Application.Modules.Proyeccion.Dtos
    {
    public class FinancialKpiDto
        {
        public decimal CapitalNecesario { get; set; }
        public decimal BrechaLiquidez { get; set; }
        public string RiesgoLiquidez { get; set; } = "";
        public string OportunidadCAPEX { get; set; } = "";
        public string ComparativaBenchmark { get; set; } = "";
        }
    }
