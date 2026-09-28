using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services
    {
    /// <summary>
    /// Corre periódicamente OpenMeteoClimateService.DetectarYRegistrarEventosAsync,
    /// que es el método que efectivamente puebla EventosClimaticos.
    /// IClimateService e IApplicationDbContext son scoped, por eso se crea un
    /// IServiceScope propio en cada corrida (este servicio vive como singleton).
    /// </summary>
    public class ClimateMonitoringHostedService : BackgroundService
        {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ClimateMonitoringHostedService> _logger;

        // Frecuencia del escaneo automático. Ajustable si hace falta más/menos seguido.
        private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

        // Pequeña espera al arrancar la app para no competir con el resto del startup
        // (ej. OllamaStartupService, migraciones, etc.)
        private static readonly TimeSpan DelayInicial = TimeSpan.FromSeconds(30);

        public ClimateMonitoringHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<ClimateMonitoringHostedService> logger)
            {
            _scopeFactory = scopeFactory;
            _logger = logger;
            }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
            try
                {
                await Task.Delay(DelayInicial, stoppingToken);
                }
            catch (OperationCanceledException)
                {
                return;
                }

            while (!stoppingToken.IsCancellationRequested)
                {
                await EjecutarEscaneoAsync(stoppingToken);

                try
                    {
                    await Task.Delay(Intervalo, stoppingToken);
                    }
                catch (OperationCanceledException)
                    {
                    break;
                    }
                }
            }

        private async Task EjecutarEscaneoAsync(CancellationToken cancellationToken)
            {
            using var scope = _scopeFactory.CreateScope();
            var climateService = scope.ServiceProvider.GetRequiredService<IClimateService>();

            try
                {
                _logger.LogInformation("ClimateMonitoringHostedService: iniciando escaneo programado.");
                await climateService.DetectarYRegistrarEventosAsync(cancellationToken);
                _logger.LogInformation("ClimateMonitoringHostedService: escaneo programado finalizado.");
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "ClimateMonitoringHostedService: error durante el escaneo programado.");
                }
            }
        }
    }
