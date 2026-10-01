using GuarinoApp.Application.Common.Dtos;

namespace GuarinoApp.Application.Modules.Proyeccion.Dtos
    {
    /// <summary>
    /// Contenedor único que agrupa toda la respuesta de IA para el tablero de Proyección de Zafra:
    /// KPIs financieros calculados, acciones estratégicas sugeridas y escenarios de crecimiento.
    /// Devuelto por IAIService.GetZafraInteligentStrategyAsync.
    /// </summary>
    public class ZafraStrategyResultDto
        {
        public FinancialKpiDto? KPIs { get; set; }
        public List<BusinessSuggestionDto> AccionesEstrategicas { get; set; } = new();
        public List<ScenarioDto> Escenarios { get; set; } = new();

        /// <summary>
        /// Veredicto de 3-4 oraciones para el comité de dirección (lo primero que lee Gerencia).
        /// El servicio de IA puede devolver una versión más rica; si viene vacío, la página usa
        /// la determinista de FinancialKpiCalculator.
        /// </summary>
        public string ResumenEjecutivo { get; set; } = string.Empty;

        /// <summary>
        /// true cuando la respuesta proviene del motor de IA (y no del cálculo determinista
        /// de respaldo). La página lo usa para mostrar el distintivo "análisis de IA incorporado".
        /// </summary>
        public bool EsIA { get; set; }
        }
    }
