using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetComparativaZafraSemanal;

public record GetComparativaZafraSemanalQuery(List<int>? Zafras = null) : IRequest<ComparativaZafraSemanalResultDto>;

public class GetComparativaZafraSemanalQueryHandler : IRequestHandler<GetComparativaZafraSemanalQuery, ComparativaZafraSemanalResultDto>
{
    private readonly IApplicationDbContext _context;

    public GetComparativaZafraSemanalQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ComparativaZafraSemanalResultDto> Handle(GetComparativaZafraSemanalQuery request, CancellationToken cancellationToken)
    {
        var resultado = new ComparativaZafraSemanalResultDto();

        try
        {
            // Obtener zafras disponibles si no se especifican
            var zafrasDisponibles = await _context.Lotes
                .AsNoTracking()
                .Where(l => !l.IsDeleted && !string.IsNullOrEmpty(l.Zafra))
                .Select(l => l.Zafra!)
                .Distinct()
                .OrderBy(z => z)
                .ToListAsync(cancellationToken);

            var zafrasAFiltrar = request.Zafras?.Any() == true
                ? request.Zafras
                : zafrasDisponibles.Where(z => int.TryParse(z, out _)).Select(int.Parse).ToList();

            foreach (var zafra in zafrasAFiltrar)
            {
                resultado.Zafras.Add(zafra);

                // Volcados de esta zafra agrupados por semana ISO
                var volcados = await _context.Volcados
                    .AsNoTracking()
                    .Include(v => v.Lote)
                    .Where(v => !v.Lote.IsDeleted && v.Lote.Zafra == zafra.ToString())
                    .Select(v => new
                    {
                        Semana = System.Globalization.ISOWeek.GetWeekOfYear(v.FechaVolcado),
                        Kg = v.PesoKilos
                    })
                    .ToListAsync(cancellationToken);

                // Pallets despachados de esta zafra agrupados por semana
                var pallets = await _context.Pallets
                    .AsNoTracking()
                    .Include(p => p.Volcado)
                    .ThenInclude(v => v!.Lote)
                    .Where(p => p.Volcado != null && !p.Volcado.Lote.IsDeleted
                        && p.Volcado.Lote.Zafra == zafra.ToString()
                        && p.Fecha.HasValue)
                    .Select(p => new
                    {
                        Semana = System.Globalization.ISOWeek.GetWeekOfYear(p.Fecha.Value),
                        Kg = p.PesoNeto ?? 0m
                    })
                    .ToListAsync(cancellationToken);

                // Acumular por semana (1-53)
                for (int semana = 1; semana <= 52; semana++)
                {
                    var kgSemana = volcados.Where(v => v.Semana == semana).Sum(v => v.Kg);
                    var palletsSemana = pallets.Where(p => p.Semana == semana).Sum(p => p.Kg);

                    var existente = resultado.Datos.FirstOrDefault(d => d.Semana == semana && d.Zafra == zafra);
                    if (existente != null)
                    {
                        existente.KgVolcadosAcumulados += kgSemana;
                        existente.PalletsDespachadosAcumulados += palletsSemana;
                    }
                    else
                    {
                        resultado.Datos.Add(new ComparativaZafraSemanalDto
                        {
                            Semana = semana,
                            Zafra = zafra,
                            KgVolcadosAcumulados = kgSemana,
                            PalletsDespachadosAcumulados = palletsSemana
                        });
                    }

                    if (!resultado.Semanas.Contains(semana))
                        resultado.Semanas.Add(semana);
                }
            }

            resultado.Semanas.Sort();
            resultado.Zafras.Sort();
        }
        catch
        {
            // Sin datos: devolvemos resultado vacío.
        }

        return resultado;
    }
}
