using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Health check para verificar la disponibilidad de Ollama.
/// Se integra con el endpoint /health de ASP.NET Core.
/// </summary>
public class OllamaHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly OllamaConnectionState _connectionState;

    public OllamaHealthCheck(
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        OllamaConnectionState connectionState)
    {
        _optionsMonitor = optionsMonitor;
        _connectionState = connectionState;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Degraded("Ollama está deshabilitado en configuración."));
        }

        if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrWhiteSpace(options.Model))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Ollama:Url o Ollama:Model no están configurados."));
        }

        if (_connectionState.IsReady)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"Ollama disponible en {options.Url} con modelo {options.Model}."));
        }

        if (_connectionState.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Degraded("Ollama conectado pero el modelo no está descargado todavía."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("Ollama no está disponible."));
    }
}
