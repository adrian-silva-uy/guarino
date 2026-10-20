using GuarinoApp.Application.Modules.ClimateRisk.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;
using MediatR;

namespace GuarinoApp.Application.Modules.ClimateRisk.Queries
    {
    /// <summary>
    /// Compone dos Queries ya existentes (patrón CQRS estándar: un Handler puede pedirle datos
    /// a otro vía IMediator) para cruzar la proyección de la próxima zafra con el riesgo
    /// climático detectado. No agrega ninguna fuente de datos nueva.
    /// </summary>
    public record GetImpactoClimaticoEnProyeccionQuery : IRequest<ImpactoClimaticoProyeccionDto?>;

    public class GetImpactoClimaticoEnProyeccionQueryHandler
        : IRequestHandler<GetImpactoClimaticoEnProyeccionQuery, ImpactoClimaticoProyeccionDto?>
        {
        private readonly IMediator _mediator;

        public GetImpactoClimaticoEnProyeccionQueryHandler(IMediator mediator)
            {
            _mediator = mediator;
            }

        public async Task<ImpactoClimaticoProyeccionDto?> Handle(GetImpactoClimaticoEnProyeccionQuery request, CancellationToken cancellationToken)
            {
            var zafras = await _mediator.Send(new GetProyeccionZafraQuery(), cancellationToken);
            var proyectada = zafras.FirstOrDefault(z => z.Estado == "Proyectado");

            if (proyectada == null || proyectada.TotalKgs <= 0)
                {
                return null;
                }

            var resumenClimatico = await _mediator.Send(new GetClimateRiskSummaryQuery(), cancellationToken);
            if (resumenClimatico == null)
                {
                // Sin eventos climáticos detectados: no hay ajuste que mostrar, la proyección queda intacta.
                return null;
                }

            var kilosEnRiesgo = resumenClimatico.KilosPerdidosTotales;
            var porcentaje = Math.Round((kilosEnRiesgo / proyectada.TotalKgs) * 100, 1);
            var ajustada = Math.Max(0, proyectada.TotalKgs - kilosEnRiesgo);

            return new ImpactoClimaticoProyeccionDto
                {
                AñoProyectado = proyectada.Año,
                TotalKgsProyectados = proyectada.TotalKgs,
                KilosEnRiesgo = kilosEnRiesgo,
                PorcentajeEnRiesgo = porcentaje,
                ProyeccionAjustadaKgs = ajustada,
                LotesAfectados = resumenClimatico.LotesAfectados,
                EventosPronosticados = resumenClimatico.TotalEventosPronosticados,
                ProyeccionEsDummy = proyectada.EsDummy,
                Comentario = resumenClimatico.TotalEventosPronosticados > 0
                    ? $"{resumenClimatico.TotalEventosPronosticados} evento(s) de hoy y pronosticado(s) todavía se pueden mitigar con acción preventiva antes de afectar la cosecha exportable."
                    : "El riesgo mostrado corresponde a eventos que ya ocurrieron (impacto histórico reciente)."
                };
            }
        }
    }
