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
        }
    }
