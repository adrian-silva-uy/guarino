namespace GuarinoApp.Application.Settings;

/// <summary>
/// Configuración de umbrales y estimaciones de impacto para detección de eventos climáticos
/// </summary>
public class ClimateRiskSettings
{
    public const string SectionName = "ClimateRisk";

    /// <summary>
    /// Contexto regional descriptivo (ej: "Salto, Paysandú, Uruguay")
    /// </summary>
    public string RegionalContext { get; set; } = "Región Subtropical Húmeda";

    /// <summary>
    /// URL base de la API de Open-Meteo
    /// </summary>
    public string OpenMeteoApiUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";

    /// <summary>
    /// Timeout para las llamadas a la API en segundos
    /// </summary>
    public int ApiTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Máximo número de llamadas paralelas a la API
    /// </summary>
    public int MaxParallelRequests { get; set; } = 5;

    /// <summary>
    /// Umbrales de temperatura, precipitación y viento para detección
    /// </summary>
    public ThresholdsConfig Thresholds { get; set; } = new();

    /// <summary>
    /// Estimaciones de pérdida de cosecha por tipo de evento (en %)
    /// </summary>
    public ImpactEstimatesConfig ImpactEstimates { get; set; } = new();
}
