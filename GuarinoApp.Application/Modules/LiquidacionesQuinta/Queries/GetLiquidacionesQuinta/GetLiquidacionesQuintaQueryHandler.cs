using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.LiquidacionesQuinta.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.LiquidacionesQuinta.Queries.GetLiquidacionesQuinta;
public class GetLiquidacionesQuintaQueryHandler : IRequestHandler<GetLiquidacionesQuintaQuery, LiquidacionesQuintaResultDto>
    {
    private readonly IApplicationDbContext _context;

    public GetLiquidacionesQuintaQueryHandler(IApplicationDbContext context)
        {
        _context = context;
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
                    TotalPagar = g.Sum(l => l.KilosNetos * 10)
                    })
                .ToListAsync(cancellationToken);

            if (!result.Any())
                {
                // Datos de ejemplo solo si la tabla está vacía; un año sin resultados no es dato demo.
                var hayDatosReales = await _context.Lotes.AsNoTracking().AnyAsync(l => !l.IsDeleted, cancellationToken);
                if (!hayDatosReales)
                    {
                    var dummy = GetDummyData();
                    return new LiquidacionesQuintaResultDto(dummy, AniosDe(dummy), EsDummy: true);
                    }
                }

            return new LiquidacionesQuintaResultDto(result, anios, EsDummy: false);
            }
        catch
            {
            var dummy = GetDummyData();
            return new LiquidacionesQuintaResultDto(dummy, AniosDe(dummy), EsDummy: true);
            }
        }

    private static List<int> AniosDe(List<LiquidacionQuintaDto> data)
        => data.Select(d => d.Año).Distinct().OrderByDescending(a => a).ToList();

    private List<LiquidacionQuintaDto> GetDummyData()
        {
        var random = new Random();
        var quintas = new[] { "Quinta La Esperanza", "Quinta El Paraíso", "Quinta San José", "Quinta Los Pinos" };
        var productores = new[] { "Frutas del Sol", "Agrícola San Juan", "Viña del Valle" };
        var variedades = new[] { "Granny Smith", "Fuji", "Gala" };
        var years = new[] { 2024, 2025, 2026 };
        var data = new List<LiquidacionQuintaDto>();

        for (int i = 1; i <= 12; i++)
            {
            data.Add(new LiquidacionQuintaDto
                {
                Quinta = quintas[random.Next(quintas.Length)],
                Productor = productores[random.Next(productores.Length)],
                Variedad = variedades[random.Next(variedades.Length)],
                Año = years[random.Next(years.Length)],
                TotalKgs = random.Next(3000, 20000),
                TotalPagar = random.Next(30000, 200000)
                });
            }
        return data;
        }
    }
