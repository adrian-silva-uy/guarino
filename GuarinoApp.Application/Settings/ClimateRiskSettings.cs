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

/// <summary>
/// Umbrales meteorológicos para clasificación de eventos climáticos
/// </summary>
//public class ThresholdsConfig
//{
//    public decimal HeladaTempMin { get; set; } = 0;
//    public decimal HeladaTempSevera { get; set; } = -3;
//    public decimal LluviaExcesivaMm { get; set; } = 50;
//    public decimal LluviaMuyExcesivaMm { get; set; } = 100;
//    public decimal VientoFuerteKmh { get; set; } = 50;
//    public decimal VientoFuerteKmhCritico { get; set; } = 80;
//    public decimal CalorExtremoTempMax { get; set; } = 38;
//}

/// <summary>
/// Estimaciones de pérdida de cosecha por tipo de evento (valores en %)
/// </summary>
//public class ImpactEstimatesConfig
//{
//    public decimal HeladaPerdida { get; set; } = 15;
//    public decimal HeladaSeveraPerdida { get; set; } = 45;
//    public decimal LluviaExcesivaPerdida { get; set; } = 20;
//    public decimal LluviaMuyExcesivaPerdida { get; set; } = 50;
//    public decimal VientoFuertePerdida { get; set; } = 10;
//    public decimal CalorExtremoPerdida { get; set; } = 25;
//}
