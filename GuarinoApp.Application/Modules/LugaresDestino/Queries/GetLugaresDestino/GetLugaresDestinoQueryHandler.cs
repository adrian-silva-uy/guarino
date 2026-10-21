using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.LugaresDestino.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.LugaresDestino.Queries.GetLugaresDestino;

public class GetLugaresDestinoQueryHandler : IRequestHandler<GetLugaresDestinoQuery, List<LugaresDestinoDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLugaresDestinoQueryHandler> _logger;

    public GetLugaresDestinoQueryHandler(
        IApplicationDbContext context,
        ILogger<GetLugaresDestinoQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<LugaresDestinoDto>> Handle(GetLugaresDestinoQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var result = await _context.LugaresDestino
                .AsNoTracking()
                .Select(l => new LugaresDestinoDto
                    {
                    IdLugarDestino = l.IdLugarDestino,
                    Nombre = l.Nombre,
                    Direccion = l.Direccion,
                    Localidad = l.Localidad
                    })
                .ToListAsync(cancellationToken);

            if (result == null || !result.Any())
                return new List<LugaresDestinoDto>();

            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error consultando lugares de destino vía EF Core; se devuelve vacío (sin datos de ejemplo).");
            return new List<LugaresDestinoDto>();
            }
        }
    }
