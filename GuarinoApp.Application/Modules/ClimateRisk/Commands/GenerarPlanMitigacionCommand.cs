using GuarinoApp.Application.Common.Behaviors;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ClimateRisk.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GuarinoApp.Application.Modules.ClimateRisk.Commands
    {
    /// <summary>
    /// Solicita el plan de mitigación de riesgo climático a la IA y lo deja listo para la UI.
    /// Nunca devuelve una lista vacía: si la IA no está disponible o responde algo no interpretable,
    /// devuelve una recomendación general clara y explícitamente marcada como "sin IA".
    /// </summary>
    /// Exento de la puerta de serialización: espera hasta30s de respuesta de IA
    /// (no debe frenar las cargas del circuito) y su consulta anidada MediatR se
    /// encola sola. Sus datos los resuelve cada servicio con scope propio.
    public record GenerarPlanMitigacionCommand(List<EventoClimaticoDto> HistorialEventos)
        : IRequest<List<MitigacionPlanDto>>, IRequestExentoPuertaDb;

    public class GenerarPlanMitigacionCommandHandler
        : IRequestHandler<GenerarPlanMitigacionCommand, List<MitigacionPlanDto>>
        {
        private readonly IClimateRiskService _climateRiskService;
        private readonly IMediator _mediator;
        private readonly ILogger<GenerarPlanMitigacionCommandHandler> _logger;

        public GenerarPlanMitigacionCommandHandler(
            IClimateRiskService climateRiskService,
            IMediator mediator,
            ILogger<GenerarPlanMitigacionCommandHandler> logger)
            {
            _climateRiskService = climateRiskService;
            _mediator = mediator;
            _logger = logger;
            }

        public async Task<List<MitigacionPlanDto>> Handle(GenerarPlanMitigacionCommand request, CancellationToken cancellationToken)
            {
            if (request.HistorialEventos.Count == 0)
                {
                return new List<MitigacionPlanDto>();
                }

            var proyeccion = await ObtenerProyeccionActualAsync(cancellationToken);

            // El historial que llega de la vista puede traer hasta 500 eventos por
            // grupo (tablas paginadas de la UI), pero el PROMPT de IA se acota a
            // 30 ya ocurridos + 30 de hoy/pronóstico (orden natural de la lista:
            // severidad primero) — exactamente lo que la IA recibía antes del cambio:
            // evita disparar el consumo de tokens y el timeout de 30 s con cientos
            // de eventos casi idénticos (mismo lote, días seguidos).
            var historialEventos = request.HistorialEventos
                .Where(e => !e.EsPronostico).Take(30)
                .Concat(request.HistorialEventos.Where(e => e.EsPronostico).Take(30))
                .ToList();

            var input = JsonSerializer.Serialize(new
                {
                HistorialEventos = historialEventos,
                ProyeccionActual = proyeccion
                });

            string respuesta;
            try
                {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                respuesta = await _climateRiskService.GetClimateRiskStrategyAsync(input, cts.Token);
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Error solicitando plan de mitigación a IClimateRiskService");
                return PlanDeRespaldo();
                }

            try
                {
                var result = JsonSerializer.Deserialize<ClimateAiResponse>(respuesta,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.PlanMitigacion?.Count > 0)
                    {
                    return result.PlanMitigacion.Select(p => new MitigacionPlanDto
                        {
                        Texto = p.Texto,
                        Categoria = p.Categoria,
                        Origen = p.Origen,
                        Impacto = p.Impacto,
                        Riesgo = p.Riesgo
                        }).ToList();
                    }

                _logger.LogWarning("El asistente de IA devolvió un plan de mitigación vacío.");
                return PlanDeRespaldo();
                }
            catch (JsonException ex)
                {
                _logger.LogWarning(ex, "La respuesta del asistente de IA no es JSON válido. Se usa el plan de respaldo.");
                return PlanDeRespaldo();
                }
            }

        // TotalKgsProyectados pide la proyección real
        // vía GetProyeccionZafraQuery (composición MediatR); si no está disponible, se lo decimos
        // explícitamente a la IA en vez de simular un número (anti-fabricación).
        private async Task<object> ObtenerProyeccionActualAsync(CancellationToken cancellationToken)
            {
            try
                {
                var zafras = await _mediator.Send(new GetProyeccionZafraQuery(), cancellationToken);
                var proyectada = zafras.FirstOrDefault(z => z.Estado == "Proyectado");

                if (proyectada == null)
                    {
                    return new { Disponible = false, Motivo = "Sin proyección de zafra calculada" };
                    }

                return new
                    {
                    Disponible = true,
                    TotalKgsProyectados = proyectada.TotalKgs,
                    Año = proyectada.Año,
                    EsSimulada = proyectada.EsDummy
                    };
                }
            catch (Exception ex)
                {
                _logger.LogWarning(ex, "No se pudo obtener la proyección de zafra real para el plan de mitigación.");
                return new { Disponible = false, Motivo = "Error consultando la proyección de zafra" };
                }
            }

        private static List<MitigacionPlanDto> PlanDeRespaldo() =>
            [
            new MitigacionPlanDto
                {
                Texto = "El asistente de IA no está disponible en este momento. Priorizar revisión manual de los lotes con eventos de severidad Crítica o Alta, especialmente aquellos en fase de floración, cuaje o precosecha, por su impacto directo sobre el calibre y la calidad exigidos para exportación a EEUU, Unión Europea y Brasil.",
                Categoria = "Aviso del Sistema",
                Origen = "Sistema (sin IA)",
                Impacto = "N/D",
                Riesgo = "N/D"
                },
            new MitigacionPlanDto
                {
                Texto = "Coordinar con el equipo agronómico una inspección de campo en los lotes afectados dentro de las próximas 48 horas, para confirmar el daño real y ajustar la proyección de cosecha exportable antes del próximo embarque.",
                Categoria = "Aviso del Sistema",
                Origen = "Sistema (sin IA)",
                Impacto = "N/D",
                Riesgo = "N/D"
                }
            ];
        }
    }
