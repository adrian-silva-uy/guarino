namespace GuarinoApp.Application.Common.Dtos
    {
    /// <summary>
    /// Sugerencia de negocio devuelta por el servicio de IA.
    /// Categoria/Origen/Impacto/Riesgo son opcionales -- GetBusinessSuggestionsAsync (sugerencias
    /// generales) no los usa y quedan en null; GetZafraInteligentStrategyAsync sí los completa.
    /// </summary>
    public class BusinessSuggestionDto
        {
        public string? Text { get; set; }
        public string? Color { get; set; }   // Hex o nombre CSS
        public string? Icon { get; set; }    // Nombre icono Material/Bootstrap (opcional)

        /// <summary>Financiero | Calidad | Logistica | ManoDeObra | Fitosanitario | CostoComparado</summary>
        public string? Categoria { get; set; }

        /// <summary>"Calculado" (respaldado por datos reales) o "Checklist" (recomendación operativa estándar, sin dato numérico real detrás).</summary>
        public string? Origen { get; set; }

        public string? Impacto { get; set; }
        public string? Riesgo { get; set; }
        }
    }
