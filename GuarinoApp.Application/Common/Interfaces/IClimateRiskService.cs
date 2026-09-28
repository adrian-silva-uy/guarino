namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Servicio que genera análisis de riesgo climático usando IA
/// </summary>
public interface IClimateRiskService
    {
    /// <summary>
    /// Obtiene estrategia de mitigación de riesgos climáticos desde la IA
    /// </summary>
    Task<string> GetClimateRiskStrategyAsync(string inputJson, CancellationToken cancellationToken);
    }
