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

        /// <summary>Eventos del lote en la ventana con Fecha &lt; hoy: aparecen en la
        /// tabla «Impacto Ya Ocurrido». Se expone al JS del mapa para que «Ver en la
        /// tabla» filtre la tabla correcta (y para que los marcadores verdes, sin
        /// eventos, no ofrezcan un botón que no tendría filas).</summary>
        public int Ocurridos { get; set; }

        /// <summary>Eventos del lote con Fecha &gt;= hoy (día en curso + pronóstico
        /// 7 días): aparecen en la tabla «De Hoy y Riesgo Pronosticado».</summary>
        public int HoyPronosticados { get; set; }
        }
    }
