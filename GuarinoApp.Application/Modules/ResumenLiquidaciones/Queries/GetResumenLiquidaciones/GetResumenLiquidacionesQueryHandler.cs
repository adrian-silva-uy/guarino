using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ResumenLiquidaciones.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.ResumenLiquidaciones.Queries.GetResumenLiquidaciones;
public class GetResumenLiquidacionesQueryHandler : IRequestHandler<GetResumenLiquidacionesQuery, ResumenLiquidacionesResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetResumenLiquidacionesQueryHandler> _logger;

    public GetResumenLiquidacionesQueryHandler(IApplicationDbContext context, ILogger<GetResumenLiquidacionesQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<ResumenLiquidacionesResultDto> Handle(GetResumenLiquidacionesQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // Años disponibles para el selector (sin filtro de año).
            var anios = await _context.Lotes
                .AsNoTracking()
                .Where(l => !l.IsDeleted && l.FechaIngreso.HasValue)
                .Select(l => l.FechaIngreso!.Value.Year)
                .Distinct()
                .OrderByDescending(a => a)
                .ToListAsync(cancellationToken);

            var result = await _context.Lotes
                .AsNoTracking()
                .Where(l => !l.IsDeleted && l.FechaIngreso.HasValue
                    && (!request.Anio.HasValue || l.FechaIngreso.Value.Year == request.Anio.Value))
                .GroupBy(l => new { Productor = l.Productor.Nombre, Variedad = l.Variedad.Nombre, Año = l.FechaIngreso!.Value.Year })
                .Select(g => new ResumenLiquidacionDto
                    {
                    Productor = g.Key.Productor,
                    Variedad = g.Key.Variedad,
                    Año = g.Key.Año,
                    TotalKgs = g.Sum(l => l.KilosNetos),
                    TotalBins = g.Sum(l => l.CantidadBins),
                    // TotalPagar = kilos × 10 (pesos); el redondeo a centavos se hace
                    // en memoria más abajo (ver comentario).
                    TotalPagar = g.Sum(l => l.KilosNetos * 10),
                    })
                .ToListAsync(cancellationToken);

            // TotalPagar en pesos: la BD trae kg con 3 decimales. ROUND() en SQL
            // conserva la escala original (x,3) y la grilla lo imprimía como
            // "22600,000"; el redondeo en memoria fija escala 2 → "22600,00" en la
            // grilla, los exports y el email. Centavos exactos en toda la app.
            foreach (var fila in result)
                fila.TotalPagar = Math.Round(fila.TotalPagar, 2);

            // Cero datos inventados: tabla vacía, año sin resultados o error de
            // BD → lista vacía real (la página informa "sin registros"). Nunca
            // filas de ejemplo: en Demo/Prod todos los saldos salen de DTrace.
            return new ResumenLiquidacionesResultDto(result, anios, EsDummy: false);
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error consultando resumen de liquidaciones; se devuelve vacío (sin datos de ejemplo).");
            return new ResumenLiquidacionesResultDto(new List<ResumenLiquidacionDto>(), new List<int>(), EsDummy: false);
            }
        }
    }
