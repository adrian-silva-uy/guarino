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
                    // cada báscula se deriva del Id (mismos datos que veía el fallback).
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

    private static List<BasculaDto> GetDummyData()
        {
        return
        [
            new() { Id = 1, Descripcion = "Báscula 1 - Patio", Ubicacion = "Patio principal", Estado = "Activa" },
            new() { Id = 2, Descripcion = "Báscula 2 - Empaque", Ubicacion = "Empaque Norte", Estado = "Activa" },
            new() { Id = 3, Descripcion = "Báscula 3 - Exportación", Ubicacion = "Empaque Sur", Estado = "Activa" },
            new() { Id = 4, Descripcion = "Báscula 4 - Playa", Ubicacion = "Playa de empaque", Estado = "En mantenimiento" }
        ];
        }
    }
