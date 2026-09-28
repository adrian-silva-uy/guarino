namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos
    {
    public class EventoClimaticoDto
        {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string TipoEvento { get; set; } = string.Empty;
        public string SeveridadEstimada { get; set; } = string.Empty;
        public decimal? PorcentajePerdidaEstimada { get; set; }
        public decimal? KilosPerdidosEstimados { get; set; }
        public string Fuente { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
        public string LoteNombre { get; set; } = string.Empty;
        public string ProductorNombre { get; set; } = string.Empty;

        // --- distingue si el evento ya ocurrió (evaluación de daño) o es pronóstico
        // (ventana para actuar preventivamente). Aditivo, no rompe otros consumidores. ---
        public bool EsPronostico { get; set; }
        }
    }
