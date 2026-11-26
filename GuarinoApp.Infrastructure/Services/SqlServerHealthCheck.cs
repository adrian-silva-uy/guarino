using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using GuarinoApp.Infrastructure.Data;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Health check para verificar la conectividad con SQL Server.
/// Se integra con el endpoint /health de ASP.NET Core.
/// </summary>
public class SqlServerHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SqlServerHealthCheck> _logger;

    public SqlServerHealthCheck(
        ApplicationDbContext context,
        ILogger<SqlServerHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Base de datos disponible.")
                : HealthCheckResult.Unhealthy("Base de datos no disponible.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en health check de SQL Server");
            return HealthCheckResult.Unhealthy("No se pudo verificar la base de datos.");
        }
    }
}
