using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ClimateRisk.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.ClimateRisk.Queries
    {
    /// <summary>
    /// Trae el resumen de riesgo climático, separando explícitamente:
    /// - Eventos OCURRIDOS (Fecha &lt; hoy): días anteriores confirmados; sirven
    ///   para evaluar daño real sobre la cosecha y ajustar la proyección.
    /// - Eventos de HOY + PRONOSTICADOS (Fecha &gt;= hoy): el día de hoy todavía
    ///   no terminó (el dato puede seguir cambiando con el próximo escáner) y el
    ///   resto es pronóstico Open-Meteo: es la ventana para actuar preventivamente
    ///   (riego antiheladas, adelantar cosecha, etc.) antes de que impacte la fruta
    ///   destinada a EEUU, UE o Brasil.
    /// MaxEventosPorGrupo limita las filas de CADA tabla de la vista (paginadas de
    /// a 10 en la UI). Si es muy chico, un lote con eventos queda fuera de la tabla
    /// y el enlace «Ver en la tabla» del mapa no encuentra sus filas: por eso es
    /// amplio (500 por tabla). Los KPIs y totales NUNCA se limitan: son COUNT/SUM
    /// sobre toda la ventana.
    /// </summary>
    public record GetClimateRiskSummaryQuery(int DiasHistorial = 30, int MaxEventosPorGrupo = 500)
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
            // Misma ventana que usa el mapa de riesgos, para que KPIs, tablas y
            // marcadores cuenten siempre lo mismo.
            var ventana = _context.EventosClimaticos
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.Fecha >= fechaDesde);

            var totalEventos = await ventana.CountAsync(cancellationToken);
            if (totalEventos == 0)
                {
                return null;
                }

            // Agregados en SQL con consultas scalar: antes se materializaban los miles
            // de eventos de la ventana en memoria (la tabla crece con cada escaneo
            // automático de Open-Meteo) y la query agotaba el timeout de 30s; la
            // página, al capturar la excepción, lo disfrazaba falsamente de
            // "Sin exposición detectada".
            var kilosTotales = await ventana.SumAsync(e => e.KilosPerdidosEstimados ?? 0m, cancellationToken);
            var lotesAfectados = await ventana.Select(e => e.LoteId).Distinct().CountAsync(cancellationToken);
            var productoresAfectados = await ventana.Select(e => e.ProductorId).Distinct().CountAsync(cancellationToken);
            // "Hoy" NO cuenta como ocurrido: el día todavía está en curso y el dato
            // puede cambiar con el próximo escáner cada 6 h. Por eso la separación es
            // estricta: Fecha < hoy (confirmado) vs Fecha >= hoy (hoy + pronóstico).
            var totalPronosticados = await ventana.CountAsync(e => e.Fecha >= hoy, cancellationToken);
            var kilosPronosticados = await ventana.Where(e => e.Fecha >= hoy).SumAsync(e => e.KilosPerdidosEstimados ?? 0m, cancellationToken);
            var totalRecientes = totalEventos - totalPronosticados;

            var porTipo = await ventana
                .GroupBy(e => e.TipoEvento)
                .Select(g => new { Tipo = g.Key, Cantidad = g.Count() })
                .ToListAsync(cancellationToken);

            var porSeveridad = await ventana
                .GroupBy(e => e.SeveridadEstimada)
                .Select(g => new { Severidad = g.Key, Cantidad = g.Count() })
                .ToListAsync(cancellationToken);

            // Las dos tablas de la vista: Top N seleccionado por FECHA (camino del índice
            // IX_EventosClimaticos_Fecha, sin operador SORT sobre toda la ventana) y el
            // orden por severidad se resuelve en memoria sobre las ≤N filas. El orden
            // global por severidad en SQL obligaba a sortear miles de filas (incluido un
            // nvarchar(max)), pedía un memory grant enorme y terminaba en
            // RESOURCE_SEMAPHORE / timeout según la presión del momento del servidor.
            // N es amplio (500) para que TODO lote con eventos en la ventana tenga sus
            // filas disponibles para el buscador/filtro y el botón «Ver en la tabla»
            // del mapa; con N=30 un lote amarillo (con eventos) quedaba fuera de la
            // tabla y el botón no encontraba nada.
            var eventosRecientes = await ventana
                .Where(e => e.Fecha < hoy)
                .OrderByDescending(e => e.Fecha)
                .Take(request.MaxEventosPorGrupo)
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
                    EsPronostico = false
                    })
                .ToListAsync(cancellationToken);
            eventosRecientes = OrdenarPorSeveridad(eventosRecientes);

            var eventosPronosticados = await ventana
                .Where(e => e.Fecha >= hoy)
                .OrderBy(e => e.Fecha)
                .Take(request.MaxEventosPorGrupo)
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
                    EsPronostico = true
                    })
                .ToListAsync(cancellationToken);
            eventosPronosticados = OrdenarPorSeveridad(eventosPronosticados);

            var tipoFrecuente = porTipo.OrderByDescending(t => t.Cantidad).Select(t => t.Tipo).FirstOrDefault() ?? "N/D";

            // Denominador REAL del % de pérdida: kilos netos de los lotes que tienen
            // al menos un evento en la ventana. Antes se dividía contra un 1.000.000 kg
            // fijo inventado, que no representaba a ningún lote real.
            var loteIdsVentana = ventana.Select(e => e.LoteId).Distinct();
            var kilosLotesAfectados = await _context.Lotes
                .AsNoTracking()
                .Where(l => loteIdsVentana.Contains(l.Id))
                .SumAsync(l => (decimal?)l.KilosNetos, cancellationToken) ?? 0m;
            var porcentaje = kilosLotesAfectados > 0 ? Math.Round((kilosTotales / kilosLotesAfectados) * 100, 2) : 0;

            return new ClimateRiskSummaryDto
                {
                TotalEventos = totalEventos,
                KilosPerdidosTotales = kilosTotales,
                PorcentajePerdidaTotal = porcentaje,
                TipoEventoMasFrecuente = tipoFrecuente,
                Observaciones = "Kilos afectados y % de pérdida son ESTIMACIONES calculadas por umbrales configurados (tipo y severidad del evento × kilos netos del lote), no mediciones de campo. El % de pérdida total se calcula sobre los kilos netos de los lotes con eventos en la ventana. Incluye eventos ya ocurridos, de hoy y pronosticados.",
                Eventos = eventosRecientes.Concat(eventosPronosticados).ToList(),
                PlanMitigacion = [],
                LotesAfectados = lotesAfectados,
                ProductoresAfectados = productoresAfectados,
                EventosPorTipo = porTipo.ToDictionary(t => t.Tipo, t => t.Cantidad),
                EventosPorSeveridad = porSeveridad.ToDictionary(s => s.Severidad, s => s.Cantidad),
                EventosRecientes = eventosRecientes,
                EventosPronosticados = eventosPronosticados,
                TotalEventosRecientes = totalRecientes,
                TotalEventosPronosticados = totalPronosticados,
                KilosEnRiesgoPronosticado = kilosPronosticados,
                KilosLotesAfectados = kilosLotesAfectados
                };
            }

        /// <summary>Ordena en memoria las ≤N filas ya seleccionadas: Crítica primero,
        /// luego Alta, y dentro de cada severidad lo más reciente. Se hace en el cliente
        /// para no forzar al servidor un SORT sobre toda la ventana de eventos.</summary>
        private static List<EventoClimaticoDto> OrdenarPorSeveridad(List<EventoClimaticoDto> eventos)
            => eventos
                .OrderByDescending(e => e.SeveridadEstimada == "Crítica" ? 2 : e.SeveridadEstimada == "Alta" ? 1 : 0)
                .ThenByDescending(e => e.Fecha)
                .ToList();
        }
    }
