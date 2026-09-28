namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos
    {
    /// <summary>
    /// Resultado de cruzar la proyección de la próxima zafra (GetProyeccionZafraQuery) con el
    /// riesgo climático detectado (GetClimateRiskSummaryQuery). No es una cifra "hardcodeada": es una
    /// estimación proporcional para dar visibilidad temprana, siempre mostrada junto a su origen.
    /// </summary>
    public class ImpactoClimaticoProyeccionDto
        {
        public int AñoProyectado { get; set; }
        public decimal TotalKgsProyectados { get; set; }
        public decimal KilosEnRiesgo { get; set; }
        public decimal PorcentajeEnRiesgo { get; set; }
        public decimal ProyeccionAjustadaKgs { get; set; }
        public int LotesAfectados { get; set; }
        public int EventosPronosticados { get; set; }
        public bool ProyeccionEsDummy { get; set; }
        public string Comentario { get; set; } = string.Empty;
        }
    }
