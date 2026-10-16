using GuarinoApp.Application.Common.Behaviors;
using GuarinoApp.Application.Common.Interfaces;
using MediatR;

namespace GuarinoApp.Application.Modules.ClimateRisk.Commands
    {
    /// <summary>
    /// Dispara el escaneo climático manual (consulta Open-Meteo por cada Lote con coordenadas
    /// y guarda los eventos detectados). El propio IClimateService ya evita duplicados y
    /// ejecuciones concurrentes con el ClimateMonitoringHostedService.
    /// Exento de la puerta de serialización: es HTTP largo (reintentos + breaker)
    /// y su persistencia usa scope propio, nunca el DbContext del circuito.
    /// </summary>
    public record EjecutarEscaneoClimaticoCommand : IRequest<Unit>, IRequestExentoPuertaDb;

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
