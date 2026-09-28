namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos
    {
    public class ClimateRiskSummaryDto
        {
        public int TotalEventos { get; set; }
        public decimal KilosPerdidosTotales { get; set; }
        public decimal PorcentajePerdidaTotal { get; set; }
        public string TipoEventoMasFrecuente { get; set; } = "N/D";
        public string Observaciones { get; set; } = string.Empty;
        public List<EventoClimaticoDto> Eventos { get; set; } = [];
        public List<MitigacionPlanDto> PlanMitigacion { get; set; } = [];

        // --- Agregado en el rediseño de gestion-riesgos-climaticos.razor ---
        public int LotesAfectados { get; set; }
        public int ProductoresAfectados { get; set; }
        public Dictionary<string, int> EventosPorTipo { get; set; } = [];
        public Dictionary<string, int> EventosPorSeveridad { get; set; } = [];

        // --- Agregado: separación pasado (impacto ya ocurrido) vs pronóstico (riesgo próximo) ---
        // Pre-separadas por el Handler para que la vista no tenga que filtrar nada.
        public List<EventoClimaticoDto> EventosRecientes { get; set; } = [];
        public List<EventoClimaticoDto> EventosPronosticados { get; set; } = [];
        public int TotalEventosRecientes { get; set; }
        public int TotalEventosPronosticados { get; set; }
        public decimal KilosEnRiesgoPronosticado { get; set; }
        }
    }
