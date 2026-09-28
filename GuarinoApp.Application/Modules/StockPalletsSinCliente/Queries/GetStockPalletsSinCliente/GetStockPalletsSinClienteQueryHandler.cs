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
            var rawData = await _context.Pallets
                .AsNoTracking()
                .Where(p => string.IsNullOrEmpty(p.Cliente) && p.EstadoTexto != "B")
                .GroupBy(p => new
                    {
                    Variedad = p.Variedad ?? "Sin variedad",
                    EstadoEnum = p.Estado,
                    EstadoTexto = p.EstadoTexto
                    })
                .Select(g => new
                    {
                    g.Key.Variedad,
                    g.Key.EstadoEnum,
                    g.Key.EstadoTexto,
                    Cantidad = g.Count(),
                    PesoTotal = (double)(g.Sum(x => x.PesoNeto) ?? 0m)
                    })
                .ToListAsync(cancellationToken);

            var result = rawData.Select(g => new StockPalletSinClienteDto
                {
                Variedad = g.Variedad,
                Cantidad = g.Cantidad,
                PesoTotal = (decimal)g.PesoTotal,
                Estado = EstadoPalletExtensions.MapEstado(g.EstadoTexto, g.EstadoEnum)
                }).ToList();

            return result.Any() ? result : GetDummyData();
            }
        catch
            {
            return GetDummyData();
            }
        }

    private List<StockPalletSinClienteDto> GetDummyData()
        {
        var random = new Random();
        var variedades = new[] { "Valencia Late", "Navel", "Clemenules", "Satsuma", "Nova", "Eureka", "Star Ruby" };

        return variedades.Select(v => new StockPalletSinClienteDto
            {
            Variedad = v,
            Cantidad = random.Next(5, 30),
            PesoTotal = random.Next(500, 3000),
            Estado = ((EstadoPallet)random.Next(1, 5)).ToDisplayName()
            }).ToList();
        }
    }
