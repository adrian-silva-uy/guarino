using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.LiquidacionesQuinta.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.LiquidacionesQuinta.Queries.GetLiquidacionesQuinta;
public class GetLiquidacionesQuintaQueryHandler : IRequestHandler<GetLiquidacionesQuintaQuery, LiquidacionesQuintaResultDto>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLiquidacionesQuintaQueryHandler> _logger;

    public GetLiquidacionesQuintaQueryHandler(IApplicationDbContext context, ILogger<GetLiquidacionesQuintaQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<LiquidacionesQuintaResultDto> Handle(GetLiquidacionesQuintaQuery request, CancellationToken cancellationToken)
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
                .GroupBy(l => new { Quinta = l.Quinta.Nombre, Productor = l.Productor.Nombre, Variedad = l.Variedad.Nombre, Año = l.FechaIngreso!.Value.Year })
                .Select(g => new LiquidacionQuintaDto
                    {
                    Quinta = g.Key.Quinta,
                    Productor = g.Key.Productor,
                    Variedad = g.Key.Variedad,
                    Año = g.Key.Año,
                    TotalKgs = g.Sum(l => l.KilosNetos),
                    // TotalPagar = kilos × 10 (pesos); el redondeo a centavos se hace
                    // en memoria más abajo (ver comentario).
                    TotalPagar = g.Sum(l => l.KilosNetos * 10),
                    })
                .ToListAsync(cancellationToken);

            // TotalPagar en pesos: la BD trae kg con 3 decimales. ROUND() en SQL
            // conserva la escala original (x,3) y la grilla lo imprimía como
            // "22600,000"; el redondeo en memoria fija escala 2 → "22600,00" en la
            // grilla y los exports. Centavos exactos.
            foreach (var fila in result)
                fila.TotalPagar = Math.Round(fila.TotalPagar, 2);

            // Cero datos inventados: tabla vacía, año sin resultados o error de
            // BD → lista vacía real (la página informa "sin registros"). Nunca
            // filas de ejemplo: en Demo/Prod todos los saldos salen de DTrace.
            return new LiquidacionesQuintaResultDto(result, anios, EsDummy: false);
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error consultando liquidaciones de quinta; se devuelve vacío (sin datos de ejemplo).");
            return new LiquidacionesQuintaResultDto(new List<LiquidacionQuintaDto>(), new List<int>(), EsDummy: false);
            }
        }
    }
