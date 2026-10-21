using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.StockPalletsSinCliente.Dtos;
using GuarinoApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.StockPalletsSinCliente.Queries.GetStockPalletsSinCliente;

public class GetStockPalletsSinClienteQueryHandler : IRequestHandler<GetStockPalletsSinClienteQuery, List<StockPalletSinClienteDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetStockPalletsSinClienteQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<StockPalletSinClienteDto>> Handle(GetStockPalletsSinClienteQuery request, CancellationToken cancellationToken)
        {
        try
            {
            // Una sola fuente de verdad: el estado es el enum Estado (antes se
            // agrupaba también por EstadoTexto, duplicando el estado y perdiendo
            // filas con NULL al filtrar contra el texto 'B').
            var rawData = await _context.Pallets
                .AsNoTracking()
                .Where(p => string.IsNullOrEmpty(p.Cliente) && p.Estado != EstadoPallet.Bajado)
                .GroupBy(p => new
                    {
                    Variedad = p.Variedad ?? "Sin variedad",
                    Estado = p.Estado
                    })
                .Select(g => new
                    {
                    g.Key.Variedad,
                    g.Key.Estado,
                    Cantidad = g.Count(),
                    PesoTotal = (double)(g.Sum(x => x.PesoNeto) ?? 0m)
                    })
                .ToListAsync(cancellationToken);

            var result = rawData.Select(g => new StockPalletSinClienteDto
                {
                Variedad = g.Variedad,
                Cantidad = g.Cantidad,
                PesoTotal = (decimal)g.PesoTotal,
                Estado = g.Estado.ToDisplayName()
                }).ToList();

            return result;
            }
        catch
            {
            // Nunca datos inventados: ante un error de consulta se devuelve la
            // lista vacía y la página muestra "No se encontraron registros".
            return new List<StockPalletSinClienteDto>();
            }
        }
    }
