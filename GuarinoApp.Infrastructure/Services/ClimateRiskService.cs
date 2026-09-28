using System.Text.Json;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Infrastructure.Services;

public class ClimateRiskService : IClimateRiskService
    {
    private readonly IAIService _aiService;
    private readonly IPromptProvider _promptProvider;
    private readonly ILogger<ClimateRiskService> _logger;
    private readonly IOptionsMonitor<ClimateRiskSettings> _climateSettings;

    public ClimateRiskService(
        IAIService aiService,
        IPromptProvider promptProvider,
        ILogger<ClimateRiskService> logger,
        IOptionsMonitor<ClimateRiskSettings> climateSettings)
        {
        _aiService = aiService;
        _promptProvider = promptProvider;
        _logger = logger;
        _climateSettings = climateSettings;
        }

    public async Task<string> GetClimateRiskStrategyAsync(string inputJson, CancellationToken cancellationToken)
        {
        try
            {
            var template = await _promptProvider.GetPromptAsync("ClimateRiskStrategy");
            if (string.IsNullOrWhiteSpace(template))
                return GetFallbackResponse();

            var settings = _climateSettings.CurrentValue;
            var prompt = ReplacePromptPlaceholders(template, settings, inputJson);
            var result = await _aiService.ChatAsync(prompt, new List<ChatMessage>());

            // FallbackAIService puede devolver texto plano (no JSON) cuando Ollama falla
            // internamente y usa su propio fallback. No basta con chequear IsNullOrWhiteSpace:
            // hay que confirmar que sea JSON válido antes de confiar en la respuesta.
            if (string.IsNullOrWhiteSpace(result) || !EsJsonValido(result))
                {
                _logger.LogWarning(
                    "La respuesta de IAIService.ChatAsync no es JSON válido (probablemente Ollama no está disponible). Se usa la respuesta de respaldo del módulo climático.");
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

    private string ReplacePromptPlaceholders(string template, ClimateRiskSettings settings, string inputJson)
        {
        var t = settings.Thresholds;
        var i = settings.ImpactEstimates;
        return template
            .Replace("{REGIONAL_CONTEXT}", settings.RegionalContext)
            .Replace("{HELA_SEVERA_TEMP}", t.HeladaTempSevera.ToString("F1"))
            .Replace("{HELA_TEMP}", t.HeladaTempMin.ToString("F1"))
            .Replace("{LLUVIA_EXCESIVA_MM}", t.LluviaExcesivaMm.ToString("F1"))
            .Replace("{LLUVIA_MUY_EXCESIVA_MM}", t.LluviaMuyExcesivaMm.ToString("F1"))
            .Replace("{VIENTO_FUERTE_KMH}", t.VientoFuerteKmh.ToString("F1"))
            .Replace("{VIENTO_FUERTE_KMH_CRITICO}", t.VientoFuerteKmhCritico.ToString("F1"))
            .Replace("{CALOR_EXTREMO_TEMP}", t.CalorExtremoTempMax.ToString("F1"))
            .Replace("{HELA_PERDIDA}", i.HeladaPerdida.ToString("F1"))
            .Replace("{HELA_SEVERA_PERDIDA}", i.HeladaSeveraPerdida.ToString("F1"))
            .Replace("{LLUVIA_EXCESIVA_PERDIDA}", i.LluviaExcesivaPerdida.ToString("F1"))
            .Replace("{LLUVIA_MUY_EXCESIVA_PERDIDA}", i.LluviaMuyExcesivaPerdida.ToString("F1"))
            .Replace("{VIENTO_FUERTE_PERDIDA}", i.VientoFuertePerdida.ToString("F1"))
            .Replace("{CALOR_EXTREMO_PERDIDA}", i.CalorExtremoPerdida.ToString("F1"))
            .Replace("{INPUT_JSON}", inputJson);
        }

    private static string GetFallbackResponse() => """
        {"resumenImpacto":{"kilosPerdidosTotales":0,"porcentajePerdidaTotal":"N/D","tipoEventoMasFrecuente":"N/D","observaciones":"Sin IA"},"planMitigacion":[]}
        """;
    }
