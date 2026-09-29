using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Gerencia.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Gerencia.Queries.GetIndicadoresGerencia;
public class GetIndicadoresGerenciaQueryHandler : IRequestHandler<GetIndicadoresGerenciaQuery, List<IndicadorGerenciaDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetIndicadoresGerenciaQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<IndicadorGerenciaDto>> Handle(GetIndicadoresGerenciaQuery request, CancellationToken cancellationToken)
        {
        var result = new List<IndicadorGerenciaDto>();

        // Total Volcados
        var totalVolcados = await _context.LotesEmboquille
            .AsNoTracking()
            .Where(le => !le.Lote.IsDeleted)
            .GroupBy(le => 1)
            .Select(g => new IndicadorGerenciaDto
                {
                Indicador = "Total Volcados",
                Valor = g.Sum(le => le.KilosNetos),
                Fecha = g.Max(le => le.FechaProduccion),
                Categoria = "Producción"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (totalVolcados != null)
            result.Add(totalVolcados);

        // Total Pallets
        var totalPallets = await _context.Pallets
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .GroupBy(p => 1)
            .Select(g => new IndicadorGerenciaDto
                {
                Indicador = "Total Pallets",
                Valor = g.Count(),
                Fecha = g.Max(p => p.Fecha ?? p.CreatedAt),
                Categoria = "Stock"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (totalPallets != null)
            result.Add(totalPallets);

        // Stock en Playa
        var stockEnPlaya = await _context.Lotes
            .AsNoTracking()
            .Where(l => !l.IsDeleted)
            .GroupBy(l => 1)
            .Select(g => new IndicadorGerenciaDto
                {
                Indicador = "Stock en Playa",
                Valor = g.Sum(l => l.CantidadBins - l.CantidadBinsDesp - l.CantidadBinsEmb),
                Fecha = g.Max(l => l.FechaIngreso ?? l.CreatedAt),
                Categoria = "Stock"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (stockEnPlaya != null)
            result.Add(stockEnPlaya);

        return result;
        }
    }
