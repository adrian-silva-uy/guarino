using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Basculas.Dtos;
using MediatR;

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
            var sql = "SELECT Id, Descripcion, Ubicacion, Estado FROM Bascula";
            var result = await _context.ExecuteSqlRawAsync<BasculaDto>(sql);
            if (result == null || result.Count == 0)
                return GetDummyData();
            return result;
            }
        catch
            {
            return GetDummyData();
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
