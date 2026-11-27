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
            return Task.FromResult(HealthCheckResult.Degraded("Servicio opcional no habilitado."));

        if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrWhiteSpace(options.Model))
            return Task.FromResult(HealthCheckResult.Unhealthy("Servicio de IA no configurado."));

        if (_connectionState.IsReady)
            return Task.FromResult(HealthCheckResult.Healthy("Servicio de IA disponible."));

        if (_connectionState.IsConnected)
            return Task.FromResult(HealthCheckResult.Degraded("Servicio de IA conectado; inicialización pendiente."));

        return Task.FromResult(HealthCheckResult.Unhealthy("Servicio de IA no disponible."));
    }
}
