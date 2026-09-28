using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ClimateRisk.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.ClimateRisk.Queries
    {
    /// <summary>
    /// Trae los marcadores del mapa de riesgo climático: un punto por Lote con coordenadas,
    /// coloreado según la severidad de su evento más reciente (verde = sin eventos).
    /// </summary>
    public record GetClimateMapMarkersQuery : IRequest<List<ClimateMapMarkerDto>>;

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
                .Where(l => l.Latitude != null && l.Longitude != null)
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

            var eventosPorLote = await _context.EventosClimaticos
                .Where(e => !e.IsDeleted)
                .GroupBy(e => e.LoteId)
                .Select(g => new
                    {
                    LoteId = g.Key,
                    TotalEventos = g.Count(),
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

                var infoTexto = info == null
                    ? "Sin eventos registrados"
                    : $"{info.TotalEventos} evento(s) · Último: {info.UltimaFecha:dd/MM} ({info.SeveridadReciente})";

                return new ClimateMapMarkerDto
                    {
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
