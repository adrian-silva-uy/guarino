using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.GraficaProcesos.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GuarinoApp.Application.Modules.GraficaProcesos.Queries.GetGraficaProcesos;

public class GetGraficaProcesosQueryHandler : IRequestHandler<GetGraficaProcesosQuery, List<GraficaProcesosDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetGraficaProcesosQueryHandler> _logger;

    public GetGraficaProcesosQueryHandler(IApplicationDbContext context, ILogger<GetGraficaProcesosQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<GraficaProcesosDto>> Handle(GetGraficaProcesosQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var query = _context.RitmosCalibradora.AsNoTracking();

            if (request.FechaDesde.HasValue)
                {
                query = query.Where(r => r.Fecha >= request.FechaDesde.Value);
                }

            if (request.FechaHasta.HasValue)
                {
                query = query.Where(r => r.Fecha <= request.FechaHasta.Value);
                }

            var result = await query
                .OrderBy(r => r.Fecha)
                .Select(r => new GraficaProcesosDto
                    {
                    Loteroda = r.Loteroda ?? string.Empty,
                    Fecha = r.Fecha,
                    IdEspecie = r.IdEspecie ?? 0,
                    Especie = r.Especie ?? string.Empty,
                    Variedad = r.Variedad ?? string.Empty,
                    IdVariedad = r.IdVariedad ?? 0,
                    Calibradora = r.Calibradora ?? 0,
                    DcteSincera = r.DcteSincera ?? 0,
                    Volcado = r.Volcado ?? 0,
                    Hscal = (int)(r.Hscal ?? 0),
                    Hspar = (int)(r.Hspar ?? 0),
                    Ritmo1 = r.Ritmo1 ?? 0,
                    Ritmo2 = r.Ritmo2 ?? 0,
                    Observacion = r.Observacion ?? string.Empty
                    })
                .ToListAsync(cancellationToken);

            if (result != null && result.Any())
                {
                return result;
                }

            // Cero datos inventados: si la vista no tiene registros se devuelve
            // vacío y la página informa "sin registros" (antes acá se generaban
            // filas dummy para desarrollo).
            return new List<GraficaProcesosDto>();
            }
        catch (Exception ex)
            {
            // Si la consulta falla (la vista no existe, no hay acceso a producción, etc.)
            // se devuelve vacío + log: nunca datos inventados (romperían la coherencia
            // de cifras con la base real en Demo/Prod).
            _logger.LogError(ex, "Error en GetGraficaProcesos: {Message}", ex.Message);
            return new List<GraficaProcesosDto>();
            }
        }
    }
