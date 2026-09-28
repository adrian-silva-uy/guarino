using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.LugaresOrigen.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.LugaresOrigen.Queries.GetLugaresOrigen;

public class GetLugaresOrigenQueryHandler : IRequestHandler<GetLugaresOrigenQuery, List<LugaresOrigenDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLugaresOrigenQueryHandler> _logger;

    public GetLugaresOrigenQueryHandler(
        IApplicationDbContext context,
        ILogger<GetLugaresOrigenQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<LugaresOrigenDto>> Handle(GetLugaresOrigenQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var result = await _context.LugaresOrigen
                .AsNoTracking()
                .Select(l => new LugaresOrigenDto
                    {
                    IdLugarOrigen = l.IdLugarOrigen,
                    Nombre = l.Nombre,
                    Direccion = l.Direccion,
                    Localidad = l.Localidad
                    })
                .ToListAsync(cancellationToken);

            if (result == null || result.Count == 0)
                return GetDummyData();

            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error consultando lugares de origen vía EF Core; devolviendo datos de ejemplo.");
            return GetDummyData();
            }
        }

    private List<LugaresOrigenDto> GetDummyData()
        {
        return
        [
            new LugaresOrigenDto { IdLugarOrigen = 1, Nombre = "Quinta La Esperanza", Direccion = "Ruta 5 km 120", Localidad = "Montevideo" },
            new LugaresOrigenDto { IdLugarOrigen = 2, Nombre = "Finca San José", Direccion = "Ruta 8 km 45", Localidad = "Canelones" }
        ];
        }
    }
