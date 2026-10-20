using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ClimateRisk.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.ClimateRisk.Queries
    {
    /// <summary>
    /// Trae los marcadores del mapa de riesgo climático: un punto por Lote con coordenadas,
    /// coloreado según la severidad de su evento más reciente DENTRO DE LA MISMA VENTANA
    /// QUE EL RESUMEN (histórico reciente + pronóstico): verde = sin eventos en la ventana.
    /// Antes no había límite de fecha y un evento de hace un año dejaba el lote en rojo
    /// para siempre, contradiciendo el alerta de "Sin exposición detectada".
    /// </summary>
    public record GetClimateMapMarkersQuery(int DiasHistorial = 30) : IRequest<List<ClimateMapMarkerDto>>;

    public class GetClimateMapMarkersQueryHandler
        : IRequestHandler<GetClimateMapMarkersQuery, List<ClimateMapMarkerDto>>
        {
        private readonly IApplicationDbContext _context;

        public GetClimateMapMarkersQueryHandler(IApplicationDbContext context)
            {
            _context = context;
            }

        public async Task<List<ClimateMapMarkerDto>> Handle(GetClimateMapMarkersQuery request, CancellationToken cancellationToken)
            {
            var lotes = await _context.Lotes
                .Where(l => !l.IsDeleted && l.Latitude != null && l.Longitude != null)
                .Select(l => new
                    {
                    l.Id,
                    l.Codigo,
                    l.Latitude,
                    l.Longitude,
                    ProductorNombre = l.Productor != null ? l.Productor.Nombre : "N/A"
                    })
                .ToListAsync(cancellationToken);

            if (lotes.Count == 0)
                {
                return [];
                }

            // Misma ventana que GetClimateRiskSummaryQuery (reciente + pronóstico):
            // mapa y KPIs tienen que contar exactamente lo mismo.
            var fechaDesde = DateTime.Today.AddDays(-request.DiasHistorial);

            var hoy = DateTime.Today;
            var eventosPorLote = await _context.EventosClimaticos
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.Fecha >= fechaDesde)
                .GroupBy(e => e.LoteId)
                .Select(g => new
                    {
                    LoteId = g.Key,
                    TotalEventos = g.Count(),
                    // Mismo corte que el resumen: "ya ocurrido" = Fecha < hoy;
                    // "hoy/pronóstico" = Fecha >= hoy (día en curso + próximos 7 días).
                    Ocurridos = g.Count(e => e.Fecha < hoy),
                    HoyPronosticados = g.Count(e => e.Fecha >= hoy),
                    UltimaFecha = g.Max(e => e.Fecha),
                    SeveridadReciente = g.OrderByDescending(e => e.Fecha).Select(e => e.SeveridadEstimada).FirstOrDefault()
                    })
                .ToListAsync(cancellationToken);

            var dictEventos = eventosPorLote.ToDictionary(e => e.LoteId);

            return lotes.Select(l =>
            {
                dictEventos.TryGetValue(l.Id, out var info);

                var color = info == null
                    ? "#4caf50"
                    : info.SeveridadReciente == "Crítica" ? "#d32f2f" : "#f9a825";

                // El popup desglosa el conteo para que no haya que adivinar qué es
                // "pronóstico": se ve exactamente cuántos eventos ya pasaron y cuántos
                // son de hoy/pronóstico (estos últimos aparecen en la tabla inferior).
                var infoTexto = info == null
                    ? $"Sin eventos en los últimos {request.DiasHistorial} días ni en el pronóstico (7 días)"
                    : $"{info.TotalEventos} evento(s) en los últimos {request.DiasHistorial} días + pronóstico de 7 días: "
                      + $"{info.Ocurridos} ya ocurrido(s) · {info.HoyPronosticados} de hoy/pronóstico. "
                      + $"Último: {info.UltimaFecha:dd/MM} ({info.SeveridadReciente})";

                return new ClimateMapMarkerDto
                    {
                    Codigo = l.Codigo,
                    Lat = (double)l.Latitude!.Value,
                    Lng = (double)l.Longitude!.Value,
                    Label = $"{l.Codigo} — {l.ProductorNombre}",
                    Info = infoTexto,
                    Color = color
                    };
            }).ToList();
            }
        }
    }
