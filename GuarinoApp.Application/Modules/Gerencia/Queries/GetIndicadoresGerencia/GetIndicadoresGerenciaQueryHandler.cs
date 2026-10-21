using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Gerencia.Dtos;
using GuarinoApp.Domain.Enums;
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
                Categoria = "Producción",
                Unidad = "kg"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (totalVolcados != null)
            result.Add(totalVolcados);

        // Volcados de la zafra en curso (año calendario): la lectura que a Gerencia le
        // importa comparar contra la proyección de la próxima zafra.
        var anioActual = DateTime.Now.Year;
        var volcadosZafra = await _context.LotesEmboquille
            .AsNoTracking()
            .Where(le => le.FechaProduccion.Year == anioActual && !le.Lote.IsDeleted)
            .GroupBy(le => 1)
            .Select(g => new IndicadorGerenciaDto
                {
                Indicador = "Volcados Zafra Actual",
                Valor = g.Sum(le => le.KilosNetos),
                Fecha = g.Max(le => le.FechaProduccion),
                Categoria = "Producción",
                Unidad = "kg"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (volcadosZafra != null)
            result.Add(volcadosZafra);

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
                Categoria = "Stock",
                Unidad = "pallets"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (totalPallets != null)
            result.Add(totalPallets);

        // Pallets por estado crítico del flujo exportación (Despachado / En Frío)
        var palletsDespachados = await _context.Pallets
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Estado == EstadoPallet.Despachado)
            .GroupBy(p => 1)
            .Select(g => new IndicadorGerenciaDto
                {
                Indicador = "Pallets Despachados",
                Valor = g.Count(),
                Categoria = "Logística",
                Unidad = "pallets"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (palletsDespachados != null)
            result.Add(palletsDespachados);

        var palletsEnFrio = await _context.Pallets
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Estado == EstadoPallet.EnFrio)
            .GroupBy(p => 1)
            .Select(g => new IndicadorGerenciaDto
                {
                Indicador = "Pallets en Frío",
                Valor = g.Count(),
                Fecha = g.Max(p => p.Fecha ?? p.CreatedAt),
                Categoria = "Stock",
                Unidad = "pallets"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (palletsEnFrio != null)
            result.Add(palletsEnFrio);

        // Stock en Playa: KG en patio con la MISMA fórmula canónica que la grilla
        // Stock en Playa y la tarjeta de Contaduría (LotesEmboquille Estado "E",
        // kilo prorrateado por los bins restantes, redondeado por fila): un solo
        // número de "stock en playa" en toda la app, medido en kg. Antes este
        // indicador sumaba BINS desde Lotes y Gerencia mostraba6.750 mientras
        // Contaduría mostraba 1.417.214 kg con la misma etiqueta.
        var stockEnPlaya = await _context.LotesEmboquille
            .AsNoTracking()
            .Where(le => le.Estado == EstadoEmboquille.EnPlaya)
            .GroupBy(le => 1)
            .Select(g => new IndicadorGerenciaDto
                {
                Indicador = "Stock en Playa",
                Valor = g.Sum(le => le.CantidadBins != 0
                    ? Math.Round(
                        (le.KilosNetos / le.CantidadBins)
                        * (le.CantidadBins - le.CantidadBinsDespachados - le.CantidadBinsVolcados), 0)
                    : 0m),
                Fecha = g.Max(le => le.FechaProduccion),
                Categoria = "Stock",
                Unidad = "kg"
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (stockEnPlaya != null)
            result.Add(stockEnPlaya);

        // Ritmo promedio de la línea de calibrado (últimos 30 registros con ritmo):
        // el indicador operativo que Gerencia mira para detectar cuellos de planta.
        var ritmos = await _context.RitmosCalibradora
            .AsNoTracking()
            .Where(r => r.Ritmo1 != null)
            .OrderByDescending(r => r.Fecha)
            .Take(30)
            .Select(r => new { r.Fecha, Valor = (decimal)r.Ritmo1!.Value })
            .ToListAsync(cancellationToken);

        if (ritmos.Count > 0)
            {
            result.Add(new IndicadorGerenciaDto
                {
                Indicador = "Ritmo Promedio Línea",
                Valor = ritmos.Average(r => r.Valor),
                Fecha = ritmos.Max(r => r.Fecha),
                Categoria = "Operación",
                Unidad = "kg/h"
                });
            }

        return result;
        }
    }
