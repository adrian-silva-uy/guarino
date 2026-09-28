using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services;

public class ClimateDetectionBackgroundService : BackgroundService
{
    private readonly ILogger<ClimateDetectionBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ClimateDetectionBackgroundService(
        ILogger<ClimateDetectionBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClimateDetection iniciado");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IClimateService>();
                await service.DetectarYRegistrarEventosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en detección");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
