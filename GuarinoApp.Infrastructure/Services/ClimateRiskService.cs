using System.Text.Json;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services;

public class ClimateRiskService : IClimateRiskService
    {
    private readonly IAIService _aiService;
    private readonly ILogger<ClimateRiskService> _logger;

    public ClimateRiskService(
        IAIService aiService,
        ILogger<ClimateRiskService> logger)
        {
        _aiService = aiService;
        _logger = logger;
        }

    public async Task<string> GetClimateRiskStrategyAsync(string inputJson, CancellationToken cancellationToken)
        {
        try
            {
            // El prompt ClimateRiskStrategy y la carrera local ↔ web los arma la
            // ruta dedicada de IA (OllamaAIService): acá solo se exige que lo
            // devuelto sea JSON utilizable. Si no lo es (motor caído, sin cupo o
            // respuesta en prosa), se devuelve el plan de respaldo del módulo —
            // plan vacío que el comando convierte en los avisos marcados "sin IA".
            var result = await _aiService.GetClimateRiskStrategyAsync(inputJson, cancellationToken);

            if (string.IsNullOrWhiteSpace(result) || !EsJsonValido(result))
                {
                _logger.LogWarning(
                    "El asistente de IA no devolvió un plan de mitigación en JSON. Se usa la respuesta de respaldo del módulo climático.");
                return GetFallbackResponse();
                }

            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error en GetClimateRiskStrategyAsync");
            return GetFallbackResponse();
            }
        }

    private static bool EsJsonValido(string texto)
        {
        try
            {
            using var _ = JsonDocument.Parse(texto);
            return true;
            }
        catch (JsonException)
            {
            return false;
            }
        }

    // Plan vacío: el comando GenerarPlanMitigacionCommand lo detecta y muestra su
    // recomendación genérica explícitamente marcada "Sistema (sin IA)".
    private static string GetFallbackResponse() => """
        {"resumenImpacto":{"kilosPerdidosTotales":0,"porcentajePerdidaTotal":"N/D","tipoEventoMasFrecuente":"N/D","observaciones":"Sin IA"},"planMitigacion":[]}
        """;
    }
