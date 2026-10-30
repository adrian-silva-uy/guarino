using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.RendimientoPacking.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.RendimientoPacking.Queries.GetRendimientoPacking;

public record GetRendimientoPackingQuery(int? Zafra = null, string? Variedad = null, string? Productor = null)
    : IRequest<RendimientoPackingResultDto>;

public class GetRendimientoPackingQueryHandler : IRequestHandler<GetRendimientoPackingQuery, RendimientoPackingResultDto>
{
    private readonly IApplicationDbContext _context;

    public GetRendimientoPackingQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RendimientoPackingResultDto> Handle(GetRendimientoPackingQuery request, CancellationToken cancellationToken)
    {
        var resultado = new RendimientoPackingResultDto();

        try
        {
            // Base: volcados con su lote (para zafra y variedad)
            var volcados = _context.Volcados
                .AsNoTracking()
                .Include(v => v.Lote)
                .ThenInclude(l => l!.Variedad)
                .Where(v => !v.Lote.IsDeleted)
                .AsQueryable();

            if (request.Zafra.HasValue)
                volcados = volcados.Where(v => v.Lote!.Zafra == request.Zafra.Value.ToString());

            if (!string.IsNullOrWhiteSpace(request.Variedad))
                volcados = volcados.Where(v => v.Lote!.Variedad!.Nombre.Contains(request.Variedad));

            // Pallets: clasificamos por categoría/estado/cliente
            // Exportable: cliente asignado y no descartado, o categoría contiene "Export"
            // Mercado interno: cliente asignado pero no export (nacional)
            // Descarte: estado descarte/bajado, o sin cliente y sin categoría de export
            var pallets = _context.Pallets
                .AsNoTracking()
                .Where(p => !string.IsNullOrEmpty(p.Variedad))
                .AsQueryable();

            if (request.Zafra.HasValue)
            {
                // Filtrar pallets por zafra a través del lote vinculado, o por año de fecha
                pallets = pallets.Where(p => p.Volcado != null && p.Volcado.Lote.Zafra == request.Zafra.Value.ToString());
            }

            var listaPallets = await pallets.ToListAsync(cancellationToken);
            var listaVolcados = await volcados.ToListAsync(cancellationToken);

            // Agrupar volcados por zafra/variedad/productor
            var grupos = listaVolcados
                .GroupBy(v => new
                {
                    Zafra = v.Lote?.Zafra ?? "Sin zafra",
                    Variedad = v.Lote?.Variedad?.Nombre ?? "Sin variedad",
                    Productor = v.Lote?.Productor?.Nombre ?? "Sin productor"
                })
                .Select(g => new
                {
                    g.Key.Zafra,
                    g.Key.Variedad,
                    g.Key.Productor,
                    KgVolcados = g.Sum(v => v.PesoKilos)
                })
                .ToList();

            foreach (var grupo in grupos)
            {
                if (!int.TryParse(grupo.Zafra, out var zafraNum)) continue;

                // Pallets de este grupo: por variedad y productor
                var palletsGrupo = listaPallets
                    .Where(p => string.Equals(p.Variedad, grupo.Variedad, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.Productor, grupo.Productor, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var kgExportable = palletsGrupo
                    .Where(p => !string.IsNullOrEmpty(p.Cliente)
                        && (p.Categoria?.Contains("Export", StringComparison.OrdinalIgnoreCase) == true
                            || p.Estado != GuarinoApp.Domain.Enums.EstadoPallet.Bajado))
                    .Sum(p => p.PesoNeto ?? 0m);

                var kgDescarte = palletsGrupo
                    .Where(p => p.Estado == GuarinoApp.Domain.Enums.EstadoPallet.Bajado)
                    .Sum(p => p.PesoNeto ?? 0m);

                var kgMercadoInterno = palletsGrupo
                    .Where(p => !string.IsNullOrEmpty(p.Cliente)
                        && p.Categoria?.Contains("Export", StringComparison.OrdinalIgnoreCase) != true
                        && p.Estado != GuarinoApp.Domain.Enums.EstadoPallet.Bajado)
                    .Sum(p => p.PesoNeto ?? 0m);

                // Si no hay clasificación por pallets, estimar: todo lo volcado que no es descarte
                // se considera exportable (conservador). Se ajusta según datos reales de pallets.
                if (palletsGrupo.Count == 0)
                {
                    kgExportable = grupo.KgVolcados;
                    kgMercadoInterno = 0;
                    kgDescarte = 0;
                }

                resultado.Detalle.Add(new RendimientoPackingDto
                {
                    Zafra = zafraNum,
                    Variedad = grupo.Variedad,
                    Productor = grupo.Productor,
                    KgVolcados = grupo.KgVolcados,
                    KgExportable = kgExportable,
                    KgMercadoInterno = kgMercadoInterno,
                    KgDescarte = kgDescarte
                });
            }
        }
        catch
        {
            // Si falla alguna tabla, devolvemos resultado vacío (sin datos inventados).
        }

        return resultado;
    }
}
