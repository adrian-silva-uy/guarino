using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Contaduria.Queries.GetContextoOperativo;

/// <summary>
/// Calcula el contexto operativo de Contaduría consultando las MISMAS tablas
/// que sus módulos (Lotes, LotesEmboquille, Pallets) con idéntica lógica de
/// negocio, pero sin repetir las queries completas ni el fallback de ejemplo:
/// cada bloque se envuelve en try/catch y queda en null si falla, porque este
/// dato solo enriquece el análisis — nunca debe romper la página de Contaduría
/// ni entregar cifras inventadas al motor de IA.
/// </summary>
public class GetContextoOperativoQueryHandler
    : IRequestHandler<GetContextoOperativoQuery, ContextoOperativoContaduriaDto>
    {
    private readonly IApplicationDbContext _context;

    public GetContextoOperativoQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<ContextoOperativoContaduriaDto> Handle(GetContextoOperativoQuery request, CancellationToken ct)
        {
        var dto = new ContextoOperativoContaduriaDto { Anio = request.Anio };

        // --- Liquidaciones a productores del año (misma suma que GetResumenLiquidaciones) ---
        try
            {
            var hayLotes = await _context.Lotes.AsNoTracking()
                .AnyAsync(l => !l.IsDeleted, ct);
            if (hayLotes)
                {
                var anio = request.Anio;
                var liq = await _context.Lotes.AsNoTracking()
                    .Where(l => !l.IsDeleted && l.FechaIngreso.HasValue
                        && l.FechaIngreso.Value.Year == anio)
                    .Select(l => new
                        {
                        Kgs = (decimal?)l.KilosNetos,
                        Bins = (int?)l.CantidadBins
                        })
                    .ToListAsync(ct);

                // Si el año aún no arrancó no hay filas: se deja en null para
                // que el análisis diga "sin liquidaciones del año" en vez de "0".
                if (liq.Count > 0)
                    {
                    var kgs = liq.Sum(l => l.Kgs ?? 0m);
                    dto.LiquidacionesKgs = kgs;
                    dto.LiquidacionesBins = liq.Sum(l => l.Bins ?? 0);
                    // Misma convención de la página: TotalPagar = kilos × 10,
                    // redondeado a centavos (escala 2) como en ResumenLiquidaciones,
                    // para que el peso mostrado sea idéntico al de la grilla.
                    dto.PagarAProductores = Math.Round(kgs * 10, 2);
                    }
                }
            }
        catch
            {
            // Bloque opcional: null = dato ausente.
            }

        // --- Avance de cosecha: volcados del año (misma suma que Resumen Mensual) ---
        try
            {
            var hayEmboquille = await _context.LotesEmboquille.AsNoTracking()
                .AnyAsync(le => !le.Lote.IsDeleted, ct);
            if (hayEmboquille)
                {
                var anio = request.Anio;
                dto.KgsVolcadosAnio = await _context.LotesEmboquille.AsNoTracking()
                    .Where(le => !le.Lote.IsDeleted && le.FechaProduccion.Year == anio)
                    .SumAsync(le => (decimal?)le.KilosNetos, ct);
                }
            }
        catch
            {
            }

        // --- Stock en patio (misma fórmula que Stock en Playa, Estado "E") ---
        try
            {
            var hayEmboquille = await _context.LotesEmboquille.AsNoTracking().AnyAsync(ct);
            if (hayEmboquille)
                {
                dto.StockPlayaKg = await _context.LotesEmboquille.AsNoTracking()
                    .Where(le => le.Estado == EstadoEmboquille.EnPlaya)
                    .SumAsync(le => le.CantidadBins != 0
                        ? (decimal?)Math.Round(
                            (le.KilosNetos / le.CantidadBins)
                            * (le.CantidadBins - le.CantidadBinsDespachados - le.CantidadBinsVolcados), 0)
                        : 0m, ct);
                }
            }
        catch
            {
            }

        // --- Pallets sin cliente (mismo filtro que el módulo Stock de Pallets Sin Cliente) ---
        try
            {
            var hayPallets = await _context.Pallets.AsNoTracking().AnyAsync(ct);
            if (hayPallets)
                {
                var sinCliente = _context.Pallets.AsNoTracking()
                    .Where(p => string.IsNullOrEmpty(p.Cliente) && p.Estado != EstadoPallet.Bajado);
                dto.PalletsSinCliente = await sinCliente.CountAsync(ct);
                dto.PalletsSinClienteKg = await sinCliente.SumAsync(p => (decimal?)(p.PesoNeto ?? 0m), ct);
                }
            }
        catch
            {
            }

        return dto;
        }
    }
