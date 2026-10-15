namespace GuarinoApp.Infrastructure.Services;

/// <summary>KPIs financieros devueltos por la IA para la estrategia de la zafra.</summary>
internal class ZafraStrategyAiKpisDto
    {
    public decimal? CapitalNecesario { get; set; }
    public decimal? BrechaLiquidez { get; set; }
    public string? RiesgoLiquidez { get; set; }
    public string? OportunidadCapex { get; set; }
    public string? ComparativaBenchmark { get; set; }
    }
