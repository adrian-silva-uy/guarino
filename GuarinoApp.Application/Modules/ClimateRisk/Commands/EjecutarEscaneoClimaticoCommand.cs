using GuarinoApp.Application.Common.Interfaces;
using MediatR;

namespace GuarinoApp.Application.Modules.ClimateRisk.Commands
    {
    /// <summary>
    /// Dispara el escaneo climático manual (consulta Open-Meteo por cada Lote con coordenadas
    /// y guarda los eventos detectados). El propio IClimateService ya evita duplicados y
    /// ejecuciones concurrentes con el ClimateMonitoringHostedService.
    /// </summary>
    public record EjecutarEscaneoClimaticoCommand : IRequest<Unit>;

    public class EjecutarEscaneoClimaticoCommandHandler : IRequestHandler<EjecutarEscaneoClimaticoCommand, Unit>
        {
        private readonly IClimateService _climateService;

        public EjecutarEscaneoClimaticoCommandHandler(IClimateService climateService)
            {
            _climateService = climateService;
            }

        public async Task<Unit> Handle(EjecutarEscaneoClimaticoCommand request, CancellationToken cancellationToken)
            {
            await _climateService.DetectarYRegistrarEventosAsync(cancellationToken);
            return Unit.Value;
            }
        }
    }
