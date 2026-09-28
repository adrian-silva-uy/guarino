using GuarinoApp.Application.Modules.Proyeccion.Dtos;

namespace GuarinoApp.Application.Common
    {
    public class BusinessSuggestionRequest
        {
        public int TotalStock { get; set; }
        public Dictionary<string, int>? StockByVariety { get; set; }
        public int PalletsWithoutClient { get; set; }
        public decimal GrowthPercent { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;

        // Parámetros controlables por UI — sin valores hardcodeados aquí.
        // Se validan y normalizan en la capa de infraestructura antes de usarlos.
        [System.ComponentModel.DataAnnotations.Range(0, 100)]
        public int MinConfidence { get; set; }
        [System.ComponentModel.DataAnnotations.Range(1, 36)]
        public int HorizonMonths { get; set; }
        public bool IncludeSeasonality { get; set; }

        /// <summary>
        /// Proyección de la próxima zafra (Estado == "Proyectado"), calculada por
        /// GetProyeccionZafraQuery. Se serializa junto con el resto del request
        /// hacia el prompt de Ollama (ver OllamaAIService.GetBusinessSuggestionsAsync),
        /// y se usa también en el fallback de plantillas cuando Ollama no responde.
        /// </summary>
        public ZafraDto? ProyeccionZafra { get; set; }
        }
    }

