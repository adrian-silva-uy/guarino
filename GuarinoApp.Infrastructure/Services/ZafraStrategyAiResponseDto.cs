namespace GuarinoApp.Infrastructure.Services;

/// <summary>Respuesta cruda de la IA para la estrategia de la zafra (antes anidada en OllamaAIService).</summary>
internal class ZafraStrategyAiResponseDto
    {
    public ZafraStrategyAiKpisDto? Kpis { get; set; }
    public List<ZafraStrategyAiAccionDto>? AccionesEstrategicas { get; set; }
    public string? ResumenEjecutivo { get; set; }
    }
