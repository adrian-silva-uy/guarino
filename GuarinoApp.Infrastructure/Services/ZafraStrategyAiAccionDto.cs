namespace GuarinoApp.Infrastructure.Services;

/// <summary>Acción estratégica devuelta por la IA para la zafra.</summary>
internal class ZafraStrategyAiAccionDto
    {
    public string? Texto { get; set; }
    public string? Categoria { get; set; }
    public string? Origen { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string? Impacto { get; set; }
    public string? Riesgo { get; set; }
    }
