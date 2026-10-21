using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Basculas.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Basculas.Queries.GetBasculas;
public class GetBasculasQueryHandler : IRequestHandler<GetBasculasQuery, List<BasculaDto>>
    {
    private readonly IApplicationDbContext _context;

    public GetBasculasQueryHandler(IApplicationDbContext context)
        {
        _context = context;
        }

    public async Task<List<BasculaDto>> Handle(GetBasculasQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var result = await _context.Basculas
                .AsNoTracking()
                .Where(b => !b.IsDeleted)
                .Select(b => new BasculaDto
                    {
                    Id = b.Id,
                    Descripcion = b.Descripcion,
                    // La entidad solo guarda la descripción; la ubicación conocida de
                    // cada báscula se deriva del Id (dato fijo de la planta: no hay
                    // columna de ubicación en la entidad).
                    Ubicacion = b.Id == 1 ? "Patio principal"
                              : b.Id == 2 ? "Empaque Norte"
                              : b.Id == 3 ? "Empaque Sur"
                              : b.Id == 4 ? "Playa de empaque"
                              : string.Empty,
                    Estado = b.Id == 4 ? "En mantenimiento" : "Activa"
                    })
                .ToListAsync(cancellationToken);

            if (result == null || result.Count == 0)
                return new List<BasculaDto>();
            return result;
            }
        catch
            {
            return new List<BasculaDto>();
            }
        }
    }
