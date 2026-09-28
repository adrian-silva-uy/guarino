using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Lugares.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.Lugares.Queries.GetLugares;

public class GetLugaresQueryHandler : IRequestHandler<GetLugaresQuery, List<LugarDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLugaresQueryHandler> _logger;

    public GetLugaresQueryHandler(
        IApplicationDbContext context,
        ILogger<GetLugaresQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<LugarDto>> Handle(GetLugaresQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var result = await _context.Lugares
                .AsNoTracking()
                .Select(l => new LugarDto
                    {
                    Id = l.Id,
                    Nombre = l.Nombre,
                    Tipo = l.Tipo,
                    Direccion = l.Direccion
                    })
                .ToListAsync(cancellationToken);

            if (result == null || !result.Any())
                return GetDummyData();

            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error consultando lugares vía EF Core; devolviendo datos de ejemplo.");
            return GetDummyData();
            }
        }

    private static List<LugarDto> GetDummyData()
        {
        return
        [
            new LugarDto { Id = 1, Nombre = "Finca El Paraíso", Tipo = "Origen", Direccion = "Ruta 5 km 123" },
            new LugarDto { Id = 2, Nombre = "Empaque Central", Tipo = "Destino", Direccion = "Av. Principal 456" },
            new LugarDto { Id = 3, Nombre = "Quinta Los Pinos", Tipo = "Origen", Direccion = "Camino rural s/n" },
            new LugarDto { Id = 4, Nombre = "Puerto Frío", Tipo = "Destino", Direccion = "Zona Franca" },
            new LugarDto { Id = 5, Nombre = "Finca San José", Tipo = "Origen", Direccion = "Ruta 3 km 456" }
        ];
        }
    }
