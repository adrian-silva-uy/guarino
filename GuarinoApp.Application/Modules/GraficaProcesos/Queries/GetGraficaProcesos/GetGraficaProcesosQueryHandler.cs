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

            // Si no hay datos reales (por ejemplo, la vista aún no tiene registros),
            // se devuelven datos dummy para el desarrollo.
            return new List<GraficaProcesosDto>();
            }
        catch (Exception ex)
            {
            // Si la consulta falla (la vista no existe, no hay acceso a producción, etc.),
            // se devuelven datos dummy para no romper la UI.
            _logger.LogError(ex, "Error en GetGraficaProcesos: {Message}", ex.Message);
            return new List<GraficaProcesosDto>();
            }
        }

    private List<GraficaProcesosDto> GetDummyData(DateTime? fechaDesde, DateTime? fechaHasta)
        {
        var random = new Random();
        var data = new List<GraficaProcesosDto>();

        // Generar 60 días de datos dummy (para que haya suficiente rango)
        var startDate = DateTime.Now.AddDays(-60);
        var endDate = DateTime.Now;

        // Si hay filtros, ajustar el rango de generación
        if (fechaDesde.HasValue)
            startDate = fechaDesde.Value;
        if (fechaHasta.HasValue)
            endDate = fechaHasta.Value;

        // Si el rango es muy grande, limitar a 60 días para no generar demasiados datos
        var totalDays = (endDate - startDate).Days;
        if (totalDays > 60) totalDays = 60;
        if (totalDays < 1) totalDays = 30;

        for (int i = 0; i < totalDays; i++)
            {
            var currentDate = startDate.AddDays(i);
            data.Add(new GraficaProcesosDto
                {
                Loteroda = $"L{1000 + i}",
                Fecha = currentDate,
                IdEspecie = 10,
                Especie = "Mandarina",
                Variedad = i % 2 == 0 ? "Murco" : "Común",
                IdVariedad = i % 2 == 0 ? 1 : 2,
                Calibradora = random.Next(50, 200),
                DcteSincera = random.Next(10, 50),
                Volcado = random.Next(100, 400),
                Hscal = random.Next(20, 80),
                Hspar = random.Next(10, 60),
                Ritmo1 = random.Next(80, 250),
                Ritmo2 = random.Next(60, 180),
                Observacion = i % 5 == 0 ? "Observación de muestra" : ""
                });
            }

        return data;
        }
    }
