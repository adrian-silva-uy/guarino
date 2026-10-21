using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.PalletsContramarca.Dtos;
using GuarinoApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.PalletsContramarca.Queries.GetPalletsContramarca;

public class GetPalletsContramarcaQueryHandler : IRequestHandler<GetPalletsContramarcaQuery, List<PalletContramarcaDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPalletsContramarcaQueryHandler> _logger;

    public GetPalletsContramarcaQueryHandler(IApplicationDbContext context, ILogger<GetPalletsContramarcaQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<PalletContramarcaDto>> Handle(GetPalletsContramarcaQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var query = _context.Pallets
                .AsNoTracking()
                // Excluimos bajas (EstadoPallet.Bajado): el estado vive solo en el enum.
                .Where(p => p.Estado != EstadoPallet.Bajado);

            var result = await query
                .GroupBy(p => p.Contramarca)
                .Select(g => new PalletContramarcaDto
                    {
                    Contramarca = g.Key ?? "Sin contramarca",
                    TotalPallets = g.Count(),
                    TotalEnvases = g.Sum(p => p.CantidadEnvases ?? 0),
                    PesoTotal = g.Sum(p => p.PesoNeto ?? 0)
                    })
                .OrderByDescending(x => x.TotalPallets) 
                .ToListAsync(cancellationToken);

            // Cero datos inventados: sin resultados → lista vacía (la página
            // informa "sin registros"); nunca filas de ejemplo.
            if (result.Count == 0)
                {
                return new List<PalletContramarcaDto>();
                }

            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error al obtener pallets por contramarca");
            return new List<PalletContramarcaDto>();
            }
        }
    }
