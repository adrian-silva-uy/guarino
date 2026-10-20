using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.ResumenLiquidaciones.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.ResumenLiquidaciones.Queries.GetResumenLiquidaciones;
public class GetResumenLiquidacionesQueryHandler : IRequestHandler<GetResumenLiquidacionesQuery, ResumenLiquidacionesResultDto>
    {
    private readonly IApplicationDbContext _context;

    public GetResumenLiquidacionesQueryHandler(IApplicationDbContext context)
        {
        _context = context;
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

            if (!result.Any())
                {
                // Datos de ejemplo solo si la tabla está vacía; un año sin resultados no es dato demo.
                var hayDatosReales = await _context.Lotes.AsNoTracking().AnyAsync(l => !l.IsDeleted, cancellationToken);
                if (!hayDatosReales)
                    {
                    var dummy = GetDummyData();
                    return new ResumenLiquidacionesResultDto(dummy, AniosDe(dummy), EsDummy: true);
                    }
                }

            return new ResumenLiquidacionesResultDto(result, anios, EsDummy: false);
            }
        catch
            {
            var dummy = GetDummyData();
            return new ResumenLiquidacionesResultDto(dummy, AniosDe(dummy), EsDummy: true);
            }
        }

    private static List<int> AniosDe(List<ResumenLiquidacionDto> data)
        => data.Select(d => d.Año).Distinct().OrderByDescending(a => a).ToList();

    private List<ResumenLiquidacionDto> GetDummyData()
        {
        var random = new Random();
        var productores = new[] { "Frutas del Sol", "Agrícola San Juan", "Viña del Valle", "Huerta Orgánica" };
        var variedades = new[] { "Valencia", "Navel", "Satsuma", "Murcott", "Eureka", "Lisboa", "Marsh", "Star Ruby" };
        var years = new[] { 2024, 2025, 2026 };
        var data = new List<ResumenLiquidacionDto>();

        for (int i = 1; i <= 12; i++)
            {
            data.Add(new ResumenLiquidacionDto
                {
                Productor = productores[random.Next(productores.Length)],
                Variedad = variedades[random.Next(variedades.Length)],
                Año = years[random.Next(years.Length)],
                TotalKgs = random.Next(5000, 30000),
                TotalBins = random.Next(50, 300),
                TotalPagar = random.Next(50000, 300000)
                });
            }
        return data;
        }
    }
