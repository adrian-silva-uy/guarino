using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ClimateRisk.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.ClimateRisk.Queries
    {
    /// <summary>
    /// Trae el resumen de riesgo climático, separando explícitamente:
    /// - Eventos RECIENTES (Fecha &lt;= hoy): ya ocurrieron, sirven para evaluar daño real
    ///   sobre la cosecha y ajustar la proyección exportable.
    /// - Eventos PRONOSTICADOS (Fecha &gt; hoy): todavía no pasaron, son la ventana para
    ///   actuar preventivamente (riego antiheladas, adelantar cosecha, etc.) antes de que
    ///   impacten la fruta destinada a EEUU, UE o Brasil.
    /// </summary>
    public record GetClimateRiskSummaryQuery(int DiasHistorial = 30, int MaxEventosPorGrupo = 30)
        : IRequest<ClimateRiskSummaryDto?>;

    public class GetClimateRiskSummaryQueryHandler
        : IRequestHandler<GetClimateRiskSummaryQuery, ClimateRiskSummaryDto?>
        {
        private readonly IApplicationDbContext _context;

        public GetClimateRiskSummaryQueryHandler(IApplicationDbContext context)
            {
            _context = context;
            }

        public async Task<ClimateRiskSummaryDto?> Handle(GetClimateRiskSummaryQuery request, CancellationToken cancellationToken)
            {
            var hoy = DateTime.Today;
            var fechaDesde = hoy.AddDays(-request.DiasHistorial);

            // Sin límite superior: el pronóstico de Open-Meteo (forecast_days=7) ya trae
            // fechas futuras, y queremos incluirlas todas para la sección de alertas.
            var eventosQuery = _context.EventosClimaticos
                .Include(e => e.Lote).ThenInclude(l => l.Productor)
                .Where(e => !e.IsDeleted && e.Fecha >= fechaDesde);

            var totalEventos = await eventosQuery.CountAsync(cancellationToken);
            if (totalEventos == 0)
                {
                return null;
                }

            var todosOrdenados = await eventosQuery
                .OrderByDescending(e => e.SeveridadEstimada == "Crítica" ? 2 : e.SeveridadEstimada == "Alta" ? 1 : 0)
                .ThenByDescending(e => e.Fecha)
                .Select(e => new EventoClimaticoDto
                    {
                    Id = e.Id,
                    Fecha = e.Fecha,
                    TipoEvento = e.TipoEvento,
                    SeveridadEstimada = e.SeveridadEstimada,
                    PorcentajePerdidaEstimada = e.PorcentajePerdidaEstimada,
                    KilosPerdidosEstimados = e.KilosPerdidosEstimados,
                    Fuente = e.Fuente,
                    Observaciones = e.Observaciones,
                    LoteNombre = e.Lote != null ? e.Lote.Codigo : "N/A",
                    ProductorNombre = e.Lote != null && e.Lote.Productor != null ? e.Lote.Productor.Nombre : "N/A",
                    EsPronostico = e.Fecha > hoy
                    })
                .ToListAsync(cancellationToken);

            var eventosRecientes = todosOrdenados.Where(e => !e.EsPronostico).Take(request.MaxEventosPorGrupo).ToList();
            var eventosPronosticados = todosOrdenados.Where(e => e.EsPronostico).Take(request.MaxEventosPorGrupo).ToList();

            var totalRecientes = todosOrdenados.Count(e => !e.EsPronostico);
            var totalPronosticados = todosOrdenados.Count(e => e.EsPronostico);

            var totalKilos = todosOrdenados.Sum(e => e.KilosPerdidosEstimados ?? 0);
            var kilosEnRiesgoPronosticado = eventosPronosticados.Sum(e => e.KilosPerdidosEstimados ?? 0);

            var lotesAfectados = await eventosQuery.Select(e => e.LoteId).Distinct().CountAsync(cancellationToken);
            var productoresAfectados = await eventosQuery.Select(e => e.ProductorId).Distinct().CountAsync(cancellationToken);

            var porTipo = todosOrdenados
                .GroupBy(e => e.TipoEvento)
                .ToDictionary(g => g.Key, g => g.Count());

            var porSeveridad = todosOrdenados
                .GroupBy(e => e.SeveridadEstimada)
                .ToDictionary(g => g.Key, g => g.Count());

            var tipoFrecuente = porTipo.OrderByDescending(t => t.Value).Select(t => t.Key).FirstOrDefault() ?? "N/D";
            var porcentaje = totalKilos > 0 ? Math.Round((totalKilos / 1_000_000m) * 100, 2) : 0;

            return new ClimateRiskSummaryDto
                {
                TotalEventos = totalEventos,
                KilosPerdidosTotales = totalKilos,
                PorcentajePerdidaTotal = porcentaje,
                TipoEventoMasFrecuente = tipoFrecuente,
                Observaciones = $"Impacto ya ocurrido: últimos {request.DiasHistorial} días · Riesgo pronosticado: según disponibilidad del pronóstico Open-Meteo (hasta 7 días hacia adelante).",
                Eventos = todosOrdenados,
                PlanMitigacion = [],
                LotesAfectados = lotesAfectados,
                ProductoresAfectados = productoresAfectados,
                EventosPorTipo = porTipo,
                EventosPorSeveridad = porSeveridad,
                EventosRecientes = eventosRecientes,
                EventosPronosticados = eventosPronosticados,
                TotalEventosRecientes = totalRecientes,
                TotalEventosPronosticados = totalPronosticados,
                KilosEnRiesgoPronosticado = kilosEnRiesgoPronosticado
                };
            }
        }
    }
