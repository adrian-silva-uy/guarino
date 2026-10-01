using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;

namespace GuarinoApp.Application.Common.Interfaces;

public interface IAIService
    {
    Task<string> ChatAsync(string prompt, List<ChatMessage> history);

    bool IsAvailable { get; }

    /// <summary>
    /// Devuelve sugerencias de negocio estructuradas basadas en los datos suministrados.
    /// </summary>
    Task<List<BusinessSuggestionDto>> GetBusinessSuggestionsAsync(BusinessSuggestionRequest request,
                                                                                             List<ChatMessage>? history = null);

    /// <summary>
    /// Devuelve el análisis estratégico completo para la planificación de la próxima zafra:
    /// KPIs financieros, acciones estratégicas y escenarios de crecimiento.
    /// <paramref name="datosFinancieros"/> son las cifras vigentes de Contaduría
    /// (costo/kg, flujo de caja, capacidad); si existen, los KPIs se calculan con ellas.
    /// </summary>
    Task<ZafraStrategyResultDto> GetZafraInteligentStrategyAsync(
        ZafraDto projectedZafra,
        decimal capacidadMaxHistorica,
        int stockActualBins,
        List<BusinessSuggestionDto>? history = null,
        FinancialDataDto? datosFinancieros = null);
    }

public class ChatMessage
    {
    public string? Text { get; set; }
    public bool IsUser { get; set; }
    public System.DateTime Timestamp { get; set; }
    }
