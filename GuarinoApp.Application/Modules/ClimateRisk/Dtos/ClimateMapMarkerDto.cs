namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos
    {
    public class ClimateMapMarkerDto
        {
        /// <summary>Código del lote: permite el enlace bidireccional mapa ↔ tablas
        /// (clic en el popup → filas del lote; clic en la fila → este marcador).</summary>
        public string Codigo { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Info { get; set; }
        public string? Color { get; set; }
        }
    }
