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
                return new List<LugarDto>();

            return result;
            }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error consultando lugares vía EF Core; se devuelve vacío (sin datos de ejemplo).");
            return new List<LugarDto>();
            }
        }
    }
